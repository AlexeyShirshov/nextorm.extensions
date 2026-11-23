#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("REDMINE_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set REDMINE_POSTGRES_CONNECTION to a clean database migrated by Redmine 6.1.0.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Redmine/RedminePostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Redmine",
    "RedminePostgreSqlRepositoryContext",
    clrType: (_, declared) => declared switch
    {
        "date" => "global::System.DateOnly",
        "numeric" => "decimal",
        _ => null
    },
    converterType: (_, declared) => declared == "date" ? "global::Nextorm.Extensions.Internal.PostgreSqlDateConverter" : null);
return 0;
