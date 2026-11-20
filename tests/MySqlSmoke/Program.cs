using Nextorm.Contexts.MySql;

if (args.Length != 1)
    throw new ArgumentException("Supply one generated C# output path.");

// Synthetic MySQL catalog descriptions, not table data.
MySqlColumn[] columns = [new("label\"line", "varchar(40)", true), new("id", "bigint unsigned", false)];
MySqlSchemaGenerator.Generate("fixture", "ordinary", columns, "id", args[0]);
var source = File.ReadAllText(args[0]);
var label = source.IndexOf("Column(\"label\\u0022line\", TypeName = \"varchar(40)\")", StringComparison.Ordinal);
var key = source.IndexOf("DataAnnotations.Key]", StringComparison.Ordinal);
var id = source.IndexOf("Column(\"id\", TypeName = \"bigint unsigned\")", StringComparison.Ordinal);
Require(source.Contains("Table(\"ordinary\", Schema = \"fixture\")", StringComparison.Ordinal), "table mapping");
Require(label >= 0 && key > label && id > key && source.LastIndexOf("DataAnnotations.Key]", StringComparison.Ordinal) == key,
    "exactly one annotation on the declared key, in catalog column order");
Require(source.Contains("public string? Column1", StringComparison.Ordinal) &&
    source.Contains("public ulong Column2", StringComparison.Ordinal), "column types and nullability");
Console.WriteLine("Case 1 complete: ordinary table, two columns and one declared key.");

var previous = File.ReadAllBytes(args[0]);
try
{
    MySqlSchemaGenerator.Generate("fixture", "ordinary", [..columns, new("cost", "decimal(65,30)", false)], "id", args[0]);
    throw new InvalidOperationException("Unsafe type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""MySQL object "fixture"."ordinary", column "cost": unsupported schema type "decimal(65,30)".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "previous output preserved");
}
Console.WriteLine("Case 2 complete: reason-specific refusal and byte-identical prior output.");
Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
