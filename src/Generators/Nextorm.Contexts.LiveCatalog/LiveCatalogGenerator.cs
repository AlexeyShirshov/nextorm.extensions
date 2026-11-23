using MySqlConnector;
using Nextorm.Contexts.MariaDb;
using Nextorm.Contexts.MySql;
using NextORM.Core;
using NextORM.MySql;
using NextORM.MariaDb;
using MySqlDatabaseMappings;
using MariaDbDatabaseMappings;

namespace Nextorm.Contexts.LiveCatalog;

public enum CatalogProvider
{
    MySql,
    MariaDb
}

public static class LiveCatalogGenerator
{
    public static void ScaffoldMySql(string connectionString, string schema, string outputPath,
        string targetNamespace, string contextName) =>
        MySqlSchemaGenerator.Scaffold(connectionString, schema, outputPath, targetNamespace, contextName);

    public static void ScaffoldMySql(string connectionString, string schema, string outputPath,
        string targetNamespace, string contextName, global::Nextorm.Scaffold.IScaffoldInterceptor interceptor, bool singleFile = false) =>
        MySqlSchemaGenerator.Scaffold(connectionString, schema, outputPath, targetNamespace, contextName, interceptor, singleFile);

    public static void ScaffoldMySql(string connectionString, string schema, string outputPath,
        string targetNamespace, string contextName, Func<string, bool>? include, Func<string, string>? entityName,
        Func<string, string, string>? propertyName, Func<string, string, string?>? clrType, bool singleFile = false) =>
        MySqlSchemaGenerator.Scaffold(connectionString, schema, outputPath, targetNamespace, contextName,
            include, entityName, propertyName, clrType, singleFile);

    // Connection settings and the output path are supplied by the trusted caller.
    public static void Generate(CatalogProvider provider, string connectionString,
        string schema, string table, string outputPath)
    {
        if (provider is not (CatalogProvider.MySql or CatalogProvider.MariaDb))
            throw new ArgumentOutOfRangeException(nameof(provider));
        ArgumentException.ThrowIfNullOrEmpty(schema);
        ArgumentException.ThrowIfNullOrEmpty(table);

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        var isMariaDb = connection.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);
        if (isMariaDb != (provider == CatalogProvider.MariaDb))
            throw new InvalidDataException($"Connected server does not match the selected provider {provider}.");

        using DataContext context = isMariaDb
            ? new MariaDbDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers())
            : new MySqlDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
        var (kind, columns, keys) = isMariaDb ? ReadMariaDb() : ReadMySql();
        var isView = kind == "VIEW";

        var mysql = new List<MySqlColumn>();
        var mariadb = new List<MariaDbColumn>();
        var mariaDbKeys = new SortedDictionary<int, string>();
        string? primaryKey = null;
        foreach (var column in columns.OrderBy(column => column.Ordinal))
        {
            var name = column.Name ?? throw new InvalidDataException("Catalog column name is missing.");
            var declaration = column.Declaration ?? throw new InvalidDataException("Catalog column type is missing.");
            var nullable = column.Nullable == "YES";
            var keyOrdinal = keys.GetValueOrDefault(name);
            if (keyOrdinal != 0)
            {
                if (isMariaDb)
                    mariaDbKeys.Add(keyOrdinal, name);
                else
                {
                    if (primaryKey is not null)
                        throw new InvalidDataException($"{provider} live catalog mode requires a single-column primary key.");
                    primaryKey = name;
                }
            }
            if (isMariaDb)
                mariadb.Add(new(name, declaration, nullable));
            else
                mysql.Add(new(name, declaration, nullable));
        }

        if (isMariaDb)
        {
            if (mariadb.Count == 0)
                throw new InvalidDataException("MariaDB live catalog mode requires a visible ordinary view or base table.");
            if (mariaDbKeys.Count > 1)
                MariaDbSchemaGenerator.GenerateComposite(schema, table, mariadb, [.. mariaDbKeys.Values], outputPath);
            else
                MariaDbSchemaGenerator.Generate(schema, table, mariadb,
                    isView ? MariaDbObjectKind.View : MariaDbObjectKind.BaseTable,
                    mariaDbKeys.Values.SingleOrDefault(), outputPath);
            return;
        }

        if (mysql.Count == 0)
            throw new InvalidDataException("MySql live catalog mode requires a visible ordinary view or base table.");

        if (isView)
        {
            MySqlSchemaGenerator.GenerateViewMapping(schema, table, mysql, outputPath);
            return;
        }

        MySqlSchemaGenerator.Generate(schema, table, mysql, primaryKey, outputPath);

        (string?, ColumnMetadata[], Dictionary<string, int>) ReadMySql()
        {
            var catalog = new MySqlRepositoryContext(context);
            var tableKind = catalog.Tables.Where(row => row.Column1 == schema && row.Column2 == table).ToList()
                .SingleOrDefault(row => Matches(row.Column1, row.Column2))?.Column3;
            if (tableKind is not ("BASE TABLE" or "VIEW"))
                return (tableKind, [], []);
            var fields = catalog.Columns.Where(row => row.Column1 == schema && row.Column2 == table).ToList()
                .Where(row => Matches(row.Column1, row.Column2))
                .Select(row => new ColumnMetadata(row.Column3, row.Column4, row.Column5, row.Column6)).ToArray();
            var primary = tableKind == "VIEW" ? [] : catalog.KeyColumnUsage
                .Where(row => row.Column1 == schema && row.Column2 == table && row.Column4 == "PRIMARY").ToList()
                .Where(row => Matches(row.Column1, row.Column2))
                .ToDictionary(row => row.Column3!, row => row.Column5 ?? 0, StringComparer.Ordinal);
            return (tableKind, fields, primary);
        }

        (string?, ColumnMetadata[], Dictionary<string, int>) ReadMariaDb()
        {
            var catalog = new MariaDbRepositoryContext(context);
            var tableKind = catalog.Tables.Where(row => row.Column1 == schema && row.Column2 == table).ToList()
                .SingleOrDefault(row => Matches(row.Column1, row.Column2))?.Column3;
            if (tableKind is not ("BASE TABLE" or "VIEW"))
                return (tableKind, [], []);
            var fields = catalog.Columns.Where(row => row.Column1 == schema && row.Column2 == table).ToList()
                .Where(row => Matches(row.Column1, row.Column2))
                .Select(row => new ColumnMetadata(row.Column3, row.Column4, row.Column5, row.Column6)).ToArray();
            var primary = tableKind == "VIEW" ? [] : catalog.KeyColumnUsage
                .Where(row => row.Column1 == schema && row.Column2 == table && row.Column4 == "PRIMARY").ToList()
                .Where(row => Matches(row.Column1, row.Column2))
                .ToDictionary(row => row.Column3!, row => row.Column5 ?? 0, StringComparer.Ordinal);
            return (tableKind, fields, primary);
        }

        bool Matches(string? actualSchema, string? actualTable) => actualSchema == schema && actualTable == table;
    }

    private sealed record ColumnMetadata(string? Name, string? Declaration, string? Nullable, int? Ordinal);
}
