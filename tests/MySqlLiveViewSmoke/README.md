# Live MySQL smoke

`Program.cs` is the existing live catalog regression harness. It checks a view, a
single-key base table, and a keyless base table. Its `--ready <unix-socket>`
mode uses the same MySqlConnector client and socket path. It reads the server
version from the connection and checks `skip_networking` through NextORM
without creating fixtures.

For a live run, from the repository root in WSL, set `TASK_SLUG` and `REVISION`
to the active PDCA task and revision. The example gives each scoped CHECK a
fresh evidence directory:

```sh
TASK_SLUG=universal-database-schema-generator
REVISION=rv2
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)-$(python3 -c 'import uuid; print(uuid.uuid4().hex[:8])')"
python3 scripts/mysql_live_smoke.py \
  --create-disposable \
  --evidence-dir ".pdca/$TASK_SLUG/$REVISION/attempts/mysql-live-smoke-$RUN_ID"
```

The default `artifacts/mysql-live-smoke/` output is ignored by Git. Use the
explicit PDCA path above when you need evidence for a scoped CHECK.

Before you run it, install Python 3 and the .NET 10 SDK, verify the `wslc`
executable path, and confirm that `mysql:8.4.11` is already available locally.
The runner uses the standard `dotnet build` restore behavior and does not pull
container images.

The command explicitly opts into creating one uniquely named container from
the already-present `mysql:8.4.11` image. It uses `wslc create --pull never`,
private bridge networking with no published ports, no host mounts, and the
container's Unix socket. It refuses to use an existing endpoint or container.
The runner checks the created container's identity, ports, and volume before
starting it, waits for the final MySQL 8.4.11 server through the smoke
assembly's MySqlConnector readiness mode, and only then runs the unchanged
three-scenario smoke. It captures source and assembly hashes, each command's
arguments and exit code, logs, selected/result counts, generated C# files, and
verified cleanup receipts under `artifacts/mysql-live-smoke/`.

The runner never pulls an image, publishes a port, mounts a host path, reads
application rows, accepts a connection string, or logs credentials. It removes
only the exact container it created and verifies its own anonymous volume is
gone. If inspection does not prove that a volume is this run's anonymous
MySQL data volume, it removes only its container and leaves the unknown volume
untouched. Do not use this runner unless creation of a fresh disposable MySQL
instance is explicitly authorized.

If `wslc create` fails or inspection cannot match both its exact returned ID
and this run's unique name, the runner records unresolved ownership and deletes
nothing. Review `run.json` and the command logs before taking any cleanup action.

After the command exits, check `run.json` for `exit_code: 0`, three selected and
completed scenarios, no failures or skips, and `teardown.verified: true`.
`commands.jsonl` and the referenced files in `logs/` contain the exact commands,
exit codes, and output for each step.

The offline orchestration tests use fake `dotnet` and `wslc` executables:

```sh
python3 -m unittest discover -s tests/MySqlLiveViewSmoke/runner_tests -p 'test_*.py'
```
