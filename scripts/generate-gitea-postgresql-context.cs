#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("GITEA_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set GITEA_POSTGRES_CONNECTION to a clean database migrated by Gitea 1.24.7.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.Gitea.PostgreSql/GiteaPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.Gitea.PostgreSql",
    "GiteaPostgreSqlRepositoryContext");
return 0;
