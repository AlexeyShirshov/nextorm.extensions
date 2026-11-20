using MySqlConnector;
using Nextorm.Contexts.LiveCatalog;

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]))
    throw new ArgumentException("Supply one disposable local Unix socket and a generated-source output path.");

var settings = new MySqlConnectionStringBuilder { Server = args[0], UserID = Environment.UserName, Pooling = false };
using var connection = new MySqlConnection(settings.ConnectionString);
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = """
    CREATE DATABASE step7_fixture;
    CREATE TABLE step7_fixture.ordinary (label varchar(40), id mediumint unsigned PRIMARY KEY);
    """;
command.ExecuteNonQuery();

LiveCatalogGenerator.Generate(CatalogProvider.MariaDb, settings.ConnectionString, "step7_fixture", "ordinary", args[1]);
var source = File.ReadAllText(args[1]);
Require(source.Contains("Table(\"ordinary\", Schema = \"step7_fixture\")", StringComparison.Ordinal), "table identity");
Require(source.Contains("Column(\"label\", TypeName = \"varchar(40)\")]\n    public string? Column1", StringComparison.Ordinal), "nullable varchar");
Require(source.Contains("DataAnnotations.Key]\n    [global::System.ComponentModel.DataAnnotations.Schema.Column(\"id\", TypeName = \"mediumint(8) unsigned\")]\n    public uint Column2", StringComparison.Ordinal), "native key identity and unsigned type");
Console.WriteLine("Case 1 complete: live MariaDB catalog table, columns and declared key.");

command.CommandText = "ALTER TABLE step7_fixture.ordinary ADD COLUMN cost decimal(65,30) NOT NULL;";
command.ExecuteNonQuery();
var previous = File.ReadAllBytes(args[1]);
try
{
    LiveCatalogGenerator.Generate(CatalogProvider.MariaDb, settings.ConnectionString, "step7_fixture", "ordinary", args[1]);
    throw new InvalidOperationException("Unsupported catalog type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""MariaDB object "step7_fixture"."ordinary", column "cost": unsupported schema type "decimal(65,30)".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[1])), "previous output preserved");
}
Console.WriteLine("Case 2 complete: live catalog refusal with unchanged prior output.");
Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
