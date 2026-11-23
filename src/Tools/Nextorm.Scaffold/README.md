# Nextorm.Scaffold

`dotnet-nextorm` global tool: generate C# Nextorm repository contexts from an existing database.

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
