# Nextorm.Scaffold.Abstractions

Public customization contract for Nextorm schema scaffolding.

## Installation

```sh
dotnet add package Nextorm.Scaffold.Abstractions
```

## Usage

Implement `IScaffoldInterceptor` to filter or rename generated entities without changing the SQL mapping:

```csharp
using Nextorm.Scaffold;

public sealed class MyInterceptor : IScaffoldInterceptor
{
    public bool IncludeObject(string databaseName) => databaseName != "audit_log";

    public string GetEntityName(string databaseName) => databaseName + "Entity";

    public string GetPropertyName(string databaseObjectName, string databaseColumnName) => databaseColumnName;

    public string? GetClrType(string databaseColumnName, string sqlDeclaration) => null; // use provider default
}
```

Compile the interceptor into a `.dll` and pass it to the scaffold tool via `--customize`.
