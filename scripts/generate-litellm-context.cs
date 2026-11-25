#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("LITELLM_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set LITELLM_POSTGRES_CONNECTION to a disposable database migrated by LiteLLM Proxy.");
    return 1;
}

PostgreSqlSchemaGenerator.Scaffold(
    connectionString,
    "src/RepositoryContext/Nextorm.Extensions.LiteLLM/LiteLLMPostgreSqlRepositoryContext.cs",
    "Nextorm.Extensions.LiteLLM",
    "LiteLLMPostgreSqlRepositoryContext",
    include: null,
    entityName: null,
    propertyName: null,
    clrType: static (_, declared) => declared switch
    {
        "timestamptz" => "global::System.DateTime",
        "AccountingDeliveryState" or "JobStatus" or "ScheduledJobRunStatus" => "string",
        _ => null
    });
return 0;
