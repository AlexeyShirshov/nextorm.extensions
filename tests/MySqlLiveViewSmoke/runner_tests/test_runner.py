from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[3]
RUNNER = PROJECT_ROOT / "scripts" / "mysql_live_smoke.py"
CONTAINER_ID = "a" * 64
VOLUME_ID = "b" * 64


class MySqlLiveRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="nextorm-mysql-runner-test-")
        self.temp_path = Path(self.temp.name)
        self.root = self.temp_path / "repo"
        self.root.mkdir()
        (self.root / "scripts").mkdir()
        shutil.copy2(RUNNER, self.root / "scripts" / RUNNER.name)
        smoke = self.root / "tests" / "MySqlLiveViewSmoke"
        smoke.mkdir(parents=True)
        (smoke / "MySqlLiveViewSmoke.csproj").write_text(
            '<Project><ProjectReference Include="../../src/Generators/Nextorm.Contexts.LiveCatalog/Nextorm.Contexts.LiveCatalog.csproj" /></Project>\n',
            encoding="utf-8",
        )
        (smoke / "Program.cs").write_text("// stable smoke source\n", encoding="utf-8")
        live = self.root / "src" / "Generators" / "Nextorm.Contexts.LiveCatalog"
        live.mkdir(parents=True)
        (live / "Nextorm.Contexts.LiveCatalog.csproj").write_text(
            '<Project><ProjectReference Include="../Nextorm.Contexts.MySql/Nextorm.Contexts.MySql.csproj" /><ProjectReference Include="../Nextorm.Contexts.MariaDb/Nextorm.Contexts.MariaDb.csproj" /></Project>\n',
            encoding="utf-8",
        )
        (live / "LiveCatalogGenerator.cs").write_text("// candidate generator source\n", encoding="utf-8")
        for name in ("Nextorm.Contexts.MySql", "Nextorm.Contexts.MariaDb"):
            dependency = self.root / "src" / "Generators" / name
            dependency.mkdir()
            (dependency / f"{name}.csproj").write_text("<Project />\n", encoding="utf-8")
            (dependency / "Dependency.cs").write_text("// transitive build input\n", encoding="utf-8")
        self.output = smoke / "bin" / "Release" / "net10.0"
        self.runtime = self.temp_path / "dotnet-root"
        (self.runtime / "host" / "fxr" / "10.0.0").mkdir(parents=True)
        (self.runtime / "host" / "fxr" / "10.0.0" / "libhostfxr.so").write_bytes(b"fake-hostfxr")
        (self.runtime / "shared" / "Microsoft.NETCore.App" / "10.0.0").mkdir(parents=True)
        (self.runtime / "shared" / "Microsoft.NETCore.App" / "10.0.0" / "System.Private.CoreLib.dll").write_bytes(
            b"fake-runtime"
        )
        dotnet = self.runtime / "dotnet"
        dotnet.write_text(
            "#!/usr/bin/env python3\n"
            "import pathlib, sys\n"
            "args = sys.argv[1:]\n"
            "if args and args[0] == 'build':\n"
            "    out = pathlib.Path.cwd() / 'tests/MySqlLiveViewSmoke/bin/Release/net10.0'\n"
            "    out.mkdir(parents=True, exist_ok=True)\n"
            "    for name in ['MySqlLiveViewSmoke.dll', 'Nextorm.Contexts.LiveCatalog.dll', 'MySqlConnector.dll']:\n"
            "        (out / name).write_bytes(('assembly:' + name).encode())\n"
            "    (out / 'MySqlLiveViewSmoke.deps.json').write_text('{}')\n"
            "    (out / 'MySqlLiveViewSmoke.runtimeconfig.json').write_text('{\\\"runtimeOptions\\\":{\\\"framework\\\":{\\\"name\\\":\\\"Microsoft.NETCore.App\\\",\\\"version\\\":\\\"10.0.0\\\"}}}')\n"
            "    print('Build succeeded.\\n    0 Warning(s)\\n    0 Error(s)')\n"
            "    raise SystemExit(0)\n"
            "print('fake dotnet')\n",
            encoding="utf-8",
        )
        dotnet.chmod(0o755)
        self.dotnet = dotnet
        self.state_path = self.temp_path / "wslc-state.json"
        self.wslc = self.temp_path / "wslc"
        self.wslc.write_text(
            "#!/usr/bin/env python3\n"
            "import io, json, os, pathlib, re, sys, tarfile\n"
            f"state_path = pathlib.Path(os.environ['FAKE_WSLC_STATE'])\n"
            "state = json.loads(state_path.read_text()) if state_path.exists() else {'calls': [], 'removed': False}\n"
            "args = sys.argv[1:]\n"
            "state['calls'].append(args)\n"
            "state_path.write_text(json.dumps(state))\n"
            "mode = os.environ.get('FAKE_WSLC_MODE', 'success')\n"
            "if args[0] == 'create':\n"
            "    if mode == 'create-fail-id':\n"
            "        print('" + "e" * 64 + "')\n"
            "        print('" + "f" * 64 + "', file=sys.stderr)\n"
            "        raise SystemExit(1)\n"
            f"    print('{CONTAINER_ID}')\n"
            "    raise SystemExit(0)\n"
            "if args[0] == 'inspect' and '--type' in args:\n"
            "    kind = args[args.index('--type') + 1]\n"
            "    if state.get('removed'):\n"
            "        print('Not found', file=sys.stderr); raise SystemExit(1)\n"
            "    if kind == 'container':\n"
            "        ports = {'3306/tcp': [{'HostIp': '0.0.0.0', 'HostPort': '3306'}]} if mode == 'unsafe-port' else {}\n"
            "        name = state['calls'][0][state['calls'][0].index('--name') + 1]\n"
            "        inspected_id = ('d' * 64) if mode == 'inspect-id-mismatch' else '" + CONTAINER_ID + "'\n"
            "        inspected_name = '/foreign-container' if mode == 'inspect-name-mismatch' else '/' + name\n"
            "        print(json.dumps({'Id': inspected_id, 'Image': 'sha256:' + 'c' * 64, 'Name': inspected_name, 'Ports': ports, 'Mounts': [{'Destination': '/var/lib/mysql', 'Name': '" + VOLUME_ID + "', 'ReadWrite': True, 'Source': '', 'Type': 'volume'}]}))\n"
            "        raise SystemExit(0)\n"
            "    if kind == 'volume':\n"
            "        print('volume still exists'); raise SystemExit(0)\n"
            "if args[0] == 'start':\n"
            "    raise SystemExit(0)\n"
            "if args[0] == 'exec':\n"
            "    joined = ' '.join(args)\n"
            "    if '--interactive' in args:\n"
            "        sys.stdin.buffer.read(); raise SystemExit(0)\n"
            "    if '--ready' in args:\n"
            "        print('READY mysql_version=8.4.11 skip_networking=0'); raise SystemExit(0)\n"
            "    if 'MySqlLiveViewSmoke.dll' in joined:\n"
            "        if mode == 'smoke-fail':\n"
            "            print('password=never-log-this', file=sys.stderr)\n"
            "            print('Selected: 3; completed: 2; failed: 1; skipped: 0.'); raise SystemExit(1)\n"
            "        print('Case 1 complete: live MySQL view kind, projected columns, no key.')\n"
            "        print('Case 2 complete: live MySQL base table retains declared key.')\n"
            "        print('Case 3 complete: live MySQL base table without a primary key retains its columns.')\n"
            "        print('Selected: 3; completed: 3; failed: 0; skipped: 0.')\n"
            "        if mode == 'mutate-source': pathlib.Path(os.environ['FAKE_SOURCE_FILE']).write_text('// changed during run\\\\n')\n"
            "        raise SystemExit(0)\n"
            "    if 'tar -cf -' in joined or ('tar' in args and '-cf' in args):\n"
            "        output = io.BytesIO()\n"
            "        with tarfile.open(fileobj=output, mode='w') as tar:\n"
            "            for name in ['generated/View.cs', 'Table.cs', 'generated/Keyless.cs']:\n"
            "                data = b'// generated source\\\\n'\n"
            "                info = tarfile.TarInfo(name); info.size = len(data); tar.addfile(info, io.BytesIO(data))\n"
            "        sys.stdout.buffer.write(output.getvalue()); raise SystemExit(0)\n"
            "    raise SystemExit(0)\n"
            "if args[0] == 'remove':\n"
            "    state['removed'] = True; state_path.write_text(json.dumps(state)); raise SystemExit(0)\n"
            "print('unknown fake wslc command', file=sys.stderr); raise SystemExit(2)\n",
            encoding="utf-8",
        )
        self.wslc.chmod(0o755)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_runner(self, mode: str, name: str) -> tuple[subprocess.CompletedProcess[str], Path, dict]:
        evidence = self.root / "artifacts" / name
        env = os.environ.copy()
        env["FAKE_WSLC_STATE"] = str(self.state_path)
        env["FAKE_WSLC_MODE"] = mode
        if mode == "mutate-source":
            env["FAKE_SOURCE_FILE"] = str(self.root / "tests" / "MySqlLiveViewSmoke" / "Program.cs")
        command = [
            sys.executable,
            str(self.root / "scripts" / RUNNER.name),
            "--create-disposable",
            "--repo-root",
            str(self.root),
            "--evidence-dir",
            str(evidence),
            "--dotnet",
            str(self.dotnet),
            "--wslc",
            str(self.wslc),
            "--readiness-timeout",
            "1",
        ]
        result = subprocess.run(command, text=True, capture_output=True, env=env, timeout=30)
        state = json.loads(self.state_path.read_text())
        return result, evidence, state

    def test_success_records_provenance_readiness_results_and_owned_teardown(self) -> None:
        result, evidence, state = self.run_runner("success", "success")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        report = json.loads((evidence / "run.json").read_text())
        self.assertEqual(report["exit_code"], 0)
        self.assertEqual(report["results"], {"selected": 3, "completed": 3, "failed": 0, "skipped": 0})
        self.assertEqual(report["server_version"], "8.4.11")
        self.assertTrue(report["ownership"]["created_by_this_run"])
        self.assertEqual(report["ownership"]["ports"], {})
        self.assertEqual(report["ownership"]["mounts"][0]["Type"], "volume")
        self.assertTrue(report["ownership"]["mounts"][0]["ReadWrite"])
        self.assertEqual(report["source_identity_before"]["manifest_sha256"], report["source_identity_after"]["manifest_sha256"])
        self.assertTrue(any(item["path"] == "tests/MySqlLiveViewSmoke/Program.cs" for item in json.loads((evidence / "sources-before.json").read_text())["files"]))
        source_paths = {item["path"] for item in json.loads((evidence / "sources-before.json").read_text())["files"]}
        self.assertIn("src/Generators/Nextorm.Contexts.MySql/Dependency.cs", source_paths)
        self.assertIn("src/Generators/Nextorm.Contexts.MariaDb/Dependency.cs", source_paths)
        self.assertIn("Nextorm.Contexts.LiveCatalog.dll", [item["path"] for item in report["assemblies"]["files"]])
        self.assertEqual(report["assemblies"]["files"], report["payload"]["staged_assemblies"])
        self.assertEqual(report["payload"]["staged_assemblies"], report["payload"]["archived_assemblies"])
        self.assertTrue(report["payload"]["assembly_bytes_verified"])
        self.assertTrue(report["teardown"]["verified"])
        self.assertTrue((evidence / "generated" / "View.cs").is_file())
        calls = [record for record in map(json.loads, (evidence / "commands.jsonl").read_text().splitlines())]
        args = [arg for record in calls for arg in record["argv"]]
        self.assertIn("--pull", args)
        self.assertIn("never", args)
        self.assertNotIn("--publish", args)
        self.assertNotIn("NuGetAudit=false", args)
        self.assertNotIn("mysql-wsl-test", " ".join(args))
        self.assertTrue(any("--ready" in record["argv"] for record in calls))
        self.assertTrue(any(record["name"] == "wslc-live-smoke" and record["exit_code"] == 0 for record in calls))
        transfer = next(record for record in calls if record["name"] == "wslc-transfer-payload")
        self.assertEqual(transfer["stdin_sha256"], report["payload"]["sha256"])
        self.assertTrue(any(record["name"] == "wslc-remove-owned-container" and "--volumes" in record["argv"] for record in calls))
        self.assertTrue(state["removed"])
        self.assertNotIn("password=secret", (evidence / "commands.jsonl").read_text().lower())

    def test_smoke_failure_still_removes_only_the_owned_resources(self) -> None:
        result, evidence, state = self.run_runner("smoke-fail", "failure")
        self.assertNotEqual(result.returncode, 0)
        report = json.loads((evidence / "run.json").read_text())
        self.assertEqual(report["results"], {"selected": 3, "completed": 2, "failed": 1, "skipped": 0})
        self.assertEqual(report["source_identity_before"]["manifest_sha256"], report["source_identity_after"]["manifest_sha256"])
        self.assertEqual(report["teardown"]["remove_exit_code"], 0)
        self.assertTrue(report["teardown"]["container_verified_absent"])
        self.assertTrue(report["teardown"]["volume_verified_absent"])
        logs = "\n".join(path.read_text() for path in (evidence / "logs").glob("*.log"))
        self.assertNotIn("never-log-this", logs)
        self.assertIn("password=[REDACTED]", logs)
        remove = next(
            json.loads(line)
            for line in (evidence / "commands.jsonl").read_text().splitlines()
            if '"wslc-remove-owned-container"' in line
        )
        self.assertEqual(remove["argv"][-1], CONTAINER_ID)
        self.assertTrue(state["removed"])

    def test_unexpected_published_port_is_never_started_and_is_cleaned(self) -> None:
        result, evidence, state = self.run_runner("unsafe-port", "unsafe")
        self.assertNotEqual(result.returncode, 0)
        report = json.loads((evidence / "run.json").read_text())
        self.assertIn("no-port/no-host-mount", report["failure"])
        calls = state["calls"]
        self.assertFalse(any(call[0] == "start" for call in calls))
        self.assertFalse(any(call[0] == "exec" for call in calls))
        self.assertTrue(report["teardown"]["verified"])
        self.assertTrue(state["removed"])

    def test_create_failure_with_hex_ids_never_claims_or_deletes_a_container(self) -> None:
        result, evidence, state = self.run_runner("create-fail-id", "create-fail")
        self.assertNotEqual(result.returncode, 0)
        report = json.loads((evidence / "run.json").read_text())
        self.assertFalse(report["ownership"]["identity_verified"])
        self.assertIsNone(report["ownership"]["container_id"])
        self.assertIsNone(report["ownership"]["candidate_container_id"])
        self.assertFalse(report["teardown"]["verified"])
        calls = state["calls"]
        self.assertFalse(any(call[0] == "inspect" for call in calls))
        self.assertFalse(any(call[0] == "start" for call in calls))
        self.assertFalse(any(call[0] == "remove" for call in calls))

    def test_inspect_name_mismatch_never_starts_or_deletes(self) -> None:
        self.assert_unverified_inspection_is_not_deleted("inspect-name-mismatch", "name-mismatch")

    def test_inspect_id_mismatch_never_starts_or_deletes(self) -> None:
        self.assert_unverified_inspection_is_not_deleted("inspect-id-mismatch", "id-mismatch")

    def assert_unverified_inspection_is_not_deleted(self, mode: str, name: str) -> None:
        result, evidence, state = self.run_runner(mode, name)
        self.assertNotEqual(result.returncode, 0)
        report = json.loads((evidence / "run.json").read_text())
        self.assertFalse(report["ownership"]["identity_verified"])
        self.assertEqual(report["ownership"]["candidate_container_id"], CONTAINER_ID)
        self.assertIsNone(report["ownership"]["container_id"])
        self.assertIsNone(report["ownership"]["volume_id"])
        self.assertFalse(report["teardown"]["verified"])
        calls = state["calls"]
        self.assertEqual([call[0] for call in calls if call[0] in {"start", "remove"}], [])

    def test_source_change_during_run_invalidates_result_and_still_cleans_up(self) -> None:
        result, evidence, state = self.run_runner("mutate-source", "mutated")
        self.assertNotEqual(result.returncode, 0)
        report = json.loads((evidence / "run.json").read_text())
        self.assertIn("Source identity changed", report["failure"])
        self.assertNotEqual(
            report["source_identity_before"]["manifest_sha256"],
            report["source_identity_after"]["manifest_sha256"],
        )
        self.assertEqual(report["results"], {"selected": 3, "completed": 3, "failed": 0, "skipped": 0})
        self.assertTrue(report["teardown"]["verified"])
        self.assertTrue(state["removed"])

    def test_live_creation_requires_explicit_opt_in(self) -> None:
        evidence = self.root / "artifacts" / "no-opt-in"
        result = subprocess.run(
            [
                sys.executable,
                str(self.root / "scripts" / RUNNER.name),
                "--repo-root",
                str(self.root),
                "--evidence-dir",
                str(evidence),
            ],
            text=True,
            capture_output=True,
            timeout=10,
        )
        self.assertEqual(result.returncode, 2)
        self.assertFalse(self.state_path.exists())
        self.assertFalse(evidence.exists())


if __name__ == "__main__":
    unittest.main()
