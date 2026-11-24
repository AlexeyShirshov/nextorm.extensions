#!/usr/bin/env dotnet
#:project ../src/Generators/Nextorm.Contexts.MySql/Nextorm.Contexts.MySql.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false

using System.Text.Json;
using Nextorm.Contexts.MySql;

var tables = JsonSerializer.Deserialize<MySqlTable[]>(Console.In.ReadToEnd())
    ?? throw new InvalidDataException("Expected schema JSON from extract-ghost-schema.cjs.");
MySqlSchemaGenerator.Scaffold("ghost", tables,
    "src/RepositoryContext/Nextorm.Extensions.Ghost/GhostMySqlRepositoryContext.cs",
    "Nextorm.Extensions.Ghost", "GhostMySqlRepositoryContext",
    include: null, entityName: null, propertyName: null,
    clrType: (_, declaration) => declaration switch
    {
        "tinyint(1)" => "bool",
        "blob" => "byte[]",
        _ when declaration.StartsWith("varbinary(", StringComparison.Ordinal) => "byte[]",
        _ => null
    });
