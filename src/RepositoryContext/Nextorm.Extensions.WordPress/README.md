# Nextorm.Extensions.WordPress

Nextorm repository context for the WordPress terms schema.

## Installation

```sh
dotnet add package Nextorm.Extensions.WordPress
```

The package depends on `NextORM.MySql` and `NextORM.MariaDb` (NextORM core providers); use the one matching your server.

## Read WordPress terms from a single C# file

Use the .NET 10 SDK or later and an account with read access to the
`wp_terms` table in a MySQL WordPress database. No `.csproj` is needed.

1. Save the following file as `ReadWordPress.cs`:

   ```csharp
   #:package Nextorm.Extensions.WordPress@1.0.0
   #:property TargetFramework=net10.0
   #:property PublishAot=false

   using MySqlConnector;
   using NextORM.Core;
   using NextORM.MySql;
   using Nextorm.Extensions.WordPress;

   var connectionString =
       Environment.GetEnvironmentVariable("WORDPRESS_CONNECTION");
   if (string.IsNullOrWhiteSpace(connectionString))
   {
       Console.Error.WriteLine(
           "Set WORDPRESS_CONNECTION to a MySQL connection string.");
       return 1;
   }

   using var connection = new MySqlConnection(connectionString);
   connection.Open();
   using var dataContext = new MySqlDataContext(
       connection, new DataContextBuilder().UseQuotedIdentifiers());
   var repository = new WordPressMySqlRepositoryContext(dataContext);

   var terms = repository.WpTerms.Query()
       .OrderBy(term => term.TermId)
       .Limit(10)
       .ToList();
   foreach (var term in terms)
       Console.WriteLine($"{term.TermId}: {term.Name}");
   return 0;
   ```

2. Set `WORDPRESS_CONNECTION` to a MySQL connection string in your shell.
   In Bash, use the following commands to enter it without echoing it or
   putting it in shell history:

   ```bash
   read -rsp 'MySQL connection string: ' WORDPRESS_CONNECTION
   printf '\n'
   export WORDPRESS_CONNECTION
   ```

3. Run the file. It prints up to ten term IDs and names, ordered by ID:

   ```sh
   dotnet run --file ReadWordPress.cs
   ```

The context maps tables with the `wp_` prefix.

## Run the same WordPress query with Native AOT

On Linux x64, install the .NET 11 SDK and the
[Native AOT build prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites).
Keep `WORDPRESS_CONNECTION` set as in the file-based example.

1. In `ReadWordPress.cs`, change the `TargetFramework` directive to:

   ```csharp
   #:property TargetFramework=net11.0
   ```

2. Publish the same file. The command overrides `PublishAot=false` in the
   source:

   ```sh
   dotnet publish ReadWordPress.cs -c Release -r linux-x64 \
     -p:PublishAot=true -o ./native
   ```

3. Run the executable directly, without `dotnet`. With a valid connection
   string, it reads the same `wp_terms` table:

   ```sh
   ./native/ReadWordPress
   ```
