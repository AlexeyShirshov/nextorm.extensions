#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.Sqlite/Nextorm.Contexts.Sqlite.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using Nextorm.Contexts.Sqlite;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run scripts/generate-jellyfin-context.cs -- <jellyfin.db>");
    return 1;
}

SqliteSchemaGenerator.Scaffold(
    args[0],
    "src/RepositoryContext/Nextorm.Extensions.Jellyfin/JellyfinSqliteRepositoryContext.cs",
    "Nextorm.Extensions.Jellyfin",
    "JellyfinSqliteRepositoryContext",
    "12.4.0");
return 0;
