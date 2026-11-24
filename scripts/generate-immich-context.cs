#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("IMMICH_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set IMMICH_POSTGRES_CONNECTION to a disposable database migrated by Immich.");
    return 1;
}

var enumTypes = await ReadEnumTypesAsync(connectionString);
PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Immich/ImmichPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Immich",
    "ImmichPostgreSqlRepositoryContext",
    // Immich excludes the backup orchestrator's tables from its own schema migrations.
    include: static name => !name.StartsWith("futo_backups_", StringComparison.Ordinal),
    clrType: (_, declared) => declared switch
    {
        "date" => "global::System.DateOnly",
        "timestamptz" => "global::System.DateTime",
        "polygon" => "global::NpgsqlTypes.NpgsqlPolygon",
        "vector" => "global::NextORM.Postgres.PgVector",
        _ when enumTypes.Contains(declared) => "string",
        _ => null
    },
    converterType: (_, declared) => declared == "date" ? "global::Nextorm.Extensions.Internal.PostgreSqlDateConverter" : null);
return 0;

static async Task<HashSet<string>> ReadEnumTypesAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    // The mapping callback receives an unqualified type name. If schemas
    // reuse a name, map it only when every type with that name is an enum.
    await using var command = new NpgsqlCommand("""
        SELECT typname::text
        FROM pg_catalog.pg_type
        GROUP BY typname
        HAVING bool_and(typtype = 'e')
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var types = new HashSet<string>(StringComparer.Ordinal);
    while (await reader.ReadAsync())
    {
        types.Add(reader.GetString(0));
    }
    return types;
}
