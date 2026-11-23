using System.Globalization;
using System.Text;
using Npgsql;
using NextORM.Core;
using NextORM.Postgres;
using PostgreSqlDatabaseMappings;

namespace Nextorm.Contexts.PostgreSql;

public static class PostgreSqlSchemaGenerator
{
    public static void Generate(string connectionString, string outputPath) =>
        Generate(connectionString, outputPath, "PostgreSqlDatabaseMappings", "PostgreSqlRepositoryContext", false);

    public static void Generate(string connectionString, string outputPath, string targetNamespace, string contextName, bool publicContext) =>
        Generate(connectionString, outputPath, targetNamespace, contextName, publicContext, splitModels: false);

    public static void Scaffold(string connectionString, string outputPath, string targetNamespace, string contextName) =>
        Generate(connectionString, outputPath, targetNamespace, contextName, publicContext: true, splitModels: true);

    public static void Scaffold(string connectionString, string outputPath, string targetNamespace, string contextName,
        global::Nextorm.Scaffold.IScaffoldInterceptor interceptor, bool singleFile = false, string? schema = null,
        Func<string, bool>? includeSchema = null) =>
        Generate(connectionString, outputPath, targetNamespace, contextName, true, !singleFile,
            interceptor.IncludeObject, interceptor.GetEntityName, interceptor.GetPropertyName, interceptor.GetClrType, schema, includeSchema);

    public static void Scaffold(string connectionString, string outputPath, string targetNamespace, string contextName,
        Func<string, bool>? include, Func<string, string>? entityName,
        Func<string, string, string>? propertyName, Func<string, string, string?>? clrType) =>
        Generate(connectionString, outputPath, targetNamespace, contextName, true, true, include, entityName, propertyName, clrType);

    private static void Generate(string connectionString, string outputPath, string targetNamespace, string contextName, bool publicContext, bool splitModels,
        Func<string, bool>? include = null, Func<string, string>? renameEntity = null,
        Func<string, string, string>? renameProperty = null, Func<string, string, string?>? clrType = null,
        string? selectedSchema = null, Func<string, bool>? includeSchema = null)
    {
        var destination = Path.GetFullPath(outputPath);
        if (!destination.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must be a C# source file.", nameof(outputPath));

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var context = new PostgresDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        ((ITransactionManager)context).UseTransaction(transaction);
        var catalog = new PostgreSqlRepositoryContext(context);
        var namespaces = catalog.PgNamespace.ToList().ToDictionary(row => row.Oid, row => row.Nspname);
        var classes = new List<(uint Oid, uint Relnamespace, string Relname, char Relkind)>();
        using (var command = new NpgsqlCommand("SELECT oid, relnamespace, relname, relkind::text FROM pg_catalog.pg_class", connection, transaction))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                classes.Add((reader.GetFieldValue<uint>(0), reader.GetFieldValue<uint>(1), reader.GetString(2), reader.GetString(3)[0]));
        }
        var objects = classes
            .Where(row => row.Relkind is 'r' or 'p' or 'f' or 'v' or 'm')
            .Select(row => (Row: row, Schema: namespaces[row.Relnamespace]))
            .Where(item => item.Schema is not ("pg_catalog" or "information_schema") &&
                !item.Schema.StartsWith("pg_toast", StringComparison.Ordinal) &&
                !item.Schema.StartsWith("pg_temp_", StringComparison.Ordinal))
            .Where(item => include?.Invoke(item.Row.Relname) ?? true)
            .Where(item => selectedSchema is null || item.Schema == selectedSchema)
            .Where(item => includeSchema?.Invoke(item.Schema) ?? true)
            .OrderBy(item => Encoding.UTF8.GetBytes(item.Schema), ByteOrder)
            .ThenBy(item => Encoding.UTF8.GetBytes(item.Row.Relname), ByteOrder);
        var columns = catalog.PgAttribute.Where(row => row.Attnum > 0 && !row.Attisdropped).ToList()
            .ToLookup(row => row.Attrelid);
        var types = catalog.PgType.ToList().ToDictionary(row => row.Oid);
        var keys = catalog.PgIndex.Where(row => row.Indisprimary).ToList().ToDictionary(row => row.Indrelid);
        transaction.Commit();
        var rows = from item in objects
                   from field in columns[item.Row.Oid].OrderBy(field => field.Attnum).DefaultIfEmpty()
                   select (item.Row, item.Schema, Field: field);
        var source = new StringBuilder($"// <auto-generated/>\n#nullable enable\nnamespace {targetNamespace};\n");
        var visibility = publicContext ? "public" : "internal";
        uint? previous = null;
        var objectNumber = 0;
        var columnNumber = 0;
        var mappings = new List<string>();
        var usedMappings = new HashSet<string>(StringComparer.Ordinal) { contextName, "Repository" };
        var usedProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (row, schema, field) in rows)
        {
            var id = row.Oid;
            var name = row.Relname;
            if (id != previous)
            {
                if (previous.HasValue)
                    source.Append("}\n");
                previous = id;
                columnNumber = 0;
                var mapping = publicContext
                    ? RepositoryContextSource.Identifier(renameEntity?.Invoke(name) ?? name, "Entity", usedMappings)
                    : FormattableString.Invariant($"Object{++objectNumber}");
                mappings.Add(mapping);
                usedProperties.Clear();
                usedProperties.Add(mapping);
                source.Append(CultureInfo.InvariantCulture,
                    $"\n[global::System.ComponentModel.DataAnnotations.Schema.Table({Literal(name)}, Schema = {Literal(schema)})]\n{visibility} class {mapping}\n{{\n");
            }

            if (field is null)
                continue;

            var column = field.Attname;
            var native = types[field.Atttypid];
            var builtIn = namespaces[native.Typnamespace] == "pg_catalog" ? native.Typname : null;
            var declaration = FormatDeclaration(native.Typname, builtIn, field.Atttypmod);
            var type = clrType?.Invoke(column, declaration) ?? (builtIn switch
            {
                "bool" => "bool",
                "int2" => "short",
                "int4" => "int",
                "int8" => "long",
                "float4" => "float",
                "float8" => "double",
                "text" or "varchar" or "bpchar" => "string",
                "bytea" => "byte[]",
                "uuid" => "global::System.Guid",
                "timestamp" => "global::System.DateTime",
                "inet" => "global::System.Net.IPAddress",
                "json" or "jsonb" => "global::System.Text.Json.JsonDocument",
                "_varchar" or "_text" => "string[]",
                "_int8" => "long[]",
                _ => throw new InvalidDataException(
                    $"PostgreSQL object {Literal(schema)}.{Literal(name)}, column {Literal(column)}: unsupported schema type {Literal(declaration)}.")
            });

            var notNull = field.Attnotnull;
            var suffix = notNull ? "" : "?";
            var initializer = notNull && type is ("string" or "byte[]" or "string[]" or "long[]" or "global::System.Net.IPAddress" or "global::System.Text.Json.JsonDocument") ? " = null!;" : "";
            if (row.Relkind is 'r' or 'p' or 'f' && keys.TryGetValue(id, out var key) &&
                key.Indkey.AsSpan(0, key.Indnkeyatts).Contains(field.Attnum))
                source.Append("    [global::System.ComponentModel.DataAnnotations.Key]\n");
            var property = publicContext
                ? RepositoryContextSource.Identifier(renameProperty?.Invoke(name, column) ?? column, "Field", usedProperties)
                : FormattableString.Invariant($"Column{++columnNumber}");
            source.Append(CultureInfo.InvariantCulture,
                $"    [global::System.ComponentModel.DataAnnotations.Schema.Column({Literal(column)}, TypeName = {Literal(declaration)})]\n    public {type}{suffix} {property} {{ get; set; }}{initializer}\n");
        }

        if (previous.HasValue)
            source.Append("}\n");

        // An unsupported declaration fails before opening any output file.
        RepositoryContextSource.Append(source, contextName, mappings, publicContext);
        if (splitModels)
        {
            RepositoryContextFiles.Split(source.ToString(), destination);
            return;
        }
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Guid.NewGuid():N}.tmp");
        var ownsTemporary = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                ownsTemporary = true;
                using var writer = new StreamWriter(stream);
                writer.Write(source.ToString());
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (ownsTemporary)
                File.Delete(temporary);
        }
    }

    private static readonly IComparer<byte[]> ByteOrder = Comparer<byte[]>.Create(
        (left, right) => left.AsSpan().SequenceCompareTo(right));

    private static string FormatDeclaration(string name, string? builtIn, int modifier) => builtIn switch
    {
        "bool" => "boolean",
        "int2" => "smallint",
        "int4" => "integer",
        "int8" => "bigint",
        "float4" => "real",
        "float8" => "double precision",
        "timestamp" => "timestamp without time zone",
        "_varchar" => "character varying[]",
        "_text" => "text[]",
        "_int8" => "bigint[]",
        "varchar" when modifier >= 4 => FormattableString.Invariant($"character varying({modifier - 4})"),
        "varchar" => "character varying",
        "bpchar" when modifier >= 4 => FormattableString.Invariant($"character({modifier - 4})"),
        _ => name
    };

    private static string Literal(string text)
    {
        var result = new StringBuilder("\"");
        foreach (var character in text)
        {
            if (character is >= ' ' and <= '~' and not '"' and not '\\')
                result.Append(character);
            else
                result.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
        }
        return result.Append('"').ToString();
    }
}
