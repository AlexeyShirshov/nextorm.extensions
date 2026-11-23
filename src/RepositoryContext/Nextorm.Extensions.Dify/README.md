# Nextorm.Extensions.Dify

Nextorm repository context for the Dify 1.9.2 PostgreSQL schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Dify
```

The package depends on `NextORM.Postgres` (NextORM core provider).

## Read applications from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadDify.cs` at the
repository root. Use an account with read access to the Dify database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Dify/Nextorm.Extensions.Dify.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.Dify;

var connectionString = Environment.GetEnvironmentVariable("NEXTORM_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set NEXTORM_CONNECTION to a PostgreSQL connection string.");
    return 1;
}

using var connection = new NpgsqlConnection(connectionString);
connection.Open();

using var dataContext = new PostgresDataContext(
    connection, new DataContextBuilder().UseQuotedIdentifiers());
var repository = new DifyPostgreSqlRepositoryContext(dataContext);

var rows = repository.Apps.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Name })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Name}");
return 0;
```

Set `NEXTORM_CONNECTION` to your PostgreSQL connection string. Run the file
to print up to ten application IDs and names:

```sh
dotnet run --file ReadDify.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadDify.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadDify
```
