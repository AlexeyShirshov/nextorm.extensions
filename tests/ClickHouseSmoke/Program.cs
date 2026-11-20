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

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
