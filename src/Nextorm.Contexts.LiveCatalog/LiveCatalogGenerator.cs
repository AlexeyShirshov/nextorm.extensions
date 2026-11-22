using MySqlConnector;
using Nextorm.Contexts.MariaDb;
using Nextorm.Contexts.MySql;

namespace Nextorm.Contexts.LiveCatalog;

public enum CatalogProvider
{
    MySql,
    MariaDb
}

public static class LiveCatalogGenerator
{
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

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.COLUMN_NAME, c.COLUMN_TYPE, c.IS_NULLABLE,
                   COALESCE(k.ORDINAL_POSITION, 0), t.TABLE_TYPE
            FROM information_schema.COLUMNS AS c
            JOIN information_schema.TABLES AS t
              ON BINARY t.TABLE_SCHEMA = BINARY c.TABLE_SCHEMA
             AND BINARY t.TABLE_NAME = BINARY c.TABLE_NAME
            LEFT JOIN information_schema.KEY_COLUMN_USAGE AS k
              ON BINARY k.TABLE_SCHEMA = BINARY c.TABLE_SCHEMA
             AND BINARY k.TABLE_NAME = BINARY c.TABLE_NAME
             AND BINARY k.COLUMN_NAME = BINARY c.COLUMN_NAME
             AND k.CONSTRAINT_NAME = 'PRIMARY'
             AND t.TABLE_TYPE = 'BASE TABLE'
            WHERE BINARY c.TABLE_SCHEMA = BINARY @schema
              AND BINARY c.TABLE_NAME = BINARY @table
              AND t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
            ORDER BY c.ORDINAL_POSITION
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        var mysql = new List<MySqlColumn>();
        var mariadb = new List<MariaDbColumn>();
        var mariaDbKeys = new SortedDictionary<int, string>();
        string? primaryKey = null;
        var isView = false;
        using var metadata = command.ExecuteReader();
        while (metadata.Read())
        {
            var name = metadata.GetString(0);
            var declaration = metadata.GetString(1);
            var nullable = metadata.GetString(2) == "YES";
            isView = metadata.GetString(4) == "VIEW";
            var keyOrdinal = metadata.GetInt32(3);
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

        if (isView)
        {
            MySqlSchemaGenerator.GenerateViewMapping(schema, table, mysql, outputPath);
            return;
        }

        if (primaryKey is null)
            throw new InvalidDataException($"{provider} live catalog mode requires a visible ordinary table with a single-column primary key.");
        MySqlSchemaGenerator.Generate(schema, table, mysql, primaryKey, outputPath);
    }
}
