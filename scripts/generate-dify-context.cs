#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("DIFY_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set DIFY_POSTGRES_CONNECTION to a clean database migrated by Dify 1.9.2.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Dify/DifyPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Dify",
    "DifyPostgreSqlRepositoryContext",
    include: null,
    entityName: null,
    propertyName: null,
    clrType: (_, declared) => declared switch
    {
        "numeric" => "decimal",
        "timestamptz" => "global::System.DateTime",
        _ => null
    });
return 0;
