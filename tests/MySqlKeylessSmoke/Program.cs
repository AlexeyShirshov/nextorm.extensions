using Nextorm.Contexts.MySql;

if (args.Length != 1)
    throw new ArgumentException("Supply a generated C# output path.");

// Catalog metadata only. A column named id does not imply a declared key.
MySqlColumn[] columns = [new("id", "bigint unsigned", false), new("label", "varchar(40)", true)];
MySqlSchemaGenerator.Generate("fixture", "ordinary_keyless", columns, null, args[0]);
var source = File.ReadAllText(args[0]);
Require(source.Contains("Table(\"ordinary_keyless\", Schema = \"fixture\")", StringComparison.Ordinal), "table identity");
Require(source.Contains("Column(\"id\", TypeName = \"bigint unsigned\")]\n    public ulong Column1", StringComparison.Ordinal), "id identity and type");
Require(source.Contains("Column(\"label\", TypeName = \"varchar(40)\")]\n    public string? Column2", StringComparison.Ordinal), "label identity and type");
Require(!source.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "fabricated primary key");
Console.WriteLine("Case 1: keyless table and both columns emitted without any primary key.");

var previous = File.ReadAllBytes(args[0]);
try
{
    MySqlSchemaGenerator.Generate("fixture", "ordinary_keyless", [..columns, new("cost", "decimal(65,30)", false)], null, args[0]);
    throw new InvalidOperationException("Unsafe declaration was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""MySQL object "fixture"."ordinary_keyless", column "cost": unsupported schema type "decimal(65,30)".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "previous output changed");
}
Console.WriteLine("Case 2: reason-specific refusal leaves prior generated source unchanged.");
Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");
return 0;

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
