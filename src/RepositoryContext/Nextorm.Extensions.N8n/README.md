# Nextorm.Extensions.N8n

Use `N8nSqliteRepositoryContext` to query the n8n 2.48.8 SQLite schema from .NET.
The package maps tables such as `workflow_entity` and `execution_entity` with
their default names (no `DB_TABLE_PREFIX`).
It doesn't target PostgreSQL or map the per-instance tables that n8n creates
for user-defined Data Tables.

## Read workflows from a single C# file

Use the .NET 11 SDK and a consistent backup of your n8n SQLite database.
The default database path is `~/.n8n/database.sqlite`.
Save this file-based app as `ReadN8n.cs` at the repository root:

```csharp
#:project src/RepositoryContext/Nextorm.Extensions.N8n/Nextorm.Extensions.N8n.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using Nextorm.Extensions.N8n;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadN8n DATABASE_PATH");
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
var repository = new N8nSqliteRepositoryContext(dataContext);

var rows = repository.WorkflowEntity.Query()
    .OrderBy(row => row.Id)
    .Limit(10)
    .Select(row => new { row.Id, row.Name, row.UpdatedAt })
    .ToList();
foreach (var row in rows)
{
    Console.WriteLine($"{row.Id}: {row.Name} ({row.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff})");
}
return 0;
```

Run the file to print up to ten workflow IDs, names, and update timestamps:

```sh
dotnet run --file ReadN8n.cs -- DATABASE_PATH
```

Replace `DATABASE_PATH` with the path to the backup.

## Run the same query with Native AOT

On Linux x64, install the [Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the example and run it against the same backup:

```sh
dotnet publish ReadN8n.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadN8n DATABASE_PATH
```

## Regenerate the context

From the repository root, run the scaffold script against a clean database
initialized by n8n 2.48.8 with the default table names:

```sh
bash scripts/generate-n8n-context.sh DATABASE_PATH
```

Replace `DATABASE_PATH` with the path to that database. The script replaces the
generated context and model files.
