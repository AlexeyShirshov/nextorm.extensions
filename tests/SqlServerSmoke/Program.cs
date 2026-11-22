using System.Data;
using Nextorm.Contexts.SqlServer;

if (args.Length != 1)
    throw new ArgumentException("Supply one generated-source destination.");

// Synthetic catalog records only; this executable never opens a database connection.
using var catalog = new DataTable();
var types = new[] { typeof(int), typeof(string), typeof(string), typeof(string),
    typeof(string), typeof(string), typeof(bool), typeof(byte), typeof(byte), typeof(short),
    typeof(int), typeof(bool) };
foreach (var type in types)
    catalog.Columns.Add(null, type);

catalog.Rows.Add(1, "dbo", "keyed", "id", "sys", "int", false, (byte)10, (byte)0, (short)4, 1, false);
catalog.Rows.Add(2, "dbo", "keyless", "odd\"name", "sys", "nvarchar", false, (byte)0, (byte)0, (short)40, 0, false);
catalog.Rows.Add(2, "dbo", "keyless", "qty", "sys", "bigint", true, (byte)19, (byte)0, (short)8, 0, false);
catalog.Rows.Add(3, "dbo", "view", "odd\"name", "sys", "nvarchar", false, (byte)0, (byte)0, (short)40, 0, false);
catalog.Rows.Add(3, "dbo", "view", "qty", "sys", "bigint", true, (byte)19, (byte)0, (short)8, 0, false);
using (var reader = catalog.CreateDataReader())
    SqlServerSchemaGenerator.WriteMappings(reader, args[0]);
var source = File.ReadAllText(args[0]);
Require(Body("keyed").Contains("DataAnnotations.Key]", StringComparison.Ordinal), "Declared key missing");
foreach (var name in new[] { "keyless", "view" })
{
    var body = Body(name);
    Require(body.Contains("Column(\"odd\\u0022name\", TypeName = \"nvarchar(20)\")", StringComparison.Ordinal), "Escaped column missing");
    Require(body.Contains("Column(\"qty\", TypeName = \"bigint\")", StringComparison.Ordinal), "Second column missing");
    Require(!body.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "Fabricated key");
}
Console.WriteLine("Case 1 complete: catalog mapping and key assertions.");

catalog.Rows.Add(4, "dbo", "unsafe", "amount", "sys", "decimal", false, (byte)38, (byte)0, (short)17, 0, false);
var previous = File.ReadAllBytes(args[0]);
try
{
    using var reader = catalog.CreateDataReader();
    SqlServerSchemaGenerator.WriteMappings(reader, args[0]);
    throw new InvalidOperationException("Unsafe type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""SQL Server object "dbo"."unsafe", column "amount": unsupported schema type "decimal(38,0)".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "Prior output changed");
}
Console.WriteLine("Case 2 complete: reason-specific refusal, byte-identical prior output.");
Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");

catalog.Clear();
catalog.Rows.Add(1, "dbo", "composite", "first_column", "sys", "int", false, (byte)10, (byte)0, (short)4, 2, false);
catalog.Rows.Add(1, "dbo", "composite", "payload", "sys", "int", false, (byte)10, (byte)0, (short)4, 0, false);
catalog.Rows.Add(1, "dbo", "composite", "last_column", "sys", "int", false, (byte)10, (byte)0, (short)4, 1, false);
catalog.Rows.Add(2, "dbo", "after_composite", "id", "sys", "int", false, (byte)10, (byte)0, (short)4, 1, false);
var compositePath = Path.ChangeExtension(args[0], ".composite.cs");
using (var reader = catalog.CreateDataReader())
    SqlServerSchemaGenerator.WriteMappings(reader, compositePath);
var composite = File.ReadAllText(compositePath);
const string key = "CompositePrimaryKey(nameof(Object1.Column3), nameof(Object1.Column1))";
Require(composite.Split("[CompositePrimaryKey(", StringSplitOptions.None).Length == 2 &&
    composite.Contains($"[{key}]\n[global::System.ComponentModel.DataAnnotations.Schema.Table(\"composite\"", StringComparison.Ordinal),
    "Expected exactly one ordered composite primary key on the composite table");
Console.WriteLine("Composite scenario: selected 1; completed 1; failed 0; skipped 0.");

string Body(string name)
{
    var start = source.IndexOf($"Table(\"{name}\", Schema = \"dbo\")", StringComparison.Ordinal);
    Require(start >= 0, $"Missing {name} mapping");
    return source[start..source.IndexOf("\n}", start, StringComparison.Ordinal)];
}

static void Require(bool condition, string failure)
{
    if (!condition)
        throw new InvalidOperationException(failure);
}
