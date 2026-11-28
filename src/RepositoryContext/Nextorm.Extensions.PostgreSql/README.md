# PostgreSQL catalog mappings for NextORM

Reference `Nextorm.Extensions.PostgreSql.csproj` and use the types in
`Nextorm.Extensions.PostgreSql.PgCatalog` with `IDataContext.From<T>()` (for
example, `db.From<PgRelation>()`). The models map the catalog columns needed
for schema inspection. They are maintained by hand because the PostgreSQL
scaffolder excludes `pg_catalog` and does not map all its native types.

Use `PgCatalogFunctions` within NextORM query expressions. Queries that use
`PgVectorFunctions.CosineDistance` require the pgvector extension installed in
the `public` schema; catalog queries do not require the extension.
