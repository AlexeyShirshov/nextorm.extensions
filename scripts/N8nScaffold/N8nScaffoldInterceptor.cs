using Nextorm.Scaffold;

namespace Nextorm.Extensions.N8n.Scaffolding;

public sealed class N8nScaffoldInterceptor : IScaffoldInterceptor
{
    public bool IncludeObject(string databaseName) => true;

    public string GetEntityName(string databaseName) => databaseName;

    public string GetPropertyName(string databaseObjectName, string databaseColumnName) => databaseColumnName;

    public string? GetClrType(string databaseColumnName, string sqlDeclaration) =>
        sqlDeclaration.Trim().ToUpperInvariant() switch
        {
            "VARCHAR" or "JSON" => "string",
            var type when type.StartsWith("VARCHAR(", StringComparison.Ordinal)
                || type.StartsWith("CHAR(", StringComparison.Ordinal) => "string",
            "DATE" or "DATETIME(0)" or "DATETIME(3)" or "TIMESTAMP" => "global::System.DateTime",
            _ => null,
        };
}
