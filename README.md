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
