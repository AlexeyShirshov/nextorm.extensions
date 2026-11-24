#!/usr/bin/env bash
set -euo pipefail

package="${1:?Usage: bash scripts/scaffold-upstream.sh PACKAGE VERSION_FILE}"
version_file="${2:?Usage: bash scripts/scaffold-upstream.sh PACKAGE VERSION_FILE}"
project_dir="src/RepositoryContext/Nextorm.Extensions.$package"
scratch="$(mktemp -d)"
container=
redis_container=
server_container=
server_pid=
cleanup() {
    local id
    if [[ -n "$server_pid" ]]; then
        kill "$server_pid" 2>/dev/null || true
        wait "$server_pid" 2>/dev/null || true
    fi
    for id in "$server_container" "$redis_container" "$container"; do
        if [[ -n "$id" ]]; then
            docker rm -f -v "$id" >/dev/null || true
        fi
    done
    rm -rf -- "$scratch"
}
trap cleanup EXIT

download() {
    curl --fail --silent --show-error --location --retry 3 --max-time 180 "$@"
}

latest_release() {
    local release
    release="$(download "https://api.github.com/repos/$1/releases/latest")"
    jq -e 'select(.draft == false and .prerelease == false)' <<< "$release"
}

latest_opencode_v2() {
    local tags candidate code
    tags="$(download "https://api.github.com/repos/$upstream/git/matching-refs/tags/v2." |
        jq -er '.[].ref | select(test("^refs/tags/v2[.][0-9]+[.][0-9]+$")) | sub("^refs/tags/"; "")' |
        LC_ALL=C sort -Vr)"
    while IFS= read -r candidate; do
        code="$(download "https://api.github.com/repos/$upstream/releases/tags/$candidate" \
            -w '%{http_code}' -o "$scratch/release.json" || true)"
        if [[ "$code" == 404 ]]; then
            continue
        fi
        [[ "$code" == 200 ]] || return 1
        if release="$(jq -e 'select(.draft == false and .prerelease == false)' "$scratch/release.json")"; then
            tag="$candidate"
            return
        fi
    done <<< "$tags"
    echo "No stable published V2 release found for $upstream." >&2
    return 1
}

download_asset() {
    local asset url digest
    asset="$(jq -ec --arg name "$1" '.assets[] | select(.name == $name)' <<< "$release")"
    url="$(jq -er '.browser_download_url' <<< "$asset")"
    digest="$(jq -er '.digest | select(test("^sha256:[0-9a-f]{64}$"))' <<< "$asset")"
    download "$url" -o "$2"
    printf '%s  %s\n' "${digest#sha256:}" "$2" | sha256sum --check --status
}

start_local_server() {
    local attempt
    mkdir -p "$scratch/home"
    (
        cd "$scratch"
        export HOME="$scratch/home" XDG_DATA_HOME="$scratch/data" XDG_CONFIG_HOME="$scratch/config"
        export XDG_STATE_HOME="$scratch/state" XDG_CACHE_HOME="$scratch/cache"
        exec "$@"
    ) > "$scratch/server.log" 2>&1 &
    server_pid=$!
    for ((attempt=0; attempt<120; attempt++)); do
        if grep -qiE 'server listening|listening on|server ready' "$scratch/server.log"; then
            return
        fi
        if ! kill -0 "$server_pid" 2>/dev/null; then
            break
        fi
        sleep 1
    done
    echo "$package did not finish starting in its isolated home directory." >&2
    return 1
}

stop_local_server() {
    kill "$server_pid"
    wait "$server_pid" || true
    server_pid=
}

wait_for_http() {
    local url="$1" service="${2:-$container}" attempt
    for ((attempt=0; attempt<120; attempt++)); do
        if curl --fail --silent --max-time 3 "$url" >/dev/null; then
            return
        fi
        if [[ "$(docker inspect --format '{{.State.Running}}' "$service")" != true ]]; then
            break
        fi
        sleep 2
    done
    docker logs --tail 80 "$service" >&2
    echo "Service did not become ready: $url" >&2
    return 1
}

sqlite_from_image() {
    local image="$1" port="$2" health_path="$3" database="$4"
    shift 4
    container="$(docker run --detach --publish "127.0.0.1::$port" "$@" "$image")"
    local address
    address="$(docker port "$container" "$port/tcp")"
    wait_for_http "http://$address$health_path"
    docker stop --time 30 "$container" >/dev/null
    docker cp "$container:$database" "$scratch/schema.sqlite"
    # Preserve committed pages even if the application did not checkpoint on shutdown.
    docker cp "$container:$database-wal" "$scratch/schema.sqlite-wal" 2>/dev/null || true
    [[ "$(sqlite3 -readonly "$scratch/schema.sqlite" 'PRAGMA quick_check;')" == ok ]]
}

start_postgres() {
    local database="$1" image="${2:-postgres:17-alpine}" attempt
    container="$(docker run --detach --publish 127.0.0.1::5432 \
        "${@:3}" -e "POSTGRES_DB=$database" -e POSTGRES_PASSWORD=nextorm-ci "$image")"
    for ((attempt=0; attempt<60; attempt++)); do
        if docker exec "$container" pg_isready -h 127.0.0.1 -U postgres -d "$database"; then
            address="$(docker port "$container" 5432/tcp)"
            return
        fi
        sleep 1
    done
    docker logs --tail 80 "$container" >&2
    return 1
}

case "$package" in
    AnythingLLM) upstream=mintplex-labs/anything-llm ;;
    Dify) upstream=langgenius/dify ;;
    Ghost) upstream=TryGhost/Ghost ;;
    Gitea|Gitea.PostgreSql) upstream=go-gitea/gitea ;;
    Grafana) upstream=grafana/grafana ;;
    Immich) upstream=immich-app/immich ;;
    Jellyfin) upstream=jellyfin/jellyfin ;;
    LiteLLM) upstream=BerriAI/litellm ;;
    Mastodon) upstream=mastodon/mastodon ;;
    N8n) upstream=n8n-io/n8n ;;
    OpenCode) upstream=anomalyco/opencode ;;
    OpenWebUI) upstream=open-webui/open-webui ;;
    Redmine) upstream=redmine/redmine ;;
    T3Code) upstream=pingdotgg/t3code ;;
    WordPress) upstream=WordPress/WordPress ;;
    *) echo "No upstream schema recipe for $package" >&2; exit 1 ;;
esac

if [[ -n "$(git status --porcelain -- "$project_dir")" ]]; then
    echo "Start with a clean checkout of $project_dir." >&2
    exit 1
fi

case "$package" in
    WordPress)
        tag="$(download 'https://api.wordpress.org/core/version-check/1.7/' |
            jq -er 'first(.offers[] | select(.locale == "en_US")).current')"
        ;;
    Redmine)
        tag="$(download 'https://www.redmine.org/releases/' |
            grep -Eo 'redmine-[0-9]+\.[0-9]+\.[0-9]+\.tar\.gz' |
            sed -E 's@redmine-(.*)\.tar\.gz@\1@' | LC_ALL=C sort -V | tail -n 1)"
        ;;
    OpenCode)
        # This package maps V2. GitHub's general latest release can point at V1.
        latest_opencode_v2
        ;;
    *)
        release="$(latest_release "$upstream")"
        tag="$(jq -er '.tag_name' <<< "$release")"
        ;;
esac
version="${tag#n8n@}"
version="${version#v}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+(\.[0-9]+)?$ ]]; then
    echo "Unexpected stable release tag for $upstream: $tag" >&2
    exit 1
fi
echo "Scaffolding $package from $upstream $tag"

# Some provider-specific scripts overwrite models but do not remove stale files.
# The project directory was verified clean; preserve hand-written source files.
for source in "$project_dir"/*RepositoryContext.cs "$project_dir"/Models/*.g.cs; do
    if ! head -n 2 "$source" | grep -Fx '// <auto-generated/>' >/dev/null; then
        echo "Refusing to replace a non-generated file: $source" >&2
        exit 1
    fi
done
rm -- "$project_dir"/*RepositoryContext.cs "$project_dir"/Models/*.g.cs

case "$package" in
    AnythingLLM)
        download "https://codeload.github.com/$upstream/tar.gz/refs/tags/$tag" -o "$scratch/source.tar.gz"
        tar -xzf "$scratch/source.tar.gz" -C "$scratch" --strip-components=4 \
            --wildcards '*/server/prisma/migrations/*/migration.sql'
        python3 - "$scratch/schema.sqlite" "$scratch" <<'PY'
from pathlib import Path
import sqlite3
import sys

migrations = sorted(Path(sys.argv[2]).glob("*/migration.sql"))
if not migrations:
    raise SystemExit("No AnythingLLM SQLite migrations in the release archive")
with sqlite3.connect(sys.argv[1]) as database:
    database.set_authorizer(lambda action, *_: sqlite3.SQLITE_DENY
                            if action in (sqlite3.SQLITE_ATTACH, sqlite3.SQLITE_DETACH)
                            else sqlite3.SQLITE_OK)
    for migration in migrations:
        database.executescript(migration.read_text(encoding="utf-8"))
PY
        dotnet run --file scripts/generate-anythingllm-context.cs -- "$scratch/schema.sqlite"
        ;;
    Dify)
        start_postgres dify
        redis_container="$(docker run --detach --network "container:$container" redis:7-alpine)"
        docker run --rm --network "container:$container" \
            -e MODE=migration -e MIGRATION_ENABLED=true -e SECRET_KEY=nextorm-schema-fixture \
            -e DB_TYPE=postgresql -e DB_HOST=127.0.0.1 -e DB_DATABASE=dify \
            -e DB_USERNAME=postgres -e DB_PASSWORD=nextorm-ci \
            -e REDIS_HOST=127.0.0.1 -e REDIS_PASSWORD= \
            "langgenius/dify-api:$version"
        DIFY_POSTGRES_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=dify;Username=postgres;Password=nextorm-ci" \
            dotnet run --file scripts/generate-dify-context.cs
        ;;
    Ghost)
        # Ghost 6 releases have used both monorepo layouts.
        if ! download "https://raw.githubusercontent.com/$upstream/$tag/ghost/core/server/data/schema/schema.js" \
            -o "$scratch/schema.js"; then
            download "https://raw.githubusercontent.com/$upstream/$tag/ghost/core/core/server/data/schema/schema.js" \
                -o "$scratch/schema.js"
        fi
        extractor="$PWD/scripts/extract-ghost-schema.cjs"
        node --permission --allow-fs-read="$extractor" --allow-fs-read="$scratch/schema.js" \
            "$extractor" "$scratch/schema.js" > "$scratch/schema.json"
        dotnet run --file scripts/generate-ghost-context.cs < "$scratch/schema.json"
        ;;
    Gitea|Gitea.PostgreSql)
        download_asset "gitea-$version-linux-amd64" "$scratch/gitea"
        chmod +x "$scratch/gitea"

        config="$scratch/app.ini"
        printf '[security]\nINSTALL_LOCK = true\n[database]\n' > "$config"
        if [[ "$package" == Gitea ]]; then
            printf 'DB_TYPE = sqlite3\nPATH = %s\n' "$scratch/schema.sqlite" >> "$config"
        else
            start_postgres gitea
            printf 'DB_TYPE = postgres\nHOST = %s\nNAME = gitea\nUSER = postgres\nPASSWD = nextorm-ci\nSSL_MODE = disable\n' \
                "$address" >> "$config"
        fi
        "$scratch/gitea" migrate --work-path "$scratch" --config "$config"

        if [[ "$package" == Gitea ]]; then
            test -s "$scratch/schema.sqlite"
            dotnet run --project src/Tools/Nextorm.Scaffold -c Release -p:PublishAot=false -- scaffold \
                --provider sqlite --database "$scratch/schema.sqlite" \
                --namespace Nextorm.Extensions.Gitea --context GiteaSqliteRepositoryContext \
                --output-dir "$project_dir" --force
        else
            GITEA_POSTGRES_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=gitea;Username=postgres;Password=nextorm-ci" \
                dotnet run --file scripts/generate-gitea-postgresql-context.cs
        fi
        ;;
    Grafana)
        sqlite_from_image "grafana/grafana:$version" 3000 /api/health /var/lib/grafana/grafana.db \
            -e GF_ANALYTICS_REPORTING_ENABLED=false -e GF_ANALYTICS_CHECK_FOR_UPDATES=false
        dotnet run --file scripts/generate-grafana-context.cs -- "$scratch/schema.sqlite"
        ;;
    Immich)
        compose="$(download "https://github.com/$upstream/releases/download/$tag/docker-compose.yml")"
        database_image="$(sed -nE 's#^[[:space:]]*image: (ghcr\.io/immich-app/postgres:[A-Za-z0-9._-]+@sha256:[0-9a-f]{64})$#\1#p' <<< "$compose")"
        test -n "$database_image"
        start_postgres immich "$database_image" --publish 127.0.0.1::2283
        redis_container="$(docker run --detach --network "container:$container" valkey/valkey:9)"
        server_container="$(docker run --detach --network "container:$container" \
            -e DB_HOSTNAME=127.0.0.1 -e DB_DATABASE_NAME=immich \
            -e DB_USERNAME=postgres -e DB_PASSWORD=nextorm-ci \
            -e REDIS_HOSTNAME=127.0.0.1 \
            "ghcr.io/immich-app/immich-server:$tag")"
        wait_for_http "http://$(docker port "$container" 2283/tcp)/api/server/ping" "$server_container"
        IMMICH_POSTGRES_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=immich;Username=postgres;Password=nextorm-ci" \
            dotnet run --file scripts/generate-immich-context.cs
        ;;
    Jellyfin)
        sqlite_from_image "jellyfin/jellyfin:$version" 8096 /health /config/data/jellyfin.db
        dotnet run --file scripts/generate-jellyfin-context.cs -- "$scratch/schema.sqlite"
        ;;
    LiteLLM)
        download "https://raw.githubusercontent.com/$upstream/$tag/litellm/proxy/schema.prisma" -o "$scratch/schema.prisma"
        docker run --rm -v "$scratch/schema.prisma:/tmp/schema.prisma:ro" -w /tmp node:22-bookworm \
            npx --yes prisma@6.19.0 migrate diff --from-empty --to-schema-datamodel /tmp/schema.prisma --script \
            > "$scratch/schema.sql"
        test -s "$scratch/schema.sql"
        start_postgres litellm
        docker exec -i "$container" psql -X -U postgres -d litellm -v ON_ERROR_STOP=1 < "$scratch/schema.sql"
        LITELLM_POSTGRES_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=litellm;Username=postgres;Password=nextorm-ci" \
            dotnet run --file scripts/generate-litellm-context.cs
        ;;
    Mastodon)
        start_postgres mastodon
        docker run --rm --network "container:$container" \
            -e LOCAL_DOMAIN=localhost -e RAILS_ENV=production \
            -e SECRET_KEY_BASE=nextorm-schema-fixture -e OTP_SECRET=nextorm-schema-fixture \
            -e DB_HOST=127.0.0.1 -e DB_USER=postgres -e DB_NAME=mastodon -e DB_PASS=nextorm-ci \
            "ghcr.io/mastodon/mastodon:$tag" bundle exec rails db:schema:load
        SCAFFOLD_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=mastodon;Username=postgres;Password=nextorm-ci" \
            dotnet run --project src/Tools/Nextorm.Scaffold -c Release -p:PublishAot=false -- scaffold \
                --provider postgresql --connection-env SCAFFOLD_CONNECTION \
                --namespace Nextorm.Extensions.Mastodon --context MastodonPostgreSqlRepositoryContext \
                --output-dir "$project_dir" --force
        ;;
    N8n)
        sqlite_from_image "docker.n8n.io/n8nio/n8n:$version" 5678 /healthz/readiness /home/node/.n8n/database.sqlite \
            -e N8N_DIAGNOSTICS_ENABLED=false -e N8N_VERSION_NOTIFICATIONS_ENABLED=false
        bash scripts/generate-n8n-context.sh "$scratch/schema.sqlite"
        ;;
    OpenCode)
        download_asset opencode-linux-x64.tar.gz "$scratch/source.tar.gz"
        tar -xzf "$scratch/source.tar.gz" -C "$scratch"
        executable="$(find "$scratch" -maxdepth 3 -name opencode -type f -executable)"
        test -x "$executable"
        OPENCODE_DB="$scratch/schema.sqlite" OPENCODE_DISABLE_MODELS_FETCH=true \
            start_local_server "$executable" serve --hostname 127.0.0.1 --port 0
        stop_local_server
        test -s "$scratch/schema.sqlite"
        dotnet run --project src/Tools/Nextorm.Scaffold -c Release -p:PublishAot=false -- scaffold \
            --provider sqlite --database "$scratch/schema.sqlite" \
            --namespace Nextorm.Extensions.OpenCode --context OpenCodeSqliteRepositoryContext \
            --output-dir "$project_dir" --force
        ;;
    OpenWebUI)
        sqlite_from_image "ghcr.io/open-webui/open-webui:$tag" 8080 /health /app/backend/data/webui.db \
            -e OFFLINE_MODE=true -e HF_HUB_OFFLINE=1 -e ENABLE_RAG_LOCAL_WEB_FETCH=false
        dotnet run --file scripts/generate-openwebui-context.cs -- "$scratch/schema.sqlite"
        ;;
    Redmine)
        start_postgres redmine
        docker run --rm --network "container:$container" \
            -e REDMINE_DB_POSTGRES=127.0.0.1 -e REDMINE_DB_DATABASE=redmine \
            -e REDMINE_DB_USERNAME=postgres -e REDMINE_DB_PASSWORD=nextorm-ci \
            "redmine:$version" bundle exec rake db:migrate
        REDMINE_POSTGRES_CONNECTION="Host=127.0.0.1;Port=${address##*:};Database=redmine;Username=postgres;Password=nextorm-ci" \
        dotnet run --file scripts/generate-redmine-context.cs
        ;;
    T3Code)
        download_asset "t3-$version-linux-x64.tar.gz" "$scratch/source.tar.gz"
        tar -xzf "$scratch/source.tar.gz" -C "$scratch"
        mkdir "$scratch/project"
        SHELL=/bin/sh start_local_server "$scratch/t3-$version-linux-x64/t3" serve \
            --mode desktop --host 127.0.0.1 --port "$((49152 + RANDOM % 16384))" \
            --no-browser --base-dir "$scratch/data" "$scratch/project"
        stop_local_server
        dotnet run --file scripts/generate-t3code-context.cs -- "$scratch/data/userdata/statev2.sqlite"
        ;;
    WordPress)
        container="$(docker run --detach --publish 127.0.0.1::3306 \
            -e MYSQL_DATABASE=wordpress -e MYSQL_USER=wordpress \
            -e MYSQL_PASSWORD=nextorm-ci -e MYSQL_ROOT_PASSWORD=nextorm-ci mysql:8.4)"
        for ((attempt=0; attempt<60; attempt++)); do
            if docker exec "$container" mysqladmin ping --host=127.0.0.1 --silent; then
                break
            fi
            sleep 1
        done
        docker exec "$container" mysqladmin ping --host=127.0.0.1 --silent
        docker run --rm --network "container:$container" --user 0:0 \
            --entrypoint sh -e "WP_VERSION=$version" wordpress:cli -ec '
                cd /tmp
                wp --allow-root core download --version="$WP_VERSION" --locale=en_US --skip-content
                wp --allow-root config create --dbname=wordpress --dbuser=wordpress --dbpass=nextorm-ci --dbhost=127.0.0.1
                wp --allow-root core install --url=http://localhost --title=Nextorm-CI \
                    --admin_user=admin --admin_password=nextorm-ci --admin_email=ci@example.com --skip-email
            '
        address="$(docker port "$container" 3306/tcp)"
        SCAFFOLD_CONNECTION="Server=127.0.0.1;Port=${address##*:};Database=wordpress;User ID=wordpress;Password=nextorm-ci;" \
            dotnet run --project src/Tools/Nextorm.Scaffold -c Release -p:PublishAot=false -- scaffold \
                --provider mysql --connection-env SCAFFOLD_CONNECTION --schema wordpress \
                --namespace Nextorm.Extensions.WordPress --context WordPressMySqlRepositoryContext \
                --output-dir "$project_dir" --force
        ;;
esac

if [[ -z "$(find "$project_dir/Models" -maxdepth 1 -name '*.g.cs' -type f -print -quit)" ]]; then
    echo "No models generated for $package." >&2
    exit 1
fi

# Keep provenance accurate, including for generators that omit the header.
for source in "$project_dir"/*RepositoryContext.cs "$project_dir"/Models/*.g.cs; do
    sed -i "/^\\/\\/ Source database version: /d; /^\\/\\/ <auto-generated\\/>$/a // Source database version: \"$version\"" "$source"
done
printf '%s\n' "$version" > "$version_file"
