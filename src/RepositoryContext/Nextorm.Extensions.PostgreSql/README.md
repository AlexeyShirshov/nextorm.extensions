# Nextorm.Extensions.PostgreSql

Use this library in a .NET application with NextORM to inspect PostgreSQL
schemas through `pg_catalog` and use pgvector cosine distance in queries.
To generate mappings for your application tables and views, see
[Nextorm.Contexts.PostgreSql](https://github.com/AlexeyShirshov/nextorm.extensions/tree/main/src/Generators/Nextorm.Contexts.PostgreSql).

## Install the NuGet package

From your .NET project directory, add the package:

```sh
dotnet add package Nextorm.Extensions.PostgreSql
```

## Query the PostgreSQL catalog

The library targets .NET 10 and depends on `Nextorm.Postgres`. To run this
example from a checkout, use the .NET 11 SDK and an account that can connect
to a PostgreSQL database and read `pg_catalog.pg_namespace`. You don't need
pgvector for catalog queries.

1. Set `NEXTORM_CONNECTION` to your PostgreSQL connection string.
2. At the repository root, save the following file as `ReadPgCatalog.cs`.
   The `#:project` directive references the library in your checkout:

   ```csharp
   #:project src/RepositoryContext/Nextorm.Extensions.PostgreSql/Nextorm.Extensions.PostgreSql.csproj
   #:property TargetFramework=net11.0
   #:property PublishAot=false

   using Npgsql;
   using NextORM.Core;
   using NextORM.Postgres;
   using Nextorm.Extensions.PostgreSql.PgCatalog;

   var connectionString = Environment.GetEnvironmentVariable("NEXTORM_CONNECTION");
   if (string.IsNullOrWhiteSpace(connectionString))
   {
       Console.Error.WriteLine("Set NEXTORM_CONNECTION before running.");
       return 1;
   }

   using var connection = new NpgsqlConnection(connectionString);
   connection.Open();

   using var db = new PostgresDataContext(
       connection, new DataContextBuilder().UseQuotedIdentifiers());

   var schemas = db.From<PgNamespace>()
       .OrderBy(schema => schema.Name)
       .Select(schema => schema.Name)
       .ToList();

   foreach (var schema in schemas)
   {
       Console.WriteLine(schema);
   }

   return 0;
   ```

   To run the file outside the checkout, replace the `#:project` directive with
   `#:package Nextorm.Extensions.PostgreSql@1.0.0`.

3. From the directory containing the file, run it. The query prints one schema name
   per line, including `pg_catalog`:

   ```sh
   dotnet run --file ReadPgCatalog.cs
   ```

## Run the catalog query with Native AOT

On Linux x64, install the
[Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites)
and keep `NEXTORM_CONNECTION` set.

1. From the repository root, publish the example as a native executable:

   ```sh
   dotnet publish ReadPgCatalog.cs -c Release -r linux-x64 \
     -p:PublishAot=true -o ./native
   ```

2. Run the executable to print the same schema names:

   ```sh
   ./native/ReadPgCatalog
   ```

This example projects a single property. With `Nextorm.Postgres` 1.0.11,
materializing a full catalog model such as `PgRelation` can fail in Native
AOT if trimming removes the constructor that NextORM looks up.

## Catalog mappings and SQL functions

Pass a model from `Nextorm.Extensions.PostgreSql.PgCatalog` to
`IDataContext.From<T>()`. For example, `PgRelation` maps selected columns
from `pg_catalog.pg_class`. The
[handwritten catalog models](https://github.com/AlexeyShirshov/nextorm.extensions/blob/main/src/RepositoryContext/Nextorm.Extensions.PostgreSql/PgCatalog/PgCatalogModels.cs) cover roles,
schemas, relations, columns, types, extensions, constraints, indexes,
partitions, enum labels, and sequences. The PostgreSQL scaffolder excludes
`pg_catalog` from generated mappings and doesn't map all of its native types.

Use these models to read structured schema details:

| Metadata | Model or query |
| --- | --- |
| Column defaults, identity, and generated columns | Use `PgAttribute` for the flags and `PgAttributeDefault` for catalog row IDs. Use `db.AttributeDefaultExpressions()` to return defaults and generation expressions as SQL text. |
| Foreign keys | Use `PgConstraint.Keys` and `ReferencedKeys` for source and target column numbers, and `OnUpdate` and `OnDelete` for PostgreSQL action codes. |
| Indexes | Use `PgIndexMetadata` for uniqueness, primary-key status, and column numbers. The first `KeyCount` entries in `Keys` are key columns; any remaining entries are included columns. A zero denotes an expression. Use `PgIndex.Definition` for the SQL text. |
| Partitions and inheritance | Use `PgPartitionedTable` for partitioning strategy and keys, and `PgInheritance` for parent-child links. Use `PgCatalogFunctions.PartitionKey` and `PartitionConstraint` for SQL text. |
| Enum labels and sequences | Order `PgEnum` rows by `SortOrder` for label order. Use `PgSequence` for sequence settings. |

Reading all columns from `PgAttribute` requires PostgreSQL 12 or later
because `attgenerated` is available starting in PostgreSQL 12.

If `relation` is a `PgRelation`, read its default and generated expressions
by passing its object identifier (`oid`) to the query:

```csharp
var expressions = db.AttributeDefaultExpressions(relation.Oid).ToList();
```

To filter an object identifier in your own query, store the captured
parameter as a `long` and call `PgCatalogFunctions.ObjectId` in the predicate.

Use [`PgCatalogFunctions`](https://github.com/AlexeyShirshov/nextorm.extensions/blob/main/src/RepositoryContext/Nextorm.Extensions.PostgreSql/PgCatalog/PgCatalogFunctions.cs) and
[`PgVectorFunctions.CosineDistance`](https://github.com/AlexeyShirshov/nextorm.extensions/blob/main/src/RepositoryContext/Nextorm.Extensions.PostgreSql/PgVector/PgVectorFunctions.cs) inside
NextORM query expressions so NextORM translates them to SQL. Calling them
directly from .NET throws `NotSupportedException`.

Queries that use `CosineDistance` require pgvector installed in the `public`
schema of the target database because the helper maps to
`public.cosine_distance`.
