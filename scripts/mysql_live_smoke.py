#!/usr/bin/env python3
"""Run the MySqlLiveViewSmoke harness on a run-owned, disposable wslc instance."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import tarfile
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import BinaryIO, Sequence


DEFAULT_IMAGE = "mysql:8.4.11"
DEFAULT_WSLC = "/mnt/c/Program Files/WSL/wslc.exe"
SOCKET_PATH = "/var/run/mysqld/mysqld.sock"
PROJECT = Path("tests/MySqlLiveViewSmoke/MySqlLiveViewSmoke.csproj")
OUTPUT = Path("tests/MySqlLiveViewSmoke/bin/Release/net10.0")
SUMMARY = re.compile(
    r"Selected:\s*(\d+);\s*completed:\s*(\d+);\s*failed:\s*(\d+);\s*skipped:\s*(\d+)\."
)
READY = re.compile(r"^READY mysql_version=(\S+) skip_networking=(\d+)$", re.MULTILINE)
HEX_ID = re.compile(r"^[0-9a-f]{64}$")
SECRET_ASSIGNMENT = re.compile(
    r"(?i)\b(password|passwd|pwd|token|secret)\b(\s*[:=]\s*)([^,\s;&]+)"
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def redact_text(value: str) -> str:
    return SECRET_ASSIGNMENT.sub(r"\1\2[REDACTED]", value)


def safe_argv(argv: Sequence[str]) -> list[str]:
    safe: list[str] = []
    redact_next = False
    for raw in argv:
        arg = str(raw)
        if redact_next:
            safe.append("[REDACTED]")
            redact_next = False
            continue
        if arg.lower() in {"--password", "--passwd", "--token", "--secret"}:
            safe.append(arg)
            redact_next = True
            continue
        if re.match(r"(?i)^--?(password|passwd|token|secret)=", arg):
            key, _, _ = arg.partition("=")
            safe.append(f"{key}=[REDACTED]")
            continue
        # This is a fixed boolean setting, not a credential.
        if arg == "MYSQL_ALLOW_EMPTY_PASSWORD=1":
            safe.append(arg)
            continue
        safe.append(redact_text(arg))
    if redact_next:
        safe.append("[REDACTED]")
    return safe


@dataclass
class CommandResult:
    exit_code: int
    output: str
    timed_out: bool = False
    binary_output: bytes | None = None
    stdout: str = ""
    stderr: str = ""


class Evidence:
    def __init__(self, root: Path):
        self.root = root
        self.logs = root / "logs"
        self.logs.mkdir(parents=True)
        self.journal = root / "commands.jsonl"
        self.sequence = 0

    def run(
        self,
        name: str,
        argv: Sequence[str],
        *,
        cwd: Path,
        timeout: int,
        stdin_path: Path | None = None,
        binary_output: bool = False,
    ) -> CommandResult:
        self.sequence += 1
        log_name = f"{self.sequence:02d}-{name}.log"
        log_path = self.logs / log_name
        stdin_file: BinaryIO | None = stdin_path.open("rb") if stdin_path else None
        stdin_sha256 = None
        if stdin_file:
            digest = hashlib.sha256()
            for chunk in iter(lambda: stdin_file.read(1024 * 1024), b""):
                digest.update(chunk)
            stdin_sha256 = digest.hexdigest()
            stdin_file.seek(0)
        started = time.monotonic()
        timed_out = False
        try:
            completed = subprocess.run(
                list(argv),
                cwd=cwd,
                stdin=stdin_file if stdin_file else subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=timeout,
                check=False,
            )
            exit_code = completed.returncode
            stdout = completed.stdout or b""
            stderr = completed.stderr or b""
        except subprocess.TimeoutExpired as error:
            timed_out = True
            exit_code = 124
            stdout = error.stdout or b""
            stderr = error.stderr or b""
            if isinstance(stdout, str):
                stdout = stdout.encode()
            if isinstance(stderr, str):
                stderr = stderr.encode()
        except OSError as error:
            exit_code = 127
            stdout = b""
            stderr = f"{type(error).__name__}: command could not be started".encode()
        finally:
            if stdin_file:
                stdin_file.close()

        stdout_text = "" if binary_output else redact_text(stdout.decode("utf-8", errors="replace"))
        stderr_text = redact_text(stderr.decode("utf-8", errors="replace"))
        if binary_output:
            binary = stdout
            output = stderr_text
            log_path.write_text(output, encoding="utf-8")
        else:
            binary = None
            output = stdout_text + ("\n" if stdout_text and stderr_text else "") + stderr_text
            log_path.write_text(output, encoding="utf-8")

        record = {
            "sequence": self.sequence,
            "name": name,
            "argv": safe_argv(argv),
            "cwd": str(cwd),
            "stdin": str(stdin_path.relative_to(self.root)) if stdin_path else None,
            "stdin_sha256": stdin_sha256,
            "stdout_binary": binary_output,
            "log": str(log_path.relative_to(self.root)),
            "exit_code": exit_code,
            "timed_out": timed_out,
            "elapsed_ms": round((time.monotonic() - started) * 1000),
        }
        with self.journal.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(record, sort_keys=True) + "\n")
        return CommandResult(
            exit_code=exit_code,
            output=output,
            timed_out=timed_out,
            binary_output=binary,
            stdout=stdout_text,
            stderr=stderr_text,
        )


class RunFailure(RuntimeError):
    def __init__(self, message: str, exit_code: int = 1):
        super().__init__(message)
        self.exit_code = exit_code


class LiveSmokeRunner:
    def __init__(
        self,
        root: Path,
        evidence_dir: Path,
        *,
        dotnet: str,
        wslc: str,
        image: str = DEFAULT_IMAGE,
        readiness_timeout: int = 120,
        readiness_interval: float = 2.0,
    ):
        self.root = root.resolve()
        self.evidence_dir = evidence_dir.resolve()
        self.dotnet = dotnet
        self.wslc = wslc
        self.image = image
        self.readiness_timeout = readiness_timeout
        self.readiness_interval = readiness_interval
        self.run_id = uuid.uuid4().hex
        self.container_name = f"nextorm-mysql-live-{self.run_id}"
        self.remote_dir = f"/tmp/nextorm-mysql-live-{self.run_id}"
        self.evidence = Evidence(self.evidence_dir)
        self.container_id: str | None = None
        self.candidate_container_id: str | None = None
        self.container_create_attempted = False
        self.volume_id: str | None = None
        self.volume_owned = False
        self.resource_inspection: dict | None = None
        self.ownership_failure: str | None = None
        self.server_version: str | None = None
        self.live_counts: dict[str, int] | None = None
        self.payload_sha256: str | None = None
        self.final_exit_code = 1
        self.failure: str | None = None

    def run(self) -> int:
        started = utc_now()
        report: dict = {
            "run_id": self.run_id,
            "started_utc": started,
            "repository": str(self.root),
            "image_requested": self.image,
            "container_name": self.container_name,
            "resource_policy": {
                "mode": "new disposable instance only",
                "existing_endpoints_supported": False,
                "pull_policy": "never",
                "network": "private bridge; no published ports",
                "host_mounts": "none",
                "credentials": "none supplied or logged; root has an empty password only in this disposable instance",
                "fixture_data": "three empty schema objects; no application rows",
            },
        }
        before: dict | None = None
        (self.evidence_dir / "run.json").write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )

        try:
            before = self.source_identity()
            self.write_identity("sources-before.json", before)
            report["source_identity_before"] = self.identity_summary(before)
            build = self.build()
            report["build"] = build
            if build["exit_code"] != 0:
                raise RunFailure("Affected smoke project build failed.", build["exit_code"])

            assembly_manifest = self.assembly_manifest()
            report["assemblies"] = assembly_manifest
            payload = self.package_payload(assembly_manifest)
            report["payload"] = payload
            self.payload_sha256 = payload["sha256"]

            self.create_and_run_live_test()
            self.final_exit_code = 0
        except RunFailure as error:
            self.failure = str(error)
            self.final_exit_code = error.exit_code or 1
        except Exception as error:
            # Avoid exception strings from external libraries or processes in durable logs.
            self.failure = f"Runner error: {type(error).__name__}."
            self.final_exit_code = 1
        finally:
            if before is not None:
                try:
                    after = self.source_identity()
                    self.write_identity("sources-after.json", after)
                    report["source_identity_after"] = self.identity_summary(after)
                    if before["manifest_sha256"] != after["manifest_sha256"]:
                        self.failure = self.failure or "Source identity changed during the run."
                        self.final_exit_code = 1
                except Exception as error:
                    self.failure = self.failure or (
                        f"Could not capture final source identity ({type(error).__name__})."
                    )
                    self.final_exit_code = 1
            teardown = self.teardown()
            staging_removed = self.remove_staging()
            if not staging_removed:
                self.final_exit_code = 1
                self.failure = self.failure or "Local payload staging could not be removed."
            report["ownership"] = {
                "created_by_this_run": (
                    True if self.container_id else False if not self.container_create_attempted else None
                ),
                "identity_verified": self.container_id is not None,
                "ownership_failure": self.ownership_failure,
                "container_name": self.container_name,
                "container_id": self.container_id,
                "candidate_container_id": self.candidate_container_id,
                "image": self.image,
                "image_id": (self.resource_inspection or {}).get("Image"),
                "volume_id": self.volume_id,
                "volume_owned": self.volume_owned,
                "ports": (self.resource_inspection or {}).get("Ports"),
                "mounts": (self.resource_inspection or {}).get("Mounts"),
                "existing_resources_touched": False,
            }
            report["teardown"] = teardown
            report["payload_staging_removed"] = staging_removed
            if teardown.get("verified") is not True:
                self.final_exit_code = 1
                self.failure = self.failure or "Run-owned resource teardown was not verified."
            report["server_version"] = self.server_version
            report["results"] = self.live_counts
            report["failure"] = self.failure
            report["finished_utc"] = utc_now()
            report["exit_code"] = self.final_exit_code
            (self.evidence_dir / "run.json").write_text(
                json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
        print(f"Evidence: {self.evidence_dir}")
        print(f"Exit code: {self.final_exit_code}")
        return self.final_exit_code

    def run_command(
        self,
        name: str,
        argv: Sequence[str],
        timeout: int,
        *,
        stdin_path: Path | None = None,
        binary_output: bool = False,
    ) -> CommandResult:
        return self.evidence.run(
            name,
            argv,
            cwd=self.root,
            timeout=timeout,
            stdin_path=stdin_path,
            binary_output=binary_output,
        )

    def source_identity(self) -> dict:
        # The smoke project references LiveCatalog, which references MySql and MariaDb.
        include_roots = [
            self.root / "src" / "Generators" / "Nextorm.Contexts.LiveCatalog",
            self.root / "src" / "Generators" / "Nextorm.Contexts.MySql",
            self.root / "src" / "Generators" / "Nextorm.Contexts.MariaDb",
            self.root / "tests" / "MySqlLiveViewSmoke",
        ]
        files: set[Path] = set()
        source_suffixes = {".cs", ".csproj", ".props", ".targets", ".py"}
        for base in include_roots:
            if not base.exists():
                continue
            for path in base.rglob("*"):
                if not path.is_file() or any(part in {"bin", "obj", "__pycache__"} for part in path.parts):
                    continue
                if path.suffix in source_suffixes:
                    files.add(path)
        for path in [
            self.root / "scripts" / "mysql_live_smoke.py",
            self.root / "Directory.Build.props",
            self.root / "Directory.Build.targets",
            self.root / "Directory.Packages.props",
            self.root / "global.json",
            self.root / "NuGet.Config",
        ]:
            if path.is_file():
                files.add(path)

        entries = [
            {
                "path": path.relative_to(self.root).as_posix(),
                "sha256": sha256_file(path),
            }
            for path in sorted(files)
        ]
        manifest = "".join(f"{item['sha256']}  {item['path']}\n" for item in entries)
        manifest_sha = hashlib.sha256(manifest.encode()).hexdigest()
        head_result = self.run_command(
            "git-head",
            ["git", "rev-parse", "--verify", "HEAD"],
            timeout=10,
        )
        status_result = self.run_command(
            "git-scope-status",
            [
                "git",
                "status",
                "--short",
                "--untracked-files=all",
                "--",
                "src/Generators/Nextorm.Contexts.LiveCatalog",
                "src/Generators/Nextorm.Contexts.MySql",
                "src/Generators/Nextorm.Contexts.MariaDb",
                "tests/MySqlLiveViewSmoke",
                "scripts/mysql_live_smoke.py",
            ],
            timeout=10,
        )
        return {
            "head": head_result.output.strip() if head_result.exit_code == 0 else None,
            "scoped_status": status_result.output.splitlines()
            if status_result.exit_code == 0
            else None,
            "files": entries,
            "manifest_sha256": manifest_sha,
        }

    def identity_summary(self, identity: dict) -> dict:
        return {
            "head": identity["head"],
            "manifest_sha256": identity["manifest_sha256"],
            "file_count": len(identity["files"]),
            "scoped_status": identity["scoped_status"],
        }

    def write_identity(self, name: str, identity: dict) -> None:
        (self.evidence_dir / name).write_text(
            json.dumps(identity, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        manifest = "".join(
            f"{item['sha256']}  {item['path']}\n" for item in identity["files"]
        )
        (self.evidence_dir / name.replace(".json", ".sha256")).write_text(
            manifest, encoding="utf-8"
        )

    def write_json(self, name: str, value: dict) -> None:
        (self.evidence_dir / name).write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )

    def build(self) -> dict:
        argv = [
            self.dotnet,
            "build",
            str(PROJECT),
            "--configuration",
            "Release",
            "--framework",
            "net10.0",
        ]
        result = self.run_command(
            "dotnet-build",
            argv,
            timeout=180,
        )
        warnings = re.search(r"(\d+)\s+Warning\(s\)", result.output)
        errors = re.search(r"(\d+)\s+Error\(s\)", result.output)
        return {
            "exit_code": result.exit_code,
            "warnings": int(warnings.group(1)) if warnings else None,
            "errors": int(errors.group(1)) if errors else None,
            "log": self.last_log_path(),
        }

    def last_log_path(self) -> str:
        return str(sorted(self.evidence.logs.glob("*.log"))[-1].relative_to(self.evidence_dir))

    def assembly_manifest(self) -> dict:
        output = self.root / OUTPUT
        required = [
            output / "MySqlLiveViewSmoke.dll",
            output / "Nextorm.Contexts.LiveCatalog.dll",
            output / "MySqlConnector.dll",
            output / "MySqlLiveViewSmoke.deps.json",
            output / "MySqlLiveViewSmoke.runtimeconfig.json",
        ]
        missing = [str(path.relative_to(self.root)) for path in required if not path.is_file()]
        if missing:
            raise RunFailure("Build output is missing required smoke assemblies.")
        return {"output_directory": str(OUTPUT), "files": self.assembly_files(output)}

    def assembly_files(self, directory: Path) -> list[dict]:
        return [
            {"path": path.name, "sha256": sha256_file(path), "bytes": path.stat().st_size}
            for path in sorted(directory.iterdir())
            if path.is_file() and path.suffix in {".dll", ".json"}
        ]

    def package_payload(self, built_manifest: dict) -> dict:
        output = self.root / OUTPUT
        dotnet_path = Path(shutil.which(self.dotnet) or self.dotnet).resolve()
        if not dotnet_path.is_file():
            raise RunFailure("The selected dotnet host could not be resolved.")
        runtime_root = dotnet_path.parent
        shared = runtime_root / "shared" / "Microsoft.NETCore.App"
        fxr_root = runtime_root / "host" / "fxr"
        runtime_dirs = sorted(
            (path for path in shared.glob("10.*") if path.is_dir()),
            key=lambda path: tuple(int(part) if part.isdigit() else 0 for part in path.name.split(".")),
        )
        fxr_dirs = sorted(
            (path for path in fxr_root.iterdir() if path.is_dir()),
            key=lambda path: tuple(int(part) if part.isdigit() else 0 for part in path.name.split(".")),
        ) if fxr_root.exists() else []
        if not runtime_dirs or not fxr_dirs:
            raise RunFailure("A .NET 10 shared runtime is required to execute the smoke in MySQL.")

        staging = self.evidence_dir / "staging"
        runtime_stage = staging / "runtime"
        app_stage = staging / "app"
        runtime_stage.mkdir(parents=True)
        app_stage.mkdir(parents=True)
        shutil.copy2(dotnet_path, runtime_stage / "dotnet")
        shutil.copytree(fxr_dirs[-1], runtime_stage / "host" / "fxr" / fxr_dirs[-1].name)
        shutil.copytree(
            runtime_dirs[-1],
            runtime_stage / "shared" / "Microsoft.NETCore.App" / runtime_dirs[-1].name,
        )
        for path in output.iterdir():
            if path.is_file():
                shutil.copy2(path, app_stage / path.name)
        staged_assemblies = self.assembly_files(app_stage)
        if staged_assemblies != built_manifest["files"]:
            raise RunFailure("Staged assemblies do not match the just-built output hashes.")

        archive = staging / "payload.tar"
        with tarfile.open(archive, "w") as tar:
            tar.add(runtime_stage, arcname="runtime")
            tar.add(app_stage, arcname="app")
        archived_assemblies = self.archive_assembly_files(archive)
        if archived_assemblies != staged_assemblies:
            raise RunFailure("Payload archive assembly hashes do not match the staged DLL bytes.")
        return {
            "archive": str(archive.relative_to(self.evidence_dir)),
            "sha256": sha256_file(archive),
            "bytes": archive.stat().st_size,
            "runtime_version": runtime_dirs[-1].name,
            "hostfxr_version": fxr_dirs[-1].name,
            "staged_assemblies": staged_assemblies,
            "archived_assemblies": archived_assemblies,
            "assembly_bytes_verified": True,
        }

    def archive_assembly_files(self, archive: Path) -> list[dict]:
        files = []
        with tarfile.open(archive, "r:") as tar:
            for member in tar.getmembers():
                if not member.name.startswith("app/") or Path(member.name).suffix not in {".dll", ".json"}:
                    continue
                if not member.isfile():
                    raise RunFailure("Payload archive contains a non-file assembly entry.")
                stream = tar.extractfile(member)
                if stream is None:
                    raise RunFailure("Could not read an assembly from the payload archive.")
                digest = hashlib.sha256()
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
                files.append(
                    {
                        "path": Path(member.name).name,
                        "sha256": digest.hexdigest(),
                        "bytes": member.size,
                    }
                )
        return sorted(files, key=lambda item: item["path"])

    def remove_staging(self) -> bool:
        staging = self.evidence_dir / "staging"
        try:
            if staging.exists():
                shutil.rmtree(staging)
            return not staging.exists()
        except OSError:
            return False

    def create_and_run_live_test(self) -> None:
        self.container_create_attempted = True
        create = self.run_command(
            "wslc-create",
            [
                self.wslc,
                "create",
                "--pull",
                "never",
                "--network",
                "bridge",
                "--name",
                self.container_name,
                "--env",
                "MYSQL_ALLOW_EMPTY_PASSWORD=1",
                self.image,
                "mysqld",
                "--bind-address=127.0.0.1",
                "--mysqlx-bind-address=127.0.0.1",
            ],
            timeout=30,
        )
        if create.exit_code != 0:
            self.ownership_failure = "wslc create failed; no resource ownership was established."
            raise RunFailure("wslc could not create the disposable container.", create.exit_code)
        # The retained successful wslc receipt is exactly one full ID on stdout.
        id_lines = [line.strip() for line in create.stdout.splitlines() if line.strip()]
        if len(id_lines) != 1 or not HEX_ID.fullmatch(id_lines[0]):
            self.ownership_failure = "Successful create did not return exactly one full container ID on stdout."
            raise RunFailure("wslc did not return the created container ID.")
        self.candidate_container_id = id_lines[0]

        inspect = self.run_command(
            "wslc-inspect-created",
            [
                self.wslc,
                "inspect",
                "--type",
                "container",
                "--format",
                "json",
                self.candidate_container_id,
            ],
            timeout=20,
        )
        if inspect.exit_code != 0:
            self.ownership_failure = "Created container inspection failed; ownership remains unresolved."
            raise RunFailure("Could not inspect the container created by this run.", inspect.exit_code)
        try:
            info = json.loads(inspect.stdout)
        except json.JSONDecodeError:
            self.ownership_failure = "Created container inspection was not valid JSON; ownership remains unresolved."
            raise RunFailure("Created container inspection was not valid JSON.") from None
        if not isinstance(info, dict):
            self.ownership_failure = "Created container inspection was not an object; ownership remains unresolved."
            raise RunFailure("Created container inspection was not a JSON object.")
        self.write_json("container-inspection.json", info)
        id_matches = info.get("Id") == self.candidate_container_id
        name_matches = info.get("Name") in {self.container_name, f"/{self.container_name}"}
        if not id_matches or not name_matches:
            self.ownership_failure = (
                "Inspection did not match both the exact create ID and this run's unique container name."
            )
            raise RunFailure("Container ownership could not be verified; no resource will be deleted.")

        self.container_id = self.candidate_container_id
        self.resource_inspection = info
        self.volume_id, self.volume_owned = self.find_run_volume(info)
        if not self.safe_container(info):
            raise RunFailure("Created container failed the no-port/no-host-mount safety check.")

        start = self.run_command(
            "wslc-start",
            [self.wslc, "start", self.container_id],
            timeout=30,
        )
        if start.exit_code != 0:
            raise RunFailure("Could not start the run-owned disposable container.", start.exit_code)

        self.transfer_payload()
        self.wait_until_ready()
        self.run_smoke()
        self.capture_generated_sources()

    def find_run_volume(self, info: dict) -> tuple[str | None, bool]:
        mounts = info.get("Mounts")
        if not isinstance(mounts, list) or len(mounts) != 1:
            return None, False
        mount = mounts[0]
        if not isinstance(mount, dict):
            return None, False
        name = mount.get("Name")
        if (
            mount.get("Destination") == "/var/lib/mysql"
            and mount.get("Type") == "volume"
            and isinstance(name, str)
            and HEX_ID.fullmatch(name)
            and mount.get("Source", "") == ""
            and mount.get("ReadWrite") is True
        ):
            return name, True
        return None, False

    def safe_container(self, info: dict) -> bool:
        image_id = info.get("Image")
        if not isinstance(image_id, str) or not image_id.startswith("sha256:"):
            return False
        ports = info.get("Ports")
        if not isinstance(ports, dict) or any(value for value in ports.values()):
            return False
        mounts = info.get("Mounts")
        if not isinstance(mounts, list) or not self.volume_owned:
            return False
        return True

    def transfer_payload(self) -> None:
        archive = self.evidence_dir / "staging" / "payload.tar"
        if not self.payload_sha256 or sha256_file(archive) != self.payload_sha256:
            raise RunFailure("Payload archive changed after assembly verification.")
        mkdir = self.run_command(
            "wslc-stage-directory",
            [self.wslc, "exec", self.container_id or "", "mkdir", "-m", "700", "-p", "--", self.remote_dir],
            timeout=15,
        )
        if mkdir.exit_code != 0:
            raise RunFailure("Could not create a private staging directory in the disposable container.", mkdir.exit_code)
        transfer = self.run_command(
            "wslc-transfer-payload",
            [
                self.wslc,
                "exec",
                "--interactive",
                self.container_id or "",
                "tar",
                "-xf",
                "-",
                "-C",
                self.remote_dir,
            ],
            timeout=120,
            stdin_path=archive,
        )
        if transfer.exit_code != 0:
            raise RunFailure("Could not transfer the build/runtime payload through wslc exec.", transfer.exit_code)
        mkdir_generated = self.run_command(
            "wslc-generated-directory",
            [
                self.wslc,
                "exec",
                self.container_id or "",
                "mkdir",
                "-m",
                "700",
                "-p",
                "--",
                f"{self.remote_dir}/generated",
            ],
            timeout=15,
        )
        if mkdir_generated.exit_code != 0:
            raise RunFailure("Could not create the generated-source output directory.", mkdir_generated.exit_code)

    def exec_dotnet(self, name: str, *app_args: str, timeout: int) -> CommandResult:
        return self.run_command(
            name,
            [
                self.wslc,
                "exec",
                "--env",
                "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1",
                "--env",
                "DOTNET_GCHeapHardLimit=0x80000000",
                "--env",
                "DOTNET_gcServer=0",
                self.container_id or "",
                f"{self.remote_dir}/runtime/dotnet",
                f"{self.remote_dir}/app/MySqlLiveViewSmoke.dll",
                *app_args,
            ],
            timeout=timeout,
        )

    def wait_until_ready(self) -> None:
        deadline = time.monotonic() + self.readiness_timeout
        last_output = ""
        attempt = 0
        while True:
            attempt += 1
            ready = self.exec_dotnet(
                f"wslc-readiness-{attempt:03d}",
                "--ready",
                SOCKET_PATH,
                timeout=8,
            )
            last_output = ready.output
            match = READY.search(ready.output)
            if ready.exit_code == 0 and match:
                version, skip_networking = match.groups()
                if version != "8.4.11" or skip_networking != "0":
                    raise RunFailure("The final server did not match MySQL 8.4.11 with networking enabled.")
                self.server_version = version
                return
            if time.monotonic() >= deadline:
                raise RunFailure(
                    f"MySqlConnector readiness timed out after {self.readiness_timeout}s; "
                    f"last client result: {last_output.strip() or 'no diagnostic'}."
                )
            time.sleep(self.readiness_interval)

    def run_smoke(self) -> None:
        output_path = f"{self.remote_dir}/generated/View.cs"
        result = self.exec_dotnet("wslc-live-smoke", SOCKET_PATH, output_path, timeout=120)
        match = SUMMARY.search(result.output)
        if match:
            self.live_counts = {
                "selected": int(match.group(1)),
                "completed": int(match.group(2)),
                "failed": int(match.group(3)),
                "skipped": int(match.group(4)),
            }
        if result.exit_code != 0:
            raise RunFailure("The live MySQL smoke harness failed.", result.exit_code)
        if self.live_counts != {"selected": 3, "completed": 3, "failed": 0, "skipped": 0}:
            raise RunFailure("The live smoke did not report the required 3/3 scenarios.")

    def capture_generated_sources(self) -> None:
        archive_path = self.evidence_dir / "generated-sources.tar"
        captured = self.run_command(
            "wslc-capture-generated-sources",
            [
                self.wslc,
                "exec",
                self.container_id or "",
                "tar",
                "-cf",
                "-",
                "-C",
                self.remote_dir,
                "generated/View.cs",
                "Table.cs",
                "generated/Keyless.cs",
            ],
            timeout=20,
            binary_output=True,
        )
        if captured.exit_code != 0 or captured.binary_output is None:
            raise RunFailure("Could not capture generated C# sources.", captured.exit_code)
        archive_path.write_bytes(captured.binary_output)
        generated = self.evidence_dir / "generated"
        generated.mkdir()
        expected = {
            "generated/View.cs": "View.cs",
            "Table.cs": "Table.cs",
            "generated/Keyless.cs": "Keyless.cs",
        }
        with tarfile.open(archive_path, "r:") as tar:
            members = tar.getmembers()
            if any(member.name not in expected or not member.isfile() or member.size > 1024 * 1024 for member in members):
                raise RunFailure("Generated-source archive contained unexpected entries.")
            if {member.name for member in members} != set(expected):
                raise RunFailure("Generated-source archive was incomplete.")
            for member in members:
                stream = tar.extractfile(member)
                if stream is None:
                    raise RunFailure("Generated-source archive could not be read.")
                destination = generated / expected[member.name]
                destination.write_bytes(stream.read())
        manifest = {
            path.name: {"sha256": sha256_file(path), "bytes": path.stat().st_size}
            for path in sorted(generated.iterdir())
        }
        self.write_json("generated-sources.json", manifest)

    def teardown(self) -> dict:
        if not self.container_id:
            return {
                "attempted": False,
                "verified": not self.container_create_attempted,
                "status": (
                    "not created"
                    if not self.container_create_attempted
                    else "ownership unresolved; no deletion attempted"
                ),
                "candidate_container_id": self.candidate_container_id,
                "ownership_failure": self.ownership_failure,
                "volume": "not created" if not self.container_create_attempted else "ownership unresolved",
            }

        remove_args = [self.wslc, "remove", "--force"]
        if self.volume_owned:
            remove_args.append("--volumes")
        remove_args.append(self.container_id)
        remove = self.run_command("wslc-remove-owned-container", remove_args, timeout=30)
        container_check = self.run_command(
            "wslc-verify-container-removed",
            [self.wslc, "inspect", "--type", "container", self.container_id],
            timeout=15,
        )
        container_gone = container_check.exit_code == 1 and re.search(
            r"not found|not exist", container_check.output, re.IGNORECASE
        ) is not None
        volume_gone: bool | None = None
        volume_check_exit: int | None = None
        mounts = (self.resource_inspection or {}).get("Mounts")
        if self.volume_id and self.volume_owned:
            volume_check = self.run_command(
                "wslc-verify-owned-volume-removed",
                [self.wslc, "inspect", "--type", "volume", self.volume_id],
                timeout=15,
            )
            volume_check_exit = volume_check.exit_code
            volume_gone = volume_check.exit_code == 1 and re.search(
                r"not found|not exist", volume_check.output, re.IGNORECASE
            ) is not None
        return {
            "attempted": True,
            "remove_exit_code": remove.exit_code,
            "container_inspect_exit_code": container_check.exit_code,
            "container_verified_absent": container_gone,
            "volume_inspect_exit_code": volume_check_exit,
            "volume_verified_absent": volume_gone,
            "volume_left_untouched": not self.volume_owned
            and (not isinstance(mounts, list) or bool(mounts)),
            "verified": bool(
                remove.exit_code == 0
                and container_gone
                and (
                    volume_gone is True
                    if self.volume_owned
                    else isinstance(mounts, list) and not mounts
                )
            ),
        }


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build and run the existing live MySQL smoke on a new disposable wslc instance."
    )
    parser.add_argument(
        "--create-disposable",
        action="store_true",
        help="explicitly opt in to creating one new container from the already-present image",
    )
    parser.add_argument("--wslc", default=DEFAULT_WSLC)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--readiness-timeout", type=int, default=120)
    parser.add_argument("--evidence-dir", type=Path)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    options = parser.parse_args(argv)
    if not options.create_disposable:
        parser.error("No live resource is created by default; pass --create-disposable after authorization.")
    if options.readiness_timeout < 1:
        parser.error("--readiness-timeout must be positive.")
    return options


def main(argv: Sequence[str] | None = None) -> int:
    options = parse_args(argv)
    root = options.repo_root.resolve()
    if options.evidence_dir:
        evidence_dir = options.evidence_dir.resolve()
    else:
        run_id = uuid.uuid4().hex
        evidence_dir = root / "artifacts" / "mysql-live-smoke" / f"{datetime.now(timezone.utc):%Y%m%dT%H%M%SZ}-{run_id[:8]}"
    try:
        evidence_dir.relative_to(root)
    except ValueError:
        print("Evidence must be written inside the selected repository.", file=sys.stderr)
        return 2
    if evidence_dir.exists() and any(evidence_dir.iterdir()):
        print("Evidence directory must be new or empty.", file=sys.stderr)
        return 2
    evidence_dir.mkdir(parents=True, exist_ok=True)
    runner = LiveSmokeRunner(
        root,
        evidence_dir,
        dotnet=options.dotnet,
        wslc=options.wslc,
        readiness_timeout=options.readiness_timeout,
    )
    return runner.run()


if __name__ == "__main__":
    raise SystemExit(main())
