# Nextorm.Extensions.Mastodon

Nextorm repository context for the Mastodon PostgreSQL tags schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Mastodon
```

The package depends on `NextORM.Postgres` (NextORM core provider).

## Read tags from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadMastodon.cs` at the
repository root. Use an account with read access to the Mastodon database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Mastodon/Nextorm.Extensions.Mastodon.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.Mastodon;

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
var repository = new MastodonPostgreSqlRepositoryContext(dataContext);

var rows = repository.Tags.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Name })
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.Id}: {row.Name}");
return 0;
```

Set `NEXTORM_CONNECTION` to your PostgreSQL connection string. Run the file
to print up to ten tag IDs and names:

```sh
dotnet run --file ReadMastodon.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadMastodon.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadMastodon
```
