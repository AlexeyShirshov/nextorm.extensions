using Nextorm.Contexts.MySql;
using Nextorm.Contexts.PostgreSql;
using Nextorm.Contexts.Sqlite;
using Nextorm.Contexts.SqlServer;
using Nextorm.Scaffold;

namespace Nextorm.Scaffolding;

public static class SchemaScaffolder
{
    public static void Generate(string provider, string source, string output, string targetNamespace,
        string context, IScaffoldInterceptor interceptor, bool singleFile,
        Func<string, bool> includeSchema, string? mysqlSchema)
    {
        switch (provider)
        {
            case "sqlite":
                SqliteSchemaGenerator.Scaffold(source, output, targetNamespace, context, null, interceptor, singleFile);
                break;
            case "postgresql":
                PostgreSqlSchemaGenerator.Scaffold(source, output, targetNamespace, context, interceptor, singleFile,
                    includeSchema: includeSchema);
                break;
            case "mysql":
                MySqlSchemaGenerator.Scaffold(source, mysqlSchema!, output, targetNamespace, context, interceptor, singleFile);
                break;
            case "mariadb":
                MySqlSchemaGenerator.ScaffoldMariaDb(source, mysqlSchema!, output, targetNamespace, context, interceptor, singleFile);
                break;
            case "sqlserver":
                SqlServerSchemaGenerator.Scaffold(source, output, targetNamespace, context, interceptor, singleFile, includeSchema);
                break;
            default:
                throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
        }
    }
}
