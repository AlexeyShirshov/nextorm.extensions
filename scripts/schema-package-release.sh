#!/usr/bin/env bash
set -euo pipefail

version_pattern='(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
upstream_pattern='[0-9]+\.[0-9]+(\.[0-9]+)?'

project_for() {
    local package_id="$1"
    [[ "$package_id" =~ ^Nextorm\.Extensions\.[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)?$ ]] || return 1
    printf 'src/RepositoryContext/%s/%s.csproj\n' "$package_id" "$package_id"
}

project_version() {
    local version
    version="$(sed -nE "s@^[[:space:]]*<PackageVersion>($version_pattern)</PackageVersion>[[:space:]]*\$@\1@p" "$1")"
    if [[ ! "$version" =~ ^${version_pattern}$ ]]; then
        echo "Expected one stable PackageVersion in $1." >&2
        return 1
    fi
    printf '%s\n' "$version"
}

update_metadata() {
    local project="$1" next="$2" upstream="$3"
    [[ "$upstream" =~ ^${upstream_pattern}$ ]]
    sed -i -E \
        -e "s@<PackageVersion>${version_pattern}</PackageVersion>@<PackageVersion>$next</PackageVersion>@" \
        -e "s@(<Description>[^<]* v?)${upstream_pattern}( [^<]*</Description>)@\1$upstream\3@" \
        "$project"
    sed -i -E "3s@( v?)${upstream_pattern}( )@\1$upstream\3@" "${project%/*}/README.md"
}

prepare() {
    local package="$1" output="$2" package_id project package_dir current published versions next major minor baseline
    package_id="Nextorm.Extensions.$package"
    project="$(project_for "$package_id")"
    package_dir="${project%/*}"
    current="$(project_version "$project")"
    versions="$(curl --fail --silent --show-error --retry 3 --max-time 30 \
        "https://api.nuget.org/v3-flatcontainer/${package_id,,}/index.json")"
    published="$(jq -er --arg pattern "^${version_pattern}$" \
        '.versions[] | select(test($pattern))' <<< "$versions" | LC_ALL=C sort -V | tail -n 1)"
    mkdir -p "$output"
    baseline=(-p:PackageValidationBaselineVersion="$published")

    if [[ "$published" == "$current" ]]; then
        bash scripts/scaffold-upstream.sh "$package" "$output/upstream.txt"
        git add -A -- "$package_dir/Models" "$package_dir/"*RepositoryContext.cs
        if git diff --cached --quiet -I '^// (Source database version:|Generated for )' -- "$package_dir"; then
            echo "$package_id: generated schema is unchanged."
            return
        fi
        # New schema mappings can add public members.
        IFS=. read -r major minor _ <<< "$current"
        next="$major.$((minor + 1)).0"
        git diff --cached --binary --no-renames -- "$package_dir" > "$output/generated.patch"
        git diff --cached --stat -- "$package_dir"
        update_metadata "$project" "$next" "$(< "$output/upstream.txt")"
    elif [[ "$(printf '%s\n' "$current" "$published" | LC_ALL=C sort -V | tail -n 1)" == "$current" &&
        -f "$package_dir/.schema-release" && "$(< "$package_dir/.schema-release")" == "$current" ]]; then
        echo "$package_id: retrying publication of $current."
        next="$current"
        if [[ "${current%%.*}" != "${published%%.*}" ]]; then
            baseline=()
        fi
    else
        echo "$package_id: repository version $current and NuGet version $published disagree; check the manual release." >&2
        return 1
    fi

    if ! dotnet pack "$project" -c Release -o "$output" \
        -p:EnablePackageValidation=true "${baseline[@]}" 2>&1 | tee "$output/pack.log"; then
        # Only a candidate that built and has a confirmed API removal may
        # switch to a major release. Restore/compile failures still stop it.
        if [[ ! -s "$output/generated.patch" || ! -s "$output/$package_id.$next.nupkg" ]] ||
            ! grep -Eq 'error CP000[12]:' "$output/pack.log"; then
            return 1
        fi
        echo "$package_id: API removal detected; packing the next major version."
        rm -- "$output/$package_id.$next.nupkg"
        next="$((major + 1)).0.0"
        update_metadata "$project" "$next" "$(< "$output/upstream.txt")"
        dotnet pack "$project" -c Release -o "$output" -p:EnablePackageValidation=true
    fi
    rm -- "$output/pack.log"
    test -s "$output/$package_id.$next.nupkg"
    printf '%s\n' "$next" > "$output/version.txt"
    printf 'ready=true\n' >> "${GITHUB_OUTPUT:?}"
    printf -- '- %s: %s → %s\n' "$package_id" "$published" "$next" >> "${GITHUB_STEP_SUMMARY:?}"
}

record() {
    local artifacts="$1" candidate package_id project package_dir next current major minor packages
    local count=0
    shopt -s nullglob
    for candidate in "$artifacts"/schema-*; do
        package_id="Nextorm.Extensions.${candidate##*/schema-}"
        project="$(project_for "$package_id")"
        package_dir="${project%/*}"
        current="$(project_version "$project")"
        next="$(< "$candidate/version.txt")"
        [[ "$next" =~ ^${version_pattern}$ ]]
        packages=("$candidate"/*.nupkg)
        [[ "${#packages[@]}" -eq 1 && "${packages[0]}" == "$candidate/$package_id.$next.nupkg" ]]
        test -s "${packages[0]}"
        if [[ -s "$candidate/generated.patch" ]]; then
            IFS=. read -r major minor _ <<< "$current"
            [[ "$next" == "$major.$((minor + 1)).0" || "$next" == "$((major + 1)).0.0" ]]
            git apply --index --include="$package_dir/Models/*.g.cs" \
                --include="$package_dir/*RepositoryContext.cs" "$candidate/generated.patch"
            if git diff --cached --quiet -- "$package_dir"; then
                echo "$package_id: no generated changes in release patch." >&2
                return 1
            fi
            update_metadata "$project" "$next" "$(< "$candidate/upstream.txt")"
            printf '%s\n' "$next" > "$package_dir/.schema-release"
            git add -- "$project" "$package_dir/README.md" "$package_dir/.schema-release"
        else
            [[ "$next" == "$current" && "$(< "$package_dir/.schema-release")" == "$current" ]]
        fi
        count=$((count + 1))
    done

    if ((count > 0)); then
        if ! git diff --cached --quiet; then
            git -c user.name='github-actions[bot]' \
                -c user.email='41898282+github-actions[bot]@users.noreply.github.com' \
                commit -m 'Refresh application database schemas'
            git push origin HEAD:refs/heads/main
        else
            # Don't retry pending releases from a superseded checkout.
            [[ "$(git ls-remote origin refs/heads/main | cut -f1)" == "$(git rev-parse HEAD)" ]]
        fi
        printf 'ready=true\n' >> "${GITHUB_OUTPUT:?}"
    fi
}

case "${1:-}" in
    prepare) prepare "$2" "$3" ;;
    record) record "$2" ;;
    *) echo 'Usage: schema-package-release.sh prepare PACKAGE OUTPUT | record ARTIFACTS' >&2; exit 1 ;;
esac
