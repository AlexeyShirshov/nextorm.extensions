using Microsoft.Data.Sqlite;
using Nextorm.Contexts.Sqlite;

var directory = Directory.CreateTempSubdirectory();
try
{
    var database = Path.Combine(directory.FullName, "sample.db");
    var output = Path.Combine(directory.FullName, "Mappings.cs");
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = database,
        Pooling = false
    }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE keyed(id INTEGER PRIMARY KEY);
        CREATE TABLE keyless(label TEXT NOT NULL, qty INTEGER, twice INTEGER GENERATED ALWAYS AS (qty * 2) VIRTUAL);
        CREATE VIEW readable AS SELECT label, twice FROM keyless;
        """;
    command.ExecuteNonQuery();
    SqliteSchemaGenerator.Generate(database, output);
    var source = File.ReadAllText(output);
    foreach (var token in new[] { "Table(\"keyed\")", "Table(\"keyless\")", "Table(\"readable\")",
             "Column(\"id\"", "Column(\"qty\"", "Column(\"twice\"", "Column(\"label\"" })
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException($"Missing mapping: {token}");
    }
    if (source.Split("DataAnnotations.Key]", StringSplitOptions.None).Length != 2)
        throw new InvalidOperationException("Expected exactly one key annotation.");
    Console.WriteLine("Completed case: table/view, generated column and keyless mapping.");

    command.CommandText = "CREATE TABLE unknown_schema(ambiguous GEOMETRY);";
    command.ExecuteNonQuery();
    var previous = File.ReadAllBytes(output);
    try
    {
        SqliteSchemaGenerator.Generate(database, output);
        throw new InvalidOperationException("Unsupported declaration was not refused.");
    }
    catch (InvalidDataException exception) when (
        exception.Message.Contains("SQLite object \"unknown_schema\", column \"ambiguous\": unsupported schema type \"GEOMETRY\".",
            StringComparison.Ordinal))
    {
        if (!previous.AsSpan().SequenceEqual(File.ReadAllBytes(output)))
            throw new InvalidOperationException("A refusal changed the previous output.");
    }
    Console.WriteLine("Completed case: reason-specific refusal and byte-identical prior output.");
    Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");
}
finally
{
    directory.Delete(recursive: true);
}
