using System.Text;
using System.Xml;
using Nextorm.Contexts.OData;

var metadata = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Metadata.xml"));
var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Models.g.cs"));
var actual = Generate(metadata);
if (actual.ReplaceLineEndings("\n") != expected.ReplaceLineEndings("\n"))
{
    throw new InvalidOperationException("Generated OData models differ from the compiled fixture.");
}

var version40 = metadata.Replace("Version=\"4.01\"", "Version=\"4.0\"", StringComparison.Ordinal);
if (actual != Generate(version40))
{
    throw new InvalidOperationException("The same CSDL should generate the same models in OData 4.0 and 4.01.");
}

ExpectRefusal<InvalidDataException>(metadata.Replace("Edm.Decimal", "Edm.Geography", StringComparison.Ordinal),
    "Unsupported OData type 'Edm.Geography' for 'Sample.Catalog.Product.Price'.");
ExpectRefusal<InvalidDataException>(metadata.Replace("record.Address", "Vocabulary.Address", StringComparison.Ordinal),
    "Unsupported OData type 'Vocabulary.Address'");
ExpectRefusal<InvalidDataException>(metadata.Replace("Catalog.Item", "Catalog.Product", StringComparison.Ordinal),
    "Circular OData inheritance");
ExpectRefusal<InvalidDataException>(metadata.Replace("Name=\"Product\"", "Name=\"Product;\"", StringComparison.Ordinal),
    "not a supported C# identifier");
ExpectRefusal<XmlException>("<!DOCTYPE Edmx><Edmx/>");

Console.WriteLine("OData smoke checks passed.");

static string Generate(string metadata)
{
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(metadata));
    var source = ODataModelGenerator.Generate(stream, "Fixtures.Models");
    if (!stream.CanRead)
    {
        throw new InvalidOperationException("The model generator closed the caller's stream.");
    }
    return source;
}

static void ExpectRefusal<TException>(string metadata, string? reason = null) where TException : Exception
{
    try
    {
        _ = Generate(metadata);
    }
    catch (TException error) when (reason is null || error.Message.Contains(reason, StringComparison.Ordinal))
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}: {reason}");
}
