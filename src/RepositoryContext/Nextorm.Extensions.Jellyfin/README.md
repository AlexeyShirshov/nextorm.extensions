# Nextorm.Extensions.Jellyfin

Nextorm repository context for the Jellyfin 12.4.0 SQLite schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Jellyfin
```

The package depends on `NextORM.Sqlite` (NextORM core provider).

## Read library items from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadJellyfin.cs` at
the repository root. Use a local copy of your Jellyfin SQLite database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Jellyfin/Nextorm.Extensions.Jellyfin.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using Nextorm.Extensions.Jellyfin;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadJellyfin DATABASE_PATH");
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
var repository = new JellyfinSqliteRepositoryContext(dataContext);

var rows = repository.BaseItems.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Name })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Name}");
return 0;
```

Run the file to print up to ten library item IDs and names. Replace `./app.db`
with the path to your database:

```sh
dotnet run --file ReadJellyfin.cs -- ./app.db
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the same file and run the executable against the same database:

```sh
dotnet publish ReadJellyfin.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadJellyfin ./app.db
```
