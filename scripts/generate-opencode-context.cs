#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.Sqlite/Nextorm.Contexts.Sqlite.csproj

using Nextorm.Contexts.Sqlite;

const string OpencodeDatabaseVersion = "2.3.2";

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/generate-opencode-context.cs -- <database-path> <output-path.cs>");
    return 2;
}

SqliteSchemaGenerator.GenerateForProject(args[0], args[1], OpencodeDatabaseVersion);
return 0;
