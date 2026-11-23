namespace Nextorm.Scaffold;

/// <summary>Customizes the generated C# model without changing the SQL mapping.</summary>
public interface IScaffoldInterceptor
{
    bool IncludeObject(string databaseName) => true;

    string GetEntityName(string databaseName) => databaseName;

    string GetPropertyName(string databaseObjectName, string databaseColumnName) => databaseColumnName;

    /// <summary>Returns a C# type expression, or null to use the provider's default.</summary>
    string? GetClrType(string databaseColumnName, string sqlDeclaration) => null;
}
