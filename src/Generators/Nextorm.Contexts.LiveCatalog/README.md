# Nextorm.Contexts.LiveCatalog

Live database catalog reader used by the scaffold tool to introspect schemas.

## Usage

Consumed internally by `Nextorm.Scaffold`; not intended for direct use. Example entry point:

```csharp
using Nextorm.Contexts.LiveCatalog;

LiveCatalogGenerator.Generate(provider, connectionString, outputPath, ...);
```
