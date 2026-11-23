# Nextorm.Contexts.Sqlite

Schema generator for SQLite databases.

## Usage

```csharp
using Nextorm.Contexts.Sqlite;

SqliteSchemaGenerator.Generate("app.db", "Generated/AppRepositoryContext.cs");
```

Or with scaffolding (split model files):

```csharp
SqliteSchemaGenerator.Scaffold("app.db", "Generated", "MyApp.Data", "AppRepositoryContext");
```
