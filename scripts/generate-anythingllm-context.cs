#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.Sqlite/Nextorm.Contexts.Sqlite.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.Sqlite;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run scripts/generate-anythingllm-context.cs -- <anythingllm.db>");
    return 1;
}

SqliteSchemaGenerator.Scaffold(
    args[0],
    "src/RepositoryContext/Nextorm.Extensions.AnythingLLM/AnythingLLMSqliteRepositoryContext.cs",
    "Nextorm.Extensions.AnythingLLM",
    "AnythingLLMSqliteRepositoryContext",
    "1.8.5");
return 0;
