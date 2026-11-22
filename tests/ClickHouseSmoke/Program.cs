using Nextorm.Contexts.ClickHouse;

if (args.Length != 1)
    throw new ArgumentException("Supply one generated C# output path.");

// Native catalog descriptions only. No service, connection, or row data.
ClickHouseColumn[] columns = [new("id", "UInt64"), new("delta", "Int32"), new("payload\"raw", "String")];
ClickHouseSchemaGenerator.GenerateFromCatalog("fixtures", "native", columns, args[0]);
var source = File.ReadAllText(args[0]);
Require(source.Contains("Table(\"native\", Schema = \"fixtures\")", StringComparison.Ordinal), "table mapping");
Require(source.Contains("Column(\"id\", TypeName = \"UInt64\")]\n    public ulong Column1", StringComparison.Ordinal), "UInt64 mapping");
Require(source.Contains("Column(\"delta\", TypeName = \"Int32\")]\n    public int Column2", StringComparison.Ordinal), "Int32 mapping");
Require(source.Contains("Column(\"payload\\u0022raw\", TypeName = \"String\")]\n    public byte[] Column3", StringComparison.Ordinal), "native String mapping");
Require(!source.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "no inferred relational key");
Console.WriteLine("Case 1 complete: native integer and byte-preserving String mappings.");

var previous = File.ReadAllBytes(args[0]);
try
{
    ClickHouseSchemaGenerator.GenerateFromCatalog("fixtures", "native", [..columns, new("unsupported", "Array(Int32)")], args[0]);
    throw new InvalidOperationException("Unsupported type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""ClickHouse object "fixtures"."native", column "unsupported": unsupported schema type "Array(Int32)".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "previous output preserved");
}
Console.WriteLine("Case 2 complete: reason-specific refusal and byte-identical prior output.");
Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");

var catalogPath = Path.ChangeExtension(args[0], ".catalog.cs");
ClickHouseCatalogObject table = new("fixtures", "native", false, columns);
ClickHouseCatalogObject view = new("fixtures", "projection", true, [new("total", "Int64"), new("code", "UInt8")]);
ClickHouseSchemaGenerator.GenerateFromCatalog([table, view], catalogPath);
var catalog = File.ReadAllText(catalogPath);
var boundary = catalog.IndexOf("[global::System.ComponentModel.Description(\"ClickHouse VIEW\")]", StringComparison.Ordinal);
Require(boundary >= 0, "view kind missing");
Require(catalog[..boundary].Contains("Table(\"native\", Schema = \"fixtures\")", StringComparison.Ordinal)
    && catalog[..boundary].Contains("public ulong Column1", StringComparison.Ordinal)
    && catalog[..boundary].Contains("public int Column2", StringComparison.Ordinal)
    && catalog[..boundary].Contains("public byte[] Column3", StringComparison.Ordinal), "complete table mapping");
Require(catalog[boundary..].Contains("Table(\"projection\", Schema = \"fixtures\")", StringComparison.Ordinal)
    && catalog[boundary..].Contains("Column(\"total\", TypeName = \"Int64\")", StringComparison.Ordinal)
    && catalog[boundary..].Contains("Column(\"code\", TypeName = \"UInt8\")", StringComparison.Ordinal)
    && !catalog.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "complete keyless view and table");
Console.WriteLine("Case 3 complete: all columns across distinct keyless table and view mappings.");

var priorCatalog = File.ReadAllBytes(catalogPath);
try
{
    ClickHouseSchemaGenerator.GenerateFromCatalog([table, view with { Columns = [new("unsafe", "Array(Int32)")] }], catalogPath);
    throw new InvalidOperationException("Unsupported late-object type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""ClickHouse object "fixtures"."projection", column "unsafe": unsupported schema type "Array(Int32)".""",
        StringComparison.Ordinal))
{
    Require(priorCatalog.AsSpan().SequenceEqual(File.ReadAllBytes(catalogPath)), "prior multi-object output preserved");
}
Console.WriteLine("Case 4 complete: late-object refusal preserves the complete previous output.");
Console.WriteLine("Total selected: 4; completed: 4; failed: 0; skipped: 0.");

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
