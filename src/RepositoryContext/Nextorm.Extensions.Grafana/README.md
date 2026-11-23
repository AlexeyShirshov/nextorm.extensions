# Nextorm.Extensions.Grafana

Nextorm repository context for the Grafana OSS 12.3.0 SQLite schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Grafana
```

The package depends on `NextORM.Sqlite` (NextORM core provider).

## Read dashboards from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadGrafana.cs` at
the repository root. Use a local copy of your Grafana SQLite database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Grafana/Nextorm.Extensions.Grafana.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using Nextorm.Extensions.Grafana;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadGrafana DATABASE_PATH");
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
var repository = new GrafanaSqliteRepositoryContext(dataContext);

var rows = repository.Dashboard.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Title })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Title}");
return 0;
```

Run the file to print up to ten dashboard IDs and titles. Replace `./app.db`
with the path to your database:

```sh
dotnet run --file ReadGrafana.cs -- ./app.db
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the same file and run the executable against the same database:

```sh
dotnet publish ReadGrafana.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadGrafana ./app.db
```
