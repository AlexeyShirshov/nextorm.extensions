using Nextorm.Contexts.PostgreSql;
using Npgsql;

// Run against a fresh disposable database, with the generated file outside the checkout.
if (args.Length != 1 || Environment.GetEnvironmentVariable("POSTGRESQL_SMOKE_CONNECTION") is not { } connectionString)
    throw new ArgumentException("Supply the disposable fixture connection and one output path.");

using var connection = new NpgsqlConnection(connectionString);
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = """
    CREATE SCHEMA mappings_fixture;
    CREATE TABLE mappings_fixture.keyed (first integer, second integer, PRIMARY KEY (second, first));
    CREATE TABLE mappings_fixture.keyless ("odd""column" text NOT NULL, qty bigint, points integer[] NOT NULL, durations integer[]);
    CREATE VIEW mappings_fixture.readable AS SELECT "odd""column", qty FROM mappings_fixture.keyless;
    """;
command.ExecuteNonQuery();
PostgreSqlSchemaGenerator.Generate(connectionString, args[0]);
var source = File.ReadAllText(args[0]);
var keyed = Body("keyed");
var keyless = Body("keyless");
var view = Body("readable");
Require(keyed.Split("DataAnnotations.Key]", StringSplitOptions.None).Length == 3, "Two declared key columns");
foreach (var body in new[] { keyless, view })
{
    Require(body.Contains("Column(\"odd\\u0022column\"", StringComparison.Ordinal), "Escaped column name");
    Require(body.Contains("Column(\"qty\"", StringComparison.Ordinal), "Second column");
    Require(!body.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "No fabricated key");
}
Require(keyless.Contains("Column(\"points\", TypeName = \"integer[]\")]\n    public int[] Column3 { get; set; } = null!;", StringComparison.Ordinal),
    "Non-null PostgreSQL integer array mapping");
Require(keyless.Contains("Column(\"durations\", TypeName = \"integer[]\")]\n    public int[]? Column4 { get; set; }", StringComparison.Ordinal),
    "Nullable PostgreSQL integer array mapping");
Console.WriteLine("Case 1: table, view, both column mappings, and key assertions completed.");

command.CommandText = "CREATE TABLE mappings_fixture.z_unknown (amount numeric);";
command.ExecuteNonQuery();
var previous = File.ReadAllBytes(args[0]);
try
{
    PostgreSqlSchemaGenerator.Generate(connectionString, args[0]);
    throw new InvalidOperationException("Unsupported type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""PostgreSQL object "mappings_fixture"."z_unknown", column "amount": unsupported schema type "numeric".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "Previous source must remain byte-identical");
}
Console.WriteLine("Case 2: reason-specific refusal with previous source preserved.");

command.CommandText = """
    DROP TABLE mappings_fixture.z_unknown;
    CREATE TABLE mappings_fixture.futo_backups_schedule (ordering uuid[] NOT NULL);
    CREATE TABLE mappings_fixture.app_dates (event_date date NOT NULL);
    """;
command.ExecuteNonQuery();
var directory = Directory.CreateTempSubdirectory();
try
{
    var filtered = Path.Join(directory.FullName, "FilteredContext.cs");
    PostgreSqlSchemaGenerator.Scaffold(connectionString, filtered, "Fixtures.Mappings", "FilteredContext",
        include: static name => !name.StartsWith("futo_backups_", StringComparison.Ordinal),
        clrType: static (_, declared) => declared == "date" ? "global::System.DateOnly" : null,
        converterType: static (_, declared) => declared == "date" ? "FixtureDateConverter" : null);
    var model = File.ReadAllText(Path.Join(directory.FullName, "Models", "AppDates.g.cs"));
    Require(model.Contains("public global::System.DateOnly EventDate", StringComparison.Ordinal) &&
        model.Contains("typeof(FixtureDateConverter)", StringComparison.Ordinal), "Filtered scaffold preserves type and converter overrides");
    Require(!File.ReadAllText(filtered).Contains("FutoBackupsSchedule", StringComparison.Ordinal),
        "Excluded backup schedule should not be generated");
}
finally
{
    directory.Delete(recursive: true);
}
Console.WriteLine("Case 3: filtered objects and custom type conversion.");
Console.WriteLine("Selected: 3; completed: 3; failed: 0; skipped: 0.");

string Body(string name)
{
    var start = source.IndexOf($"Table(\"{name}\", Schema = \"mappings_fixture\")", StringComparison.Ordinal);
    Require(start >= 0, $"{name} mapping");
    return source[start..source.IndexOf("\n}", start, StringComparison.Ordinal)];
}

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
