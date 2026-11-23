#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.PostgreSql/Nextorm.Contexts.PostgreSql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.PostgreSql;

var connectionString = Environment.GetEnvironmentVariable("LITELLM_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set LITELLM_POSTGRES_CONNECTION to a clean database migrated by LiteLLM Proxy 1.110.2.");
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
    clrType: (_, declared) => declared is "AccountingDeliveryState" or "JobStatus" ? "string" : null);
return 0;
