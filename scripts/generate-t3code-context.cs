#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.Sqlite/Nextorm.Contexts.Sqlite.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.Sqlite;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run scripts/generate-t3code-context.cs -- <statev2.sqlite>");
    return 1;
}

SqliteSchemaGenerator.Scaffold(
    args[0],
    "src/RepositoryContext/Nextorm.Extensions.T3Code/T3CodeSqliteRepositoryContext.cs",
    "Nextorm.Extensions.T3Code",
    "T3CodeSqliteRepositoryContext",
    "0.1.11",
    // The FTS index and its shadow tables are derived from the message tables.
    include: name => name != "orchestration_v2_message_search" &&
        !name.StartsWith("orchestration_v2_message_search_", StringComparison.Ordinal),
    entityName: null,
    propertyName: null,
    clrType: (_, declared) => declared.Equals("VARCHAR(255)", StringComparison.OrdinalIgnoreCase)
        ? "string"
        : null);
return 0;
