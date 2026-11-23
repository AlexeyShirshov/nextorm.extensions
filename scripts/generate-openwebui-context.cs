#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.Sqlite/Nextorm.Contexts.Sqlite.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.Sqlite;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run scripts/generate-openwebui-context.cs -- <webui.db>");
    return 1;
}

SqliteSchemaGenerator.Scaffold(
    args[0],
    "src/RepositoryContext/Nextorm.Extensions.OpenWebUI/OpenWebUISqliteRepositoryContext.cs",
    "Nextorm.Extensions.OpenWebUI",
    "OpenWebUISqliteRepositoryContext",
    "0.6.43",
    columnClrType: (table, column, declared) => (table, column, declared.ToUpperInvariant()) switch
    {
        // Chat timestamps are written as Unix seconds, even on databases migrated from the legacy DATETIME schema.
        ("chat", "created_at" or "updated_at", "DATETIME") => "long",
        (_, _, "DATE") => "global::System.DateOnly",
        (_, _, "VARCHAR" or "VARCHAR(255)" or "VARCHAR(32)" or "VARCHAR(50)" or "JSON") => "string",
        _ => null
    },
    converterType: (_, _, declared) => declared.Equals("DATE", StringComparison.OrdinalIgnoreCase)
        ? "global::Nextorm.Extensions.OpenWebUI.OpenWebUIDateConverter"
        : null);
return 0;
