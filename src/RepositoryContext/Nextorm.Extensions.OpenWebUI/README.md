# Nextorm.Extensions.OpenWebUI

Nextorm repository context for the Open WebUI 0.13.5 SQLite schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.OpenWebUI
```

The package depends on `NextORM.Sqlite` (NextORM core provider).

## Read migration revisions from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadOpenWebUI.cs` at
the repository root. Use a local copy of your Open WebUI SQLite database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.OpenWebUI/Nextorm.Extensions.OpenWebUI.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using Nextorm.Extensions.OpenWebUI;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadOpenWebUI DATABASE_PATH");
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
var repository = new OpenWebUISqliteRepositoryContext(dataContext);

var rows = repository.AlembicVersion.Query()
    .OrderBy(row => row.VersionNum)
    .Limit(10)
    .Select(row => row.VersionNum)
    .ToList();
foreach (var revision in rows)
    Console.WriteLine(revision);
return 0;
```

Run the file to print the recorded migration revisions, up to ten. Replace
`./app.db` with the path to your database:

```sh
dotnet run --file ReadOpenWebUI.cs -- ./app.db
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the same file and run the executable against the same database:

```sh
dotnet publish ReadOpenWebUI.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadOpenWebUI ./app.db
```
