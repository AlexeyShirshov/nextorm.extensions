using System.ComponentModel.DataAnnotations.Schema;
using MySqlConnector;
using Nextorm.Contexts.LiveCatalog;
using NextORM.Core;
using NextORM.MySql;

if (args.Length == 2 && args[0] == "--ready" && Path.IsPathFullyQualified(args[1]))
{
    Environment.ExitCode = CheckReadiness(args[1]);
    return;
}

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]))
    throw new ArgumentException("Supply a disposable local Unix socket and a generated source output path.");

var settings = new MySqlConnectionStringBuilder { Server = args[0], UserID = "root", Pooling = false };
using var connection = new MySqlConnection(settings.ConnectionString);
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = """
    CREATE DATABASE step11_fixture;
    CREATE TABLE step11_fixture.ordinary (id bigint unsigned PRIMARY KEY, label varchar(40));
    CREATE TABLE step11_fixture.keyless (label varchar(40) NOT NULL, quantity int NULL);
    CREATE VIEW step11_fixture.projected AS SELECT label AS description, id AS identifier FROM step11_fixture.ordinary;
    """;
command.ExecuteNonQuery();

try
{
    LiveCatalogGenerator.Generate(CatalogProvider.MySql, settings.ConnectionString, "step11_fixture", "projected", args[1]);
    var source = File.ReadAllText(args[1]);
    Require(source.Contains("Description(\"MySQL VIEW\")", StringComparison.Ordinal), "view kind");
    Require(source.Contains("Table(\"projected\", Schema = \"step11_fixture\")", StringComparison.Ordinal), "view identity");
    Require(source.Contains("Column(\"description\", TypeName = \"varchar(40)\")]\n    public string? Column1", StringComparison.Ordinal), "first projection");
    Require(source.Contains("Column(\"identifier\", TypeName = \"bigint unsigned\")]\n    public ulong Column2", StringComparison.Ordinal), "second projection");
    Require(!source.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "invented view key");
    Console.WriteLine("Case 1 complete: live MySQL view kind, projected columns, no key.");

    var tablePath = Path.Combine(Path.GetDirectoryName(args[1])!, "..", "Table.cs");
    LiveCatalogGenerator.Generate(CatalogProvider.MySql, settings.ConnectionString, "step11_fixture", "ordinary", tablePath);
    var table = File.ReadAllText(tablePath);
    Require(table.Contains("DataAnnotations.Key]", StringComparison.Ordinal) &&
        !table.Contains("Description(\"MySQL VIEW\")", StringComparison.Ordinal), "live keyed table behavior");
    var scaffoldDirectory = Path.Combine(Path.GetDirectoryName(args[1])!, "Scaffold");
    LiveCatalogGenerator.ScaffoldMySql(settings.ConnectionString, "step11_fixture",
        Path.Combine(scaffoldDirectory, "SchemaContext.cs"), "Fixture", "SchemaContext");
    var scaffoldModels = Path.Combine(scaffoldDirectory, "Models");
    Require(File.ReadAllText(Path.Combine(scaffoldModels, "Ordinary.g.cs"))
            .Contains("DataAnnotations.Key]", StringComparison.Ordinal) &&
        !File.ReadAllText(Path.Combine(scaffoldModels, "Keyless.g.cs"))
            .Contains("DataAnnotations.Key]", StringComparison.Ordinal) &&
        !File.Exists(Path.Combine(scaffoldModels, "Projected.g.cs")),
        "schema scaffolding retains keyless tables and excludes MySQL views");
    Console.WriteLine("Case 2 complete: live MySQL base table retains declared key.");

    var keylessPath = Path.Combine(Path.GetDirectoryName(args[1])!, "Keyless.cs");
    LiveCatalogGenerator.Generate(CatalogProvider.MySql, settings.ConnectionString, "step11_fixture", "keyless", keylessPath);
    var keyless = File.ReadAllText(keylessPath);
    Require(keyless.Contains("Table(\"keyless\", Schema = \"step11_fixture\")", StringComparison.Ordinal), "keyless table identity");
    Require(keyless.Contains("Column(\"label\", TypeName = \"varchar(40)\")]\n    public string Column1", StringComparison.Ordinal), "keyless label");
    Require(keyless.Contains("Column(\"quantity\", TypeName = \"int\")]\n    public int? Column2", StringComparison.Ordinal), "keyless quantity");
    Require(!keyless.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "invented table key");
    Console.WriteLine("Case 3 complete: live MySQL base table without a primary key retains its columns.");
    Console.WriteLine("Selected: 3; completed: 3; failed: 0; skipped: 0.");
}
finally
{
    command.CommandText = "DROP DATABASE step11_fixture;";
    command.ExecuteNonQuery();
}

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}

static int CheckReadiness(string socketPath)
{
    try
    {
        var settings = new MySqlConnectionStringBuilder
        {
            Server = socketPath,
            UserID = "root",
            Pooling = false,
            ConnectionTimeout = 2
        };
        using var connection = new MySqlConnection(settings.ConnectionString);
        connection.Open();
        connection.ChangeDatabase("performance_schema");

        using var context = new MySqlDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
        var value = context.From<MySqlGlobalVariable>()
            .Where(row => row.Name == "skip_networking")
            .Select(row => row.Value)
            .ToList()
            .SingleOrDefault();
        if (value is not ("ON" or "OFF"))
        {
            Console.Error.WriteLine("Readiness query did not return a skip_networking setting.");
            return 1;
        }

        var skipNetworking = value == "ON";
        Console.WriteLine($"READY mysql_version={connection.ServerVersion} skip_networking={(skipNetworking ? 1 : 0)}");
        return skipNetworking ? 1 : 0;
    }
    catch (Exception error)
    {
        // Keep readiness diagnostics useful without ever printing connection details.
        Console.Error.WriteLine($"MySQL is not ready ({error.GetType().Name}).");
        return 75;
    }
}

[Table("global_variables", Schema = "performance_schema")]
internal sealed class MySqlGlobalVariable
{
    [Column("VARIABLE_NAME")] public string Name { get; set; } = "";
    [Column("VARIABLE_VALUE")] public string? Value { get; set; }
}
