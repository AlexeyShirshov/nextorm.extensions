#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("IMMICH_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set IMMICH_POSTGRES_CONNECTION to a database migrated by Immich 2.0.0.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Immich/ImmichPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Immich",
    "ImmichPostgreSqlRepositoryContext",
    clrType: (_, declared) => declared switch
    {
        "date" => "global::System.DateOnly",
        "timestamptz" => "global::System.DateTime",
        "polygon" => "global::NpgsqlTypes.NpgsqlPolygon",
        "vector" => "global::NextORM.Postgres.PgVector",
        "asset_visibility_enum" or "assets_status_enum" or "sourcetype" => "string",
        _ => null
    },
    converterType: (_, declared) => declared == "date" ? "global::Nextorm.Extensions.Internal.PostgreSqlDateConverter" : null);
return 0;
