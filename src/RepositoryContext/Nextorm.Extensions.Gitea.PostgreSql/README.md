# Nextorm.Extensions.Gitea.PostgreSql

Nextorm repository context for the Gitea 1.24.7 PostgreSQL schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Gitea.PostgreSql
```

The package depends on `NextORM.Postgres` (NextORM core provider).

## Read repository topics from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadGiteaPostgreSql.cs` at
the repository root. Use an account with read access to the Gitea database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.Gitea.PostgreSql/Nextorm.Extensions.Gitea.PostgreSql.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.Gitea.PostgreSql;

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
var repository = new GiteaPostgreSqlRepositoryContext(dataContext);

var rows = repository.RepoTopic.Query()
    .OrderBy(row => row.RepoId)
    .ThenBy(row => row.TopicId)
    .Limit(10)
    .ToList();
foreach (var row in rows)
    Console.WriteLine($"{row.RepoId}: {row.TopicId}");
return 0;
```

Set `NEXTORM_CONNECTION` to your PostgreSQL connection string. Run the file
to print up to ten pairs of repository and topic IDs:

```sh
dotnet run --file ReadGiteaPostgreSql.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadGiteaPostgreSql.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadGiteaPostgreSql
```
