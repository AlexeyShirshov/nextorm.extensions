# Nextorm extensions

Use the [application repository contexts](src/RepositoryContext) to read an
existing application's database from .NET.

## Run a package example as a file-based app

Each [repository context README](src/RepositoryContext) includes a complete C#
example and a `dotnet run --file` command. Use the .NET 11 SDK to run examples
that reference projects in this checkout.

The `#:project` directive is relative to the example file. If the package has
been published and you want to use it outside this repository, replace that
directive with `#:package PACKAGE_ID@PACKAGE_VERSION`, using the package ID
and an available version.

For SQLite, pass the path to a local database copy. For PostgreSQL and MySQL,
set the connection string in the environment variable named by the example.
Use a database account with read access rather than storing credentials in the
C# file.

## Run a repository query with Native AOT

Each package README also shows how to publish the same C# file as a Linux x64
executable and run it without `dotnet`. Test your own queries against the native
build before shipping it.

## Refresh package schemas

The [publish workflow](.github/workflows/publish-opencode.yml) checks public
stable releases daily at 04:23 UTC. It uses a fresh database or an upstream
schema definition. It does not connect to deployments or use application data.

The [schema acquisition script](scripts/scaffold-upstream.sh) covers 16
packages. Orca still requires manual refresh with its [three database
snapshots](src/RepositoryContext/Nextorm.Extensions.Orca/README.md#regenerate-the-contexts).

The workflow compares the generated mappings with `main`. A version comment
alone doesn't trigger a release. If the mappings change, the workflow checks
the package against the last stable version on NuGet.org. It increments the
minor version for compatible changes and the major version when the check
confirms a removed type or member. Other build or validation errors block
the affected package, not the rest of the matrix.

For a dry run, open **Actions → Publish Nextorm extension packages → Run
workflow**, select `refresh_schemas`, and leave `publish_schema_updates` off.
Check the run summary for candidates and download their `schema-*` artifacts
to inspect the generated patches. To apply and publish changes manually, also
select `publish_schema_updates` and run from `main`.

The scheduled run saves the generated files and package versions to `main`
before using the existing NuGet Trusted Publishing policy. Keep Actions write
access to `main` and the `NUGET_USER` secret available. If publication fails
after the commit, start a new refresh run from `main` to retry the saved
version without increasing it again.
