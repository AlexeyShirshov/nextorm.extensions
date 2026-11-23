using MySqlConnector;
using Nextorm.Contexts.LiveCatalog;

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]))
    throw new ArgumentException("Supply the disposable fixture socket and output directory.");

var connection = new MySqlConnectionStringBuilder
{
    Server = args[0], UserID = "root", Pooling = false, ConnectionTimeout = 5, SslMode = MySqlSslMode.Disabled
};
var failures = 0;
foreach (var (name, view, keys) in new[]
{
    ("ordinary_view", true, 0), ("keyless_base", false, 0), ("keyed_base", false, 2)
})
{
    try
    {
        var path = Path.Combine(args[1], $"{name}.cs");
        LiveCatalogGenerator.Generate(CatalogProvider.MariaDb, connection.ConnectionString, "nextorm_s23", name, path);
        var source = File.ReadAllText(path);
        if (!source.Contains($"Table(\"{name}\", Schema = \"nextorm_s23\")", StringComparison.Ordinal)
            || source.Contains("Description(\"MariaDB VIEW\")", StringComparison.Ordinal) != view
            || source.Split("DataAnnotations.Key]", StringSplitOptions.None).Length - 1 != keys
            || source.Contains("CompositePrimaryKey", StringComparison.Ordinal))
            throw new InvalidDataException("Generated object kind or primary key did not match the fixture.");
        Console.WriteLine($"{name}: completed");
    }
    catch (InvalidDataException error)
    {
        failures++;
        Console.WriteLine($"{name}: {error.Message}");
    }
}
Console.WriteLine($"Selected: 3; completed: {3 - failures}; failed: {failures}; skipped: 0.");
return failures == 0 ? 0 : 1;
