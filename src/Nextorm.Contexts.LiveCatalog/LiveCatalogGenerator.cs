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
                   CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END, t.TABLE_TYPE
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
              AND (t.TABLE_TYPE = 'BASE TABLE' OR (@allowView = 1 AND t.TABLE_TYPE = 'VIEW'))
            ORDER BY c.ORDINAL_POSITION
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@allowView", isMariaDb ? 0 : 1);

        var mysql = new List<MySqlColumn>();
        var mariadb = new List<MariaDbColumn>();
        string? primaryKey = null;
        var isView = false;
        using var metadata = command.ExecuteReader();
        while (metadata.Read())
        {
            var name = metadata.GetString(0);
            var declaration = metadata.GetString(1);
            var nullable = metadata.GetString(2) == "YES";
            isView = metadata.GetString(4) == "VIEW";
            if (metadata.GetInt32(3) != 0)
            {
                if (primaryKey is not null)
                    throw new InvalidDataException($"{provider} live catalog mode requires a single-column primary key.");
                primaryKey = name;
            }
            if (isMariaDb)
                mariadb.Add(new(name, declaration, nullable));
            else
                mysql.Add(new(name, declaration, nullable));
        }

        if (isView)
        {
            MySqlSchemaGenerator.GenerateViewMapping(schema, table, mysql, outputPath);
            return;
        }

        if (primaryKey is null)
            throw new InvalidDataException($"{provider} live catalog mode requires a visible ordinary table with a single-column primary key.");
        if (isMariaDb)
            MariaDbSchemaGenerator.Generate(schema, table, mariadb, primaryKey, outputPath);
        else
            MySqlSchemaGenerator.Generate(schema, table, mysql, primaryKey, outputPath);
    }
}
