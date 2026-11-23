# Nextorm.Scaffolding

Internal schema scaffolding engine used by `Nextorm.Scaffold`. Not published as a standalone package.

## Usage

Consumed by the `dotnet-nextorm` tool:

```sh
dotnet-nextorm scaffold --provider sqlite --database ./app.db --namespace MyApp.Data --context AppRepositoryContext --output-dir ./Generated
```
