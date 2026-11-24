#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("MASTODON_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set MASTODON_POSTGRES_CONNECTION to a disposable database initialized by Mastodon.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Mastodon/MastodonPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Mastodon",
    "MastodonPostgreSqlRepositoryContext",
    clrType: (_, declared) => declared switch
    {
        "date" => "global::System.DateOnly",
        "numeric" => "decimal",
        "timestamptz" => "global::System.DateTime",
        _ => null
    },
    converterType: (_, declared) => declared == "date" ? "global::Nextorm.Extensions.Internal.PostgreSqlDateConverter" : null);
return 0;
