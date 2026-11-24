# Nextorm.Extensions.Orca

Use Nextorm to query local SQLite snapshots from [Orca IDE 1.4.272](https://github.com/stablyai/orca/releases/tag/v1.4.272).
The package targets `net10.0` and `net11.0`.

## Choose a database

Each context opens one database through a `SqliteDataContext` you supply:

| SQLite file | Context | What you can query |
| --- | --- | --- |
| `profile-state.db` | `OrcaProfileSqliteRepositoryContext` | Profile documents, document parts, and automation runs |
| `agent-session-domain.db` | `OrcaAgentSessionsSqliteRepositoryContext` | Native chat sessions, journal items, and related records |
| `session-search.sqlite` | `OrcaSessionSearchSqliteRepositoryContext` | Indexed sessions and message metadata when Orca's search index is present |

The package doesn't cover the older `agent-session-journal.db`, or the search
index's `messages_fts` virtual table and its shadow tables. It maps stored JSON
columns as text rather than reconstructing Orca's application objects.

## Read session IDs from a SQLite snapshot

Use a consistent SQLite snapshot. Use SQLite's backup operation for a running
Orca instance; don't copy only the main file while Orca can still write to it.
Open the snapshot in read-only mode as shown in the example.

1. With the .NET 11 SDK, save this file-based app as `ReadOrca.cs` at the
   repository root:

   ```csharp
   #:project src/RepositoryContext/Nextorm.Extensions.Orca/Nextorm.Extensions.Orca.csproj
   #:property TargetFramework=net11.0
   #:property PublishAot=false

   using Microsoft.Data.Sqlite;
   using NextORM.Core;
   using NextORM.Sqlite;
   using Nextorm.Extensions.Orca.AgentSessions;

   if (args.Length != 1)
   {
       Console.Error.WriteLine("Usage: ReadOrca DATABASE_PATH");
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
   var repository = new OrcaAgentSessionsSqliteRepositoryContext(dataContext);

   var rows = repository.AgentSessionRecords.Query()
       .OrderBy(row => row.SessionId)
       .Limit(10)
       .Select(row => new { row.SessionId, row.AdoptedNativeId })
       .ToList();
   foreach (var row in rows)
       Console.WriteLine($"{row.SessionId}: {row.AdoptedNativeId ?? "(none)"}");
   return 0;
   ```

2. Run the example to print up to ten session IDs and their associated native
   agent IDs:

   ```sh
   dotnet run --file ReadOrca.cs -- AGENT_SESSION_DB
   ```

   Replace `AGENT_SESSION_DB` with the path to your `agent-session-domain.db`
   snapshot. If the snapshot has no session records, the command prints nothing.

## Run the same query with Native AOT

On Linux x64, install the [Native AOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Publish the example and run it against the same snapshot:

```sh
dotnet publish ReadOrca.cs -c Release -r linux-x64 \
  -p:PublishAot=true -o ./native
./native/ReadOrca AGENT_SESSION_DB
```

## Regenerate the contexts

From the repository root, run the [scaffold script](../../../scripts/generate-orca-context.sh)
with the three snapshots:

```sh
bash scripts/generate-orca-context.sh PROFILE_DB AGENT_SESSION_DB SESSION_SEARCH_DB
```

Replace `PROFILE_DB`, `AGENT_SESSION_DB`, and `SESSION_SEARCH_DB` with the paths
to `profile-state.db`, `agent-session-domain.db`, and `session-search.sqlite`,
respectively. Review the generated diff, including any models that a newer
schema no longer uses.
