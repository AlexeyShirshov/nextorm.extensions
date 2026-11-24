#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
    echo "Usage: bash scripts/generate-orca-context.sh PROFILE_DB AGENT_SESSION_DB SESSION_SEARCH_DB" >&2
    exit 1
fi

for database in "$@"; do
    if [[ ! -f "$database" ]]; then
        echo "Database file not found: $database" >&2
        exit 1
    fi
done

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
tool="$root/src/Tools/Nextorm.Scaffold/Nextorm.Scaffold.csproj"
package="$root/src/RepositoryContext/Nextorm.Extensions.Orca"

scaffold() {
    local database="$1" area="$2" context="$3"
    shift 3
    dotnet run --project "$tool" -p:PublishAot=false -- scaffold \
        --provider sqlite --database "$database" \
        --namespace "Nextorm.Extensions.Orca.$area" --context "$context" \
        --output-dir "$package/$area" --force "$@"
}

scaffold "$1" Profile OrcaProfileSqliteRepositoryContext
scaffold "$2" AgentSessions OrcaAgentSessionsSqliteRepositoryContext
# The FTS virtual table has untyped columns; its shadow tables are internal.
scaffold "$3" SessionSearch OrcaSessionSearchSqliteRepositoryContext \
    --table files --table messages --table meta --table sessions
