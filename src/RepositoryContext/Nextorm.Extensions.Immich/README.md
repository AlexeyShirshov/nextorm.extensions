# Nextorm.Extensions.Immich

Nextorm repository context for the Immich 3.4.2 PostgreSQL schema with native PgVector mappings.

## Installation

```sh
dotnet add package Nextorm.Extensions.Immich
```

The package depends on `NextORM.Postgres` (NextORM core provider).

## Read albums from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadImmich.cs` at the
repository root. Use an account with read access to the Immich database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Immich/Nextorm.Extensions.Immich.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.Immich;

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
var repository = new ImmichPostgreSqlRepositoryContext(dataContext);

var rows = repository.Album.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.AlbumName })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.AlbumName}");
return 0;
```

Set `NEXTORM_CONNECTION` to your PostgreSQL connection string. Run the file
to print up to ten album IDs and names:

```sh
dotnet run --file ReadImmich.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadImmich.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadImmich
```
