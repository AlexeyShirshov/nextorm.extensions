# Nextorm.Extensions.Ghost

Nextorm repository context for the Ghost 6.79.0 MySQL application schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Ghost
```

The package depends on `NextORM.MySql` (NextORM core provider).

## Read post titles from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadGhost.cs` at the
repository root. Use an account with read access to the Ghost database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Ghost/Nextorm.Extensions.Ghost.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using MySqlConnector;
using NextORM.Core;
using NextORM.MySql;
using Nextorm.Extensions.Ghost;

var connectionString = Environment.GetEnvironmentVariable("NEXTORM_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set NEXTORM_CONNECTION to a MySQL connection string.");
    return 1;
}

using var connection = new MySqlConnection(connectionString);
connection.Open();

using var dataContext = new MySqlDataContext(
    connection, new DataContextBuilder().UseQuotedIdentifiers());
var repository = new GhostMySqlRepositoryContext(dataContext);

var rows = repository.Posts.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Title })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Title}");
return 0;
```

Set `NEXTORM_CONNECTION` to your MySQL connection string. Run the file
to print up to ten post IDs and titles:

```sh
dotnet run --file ReadGhost.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadGhost.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadGhost
```
