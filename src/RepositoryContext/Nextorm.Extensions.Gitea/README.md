# Nextorm.Extensions.Gitea

Nextorm repository context for the Gitea 28.4.0 SQLite schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.Gitea
```

The package depends on `NextORM.Sqlite` (NextORM core provider).

## Read repository topics

To try the SQLite context without a `.csproj`, install the .NET 11 SDK and
the `sqlite3` command-line client. If you reference the NuGet package instead
of the source project, you can also use the .NET 10 SDK.

1. From the repository root, create a fixture database in a temporary directory:

   ```sh
   demo_dir="$(mktemp -d)"
   sqlite3 "$demo_dir/gitea.db" < tests/AppSchemas/gitea.sqlite.sql
   sqlite3 "$demo_dir/gitea.db" 'INSERT INTO repo_topic VALUES (1, 42);'
   ```

2. Save this file-based app (SFA) as `ReadGitea.cs` in the repository root.
   To use NuGet instead of the source project, replace the `#:project`
   directive with `#:package Nextorm.Extensions.Gitea@1.0.0`:

   ```csharp
   #:project src/RepositoryContext/Nextorm.Extensions.Gitea/Nextorm.Extensions.Gitea.csproj
   #:property TargetFramework=net10.0
   #:property PublishAot=false

   using Microsoft.Data.Sqlite;
   using NextORM.Core;
   using NextORM.Sqlite;
   using Nextorm.Extensions.Gitea;

   if (args.Length != 1)
   {
       Console.Error.WriteLine("Usage: ReadGitea DATABASE_PATH");
       return 1;
   }

   var connectionString = new SqliteConnectionStringBuilder
   {
       DataSource = args[0],
       Mode = SqliteOpenMode.ReadOnly,
   }.ToString();
   using var connection = new SqliteConnection(connectionString);
   connection.Open();
   using var dataContext = new SqliteDataContext(connection,
       new DataContextBuilder().UseQuotedIdentifiers());
   var repository = new GiteaSqliteRepositoryContext(dataContext);

   foreach (var topic in repository.RepoTopic.ToList())
       Console.WriteLine($"{topic.RepoId}: {topic.TopicId}");
   return 0;
   ```

3. Run the example. It prints `1: 42`:

   ```sh
   dotnet run --file ReadGitea.cs -- "$demo_dir/gitea.db"
   ```

## Run the same query with Native AOT

On Linux x64, install the .NET 11 SDK and the
[Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Use the source project reference in `ReadGitea.cs` for this example.

1. Change the `TargetFramework` directive in `ReadGitea.cs` to:

   ```csharp
   #:property TargetFramework=net11.0
   ```

2. Publish the same app. The command overrides `PublishAot=false` in the
   file:

   ```sh
   dotnet publish ReadGitea.cs -c Release -r linux-x64 \
     -p:PublishAot=true -o ./native
   ```

3. Run the executable against the same fixture. It prints `1: 42`:

   ```sh
   ./native/ReadGitea "$demo_dir/gitea.db"
   ```
