# Nextorm.Extensions.LiteLLM

Nextorm repository context for the LiteLLM Proxy 1.111.0 PostgreSQL schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.LiteLLM
```

The package depends on `NextORM.Postgres` (NextORM core provider).

## Read model IDs from a single C# file

With the .NET 11 SDK, save this file-based app as `ReadLiteLLM.cs` at the
repository root. Use an account with read access to the LiteLLM database:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.LiteLLM/Nextorm.Extensions.LiteLLM.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.LiteLLM;

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
var repository = new LiteLLMPostgreSqlRepositoryContext(dataContext);

var ids = repository.LiteLLMModelTable.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => row.Id)
    .ToList();
foreach (var id in ids)
    Console.WriteLine(id);
return 0;
```

Set `NEXTORM_CONNECTION` to your PostgreSQL connection string. Run the file
to print up to ten model IDs:

```sh
dotnet run --file ReadLiteLLM.cs
```

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `NEXTORM_CONNECTION` set, then publish and run the executable:

```sh
dotnet publish ReadLiteLLM.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadLiteLLM
```
