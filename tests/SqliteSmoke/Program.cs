using Microsoft.Data.Sqlite;
using Nextorm.Contexts.Sqlite;

var directory = Directory.CreateTempSubdirectory();
try
{
    var database = Path.Combine(directory.FullName, "sample.db");
    var output = Path.Combine(directory.FullName, "Mappings.cs");
    File.WriteAllText(Path.Combine(directory.FullName, "Nextorm.Extensions.SmokeFixture.csproj"), "<Project />");
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
    if (!File.ReadAllText(output).Contains("class SmokeFixtureRepositoryContext", StringComparison.Ordinal))
        throw new InvalidOperationException("Expected the context name to come from the project, not the database.");
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

    SqliteSchemaGenerator.Scaffold(database, output, "Customized", "TestContext", databaseVersion: null,
        include: table => table == "keyed",
        entityName: _ => "SelectedRow",
        propertyName: (_, _) => "RowId",
        clrType: (_, _) => "int");
    var models = Directory.GetFiles(Path.Combine(directory.FullName, "Models"), "*.g.cs");
    if (models.Length != 1 || Path.GetFileName(models[0]) != "SelectedRow.g.cs" ||
        !File.ReadAllText(models[0]).Contains("public int? RowId", StringComparison.Ordinal) ||
        !File.ReadAllText(models[0]).Contains("Column(\"id\"", StringComparison.Ordinal))
        throw new InvalidOperationException("Scaffold customizations did not preserve the SQL mapping.");

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
