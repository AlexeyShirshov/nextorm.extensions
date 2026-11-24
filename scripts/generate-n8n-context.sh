#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
    echo "Usage: bash scripts/generate-n8n-context.sh DATABASE_PATH" >&2
    exit 1
fi

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
interceptor="$root/scripts/N8nScaffold"
dotnet build "$interceptor/N8nScaffold.csproj" -c Release
dotnet run --project "$root/src/Tools/Nextorm.Scaffold/Nextorm.Scaffold.csproj" \
    -p:PublishAot=false -- scaffold \
    --provider sqlite --database "$1" \
    --customize "$interceptor/bin/Release/net11.0/N8nScaffold.dll" \
    --namespace Nextorm.Extensions.N8n --context N8nSqliteRepositoryContext \
    --output-dir "$root/src/RepositoryContext/Nextorm.Extensions.N8n" --force
