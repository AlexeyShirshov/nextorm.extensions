# Nextorm.Scaffold

Use the `dotnet-nextorm` global tool to generate C# Nextorm repository contexts
from a database or standalone C# models from OData metadata.

## Installation

```sh
dotnet tool install --global Nextorm.Scaffold
```

## Usage

SQLite:

```sh
dotnet-nextorm scaffold \
  --provider sqlite \
  --database ./app.db \
  --namespace MyApp.Data \
  --context AppRepositoryContext \
  --output-dir ./Generated
```

PostgreSQL:

```sh
dotnet-nextorm scaffold \
  --provider postgresql \
  --connection-env DATABASE_URL \
  --namespace MyApp.Data \
  --context AppRepositoryContext \
  --output-dir ./Generated
```

MySQL / MariaDB (requires exactly one schema):

```sh
dotnet-nextorm scaffold \
  --provider mysql \
  --connection-env MYSQL_URL \
  --schema mydb \
  --namespace MyApp.Data \
  --context AppRepositoryContext \
  --output-dir ./Generated
```

Use `--customize path/to/interceptor.dll` to apply an `IScaffoldInterceptor`, `--table`/`--exclude-table` to filter tables, and `--force` to overwrite previously generated files.

## Generate models from OData metadata

To generate C# models from an OData 4.0 or 4.01 XML metadata endpoint, run:

```sh
dotnet-nextorm odata-models \
  --metadata 'https://SERVICE_HOST/odata/$metadata' \
  --namespace MyApp.Models \
  --output ./Generated/ODataModels.g.cs
```

Replace `SERVICE_HOST` with your service's host and adjust the path to match
its metadata URL. Keep the URL in single quotes so the shell doesn't expand
`$metadata`. If you have an XML metadata file, pass its path to `--metadata`
instead of the URL.

Check `./Generated/ODataModels.g.cs` for the classes and enums. The command
appends each OData schema namespace to `--namespace`, so a `Catalog` schema
appears under `MyApp.Models.Catalog`. Use `--force` to replace a file this
command generated.

The command generates models only, not a repository context or OData client.
It doesn't fetch external schema references. If a property or base class
uses an unsupported type, such as a spatial type or a type defined only in
an external document, the command reports an error without changing the
output file.
