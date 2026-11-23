# Nextorm.Extensions.AnythingLLM

Nextorm repository context for the AnythingLLM Desktop v1.8.5 SQLite schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.AnythingLLM
```

The package depends on `NextORM.Sqlite` (NextORM core provider).

## Read workspaces from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadAnythingLLM.cs` at
the repository root. Use a local copy of your AnythingLLM SQLite database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.AnythingLLM/Nextorm.Extensions.AnythingLLM.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using Nextorm.Extensions.AnythingLLM;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadAnythingLLM DATABASE_PATH");
    return 1;
}

var settings = new SqliteConnectionStringBuilder
{
    DataSource = args[0],
    Mode = SqliteOpenMode.ReadOnly,
};
using var connection = new SqliteConnection(settings.ToString());
connection.Open();

using var dataContext = new SqliteDataContext(
    connection, new DataContextBuilder().UseQuotedIdentifiers());
var repository = new AnythingLLMSqliteRepositoryContext(dataContext);

var rows = repository.Workspaces.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Name })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Name}");
return 0;
```

Run the file to print up to ten workspace IDs and names. Replace `./app.db`
with the path to your database:

```sh
dotnet run --file ReadAnythingLLM.cs -- ./app.db
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the same file and run the executable against the same database:

```sh
dotnet publish ReadAnythingLLM.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadAnythingLLM ./app.db
```
