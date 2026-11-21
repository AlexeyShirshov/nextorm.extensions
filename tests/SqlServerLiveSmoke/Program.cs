using Microsoft.Data.SqlClient;
using Nextorm.Contexts.SqlServer;

var password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
if (args.Length != 1 || password is null ||
    Environment.GetEnvironmentVariable("NEXTORM_STEP13_FIXTURE") != "disposable")
    throw new InvalidOperationException("This smoke requires the isolated disposable fixture.");

var settings = new SqlConnectionStringBuilder
{
    DataSource = "tcp:127.0.0.1,1433",
    InitialCatalog = "master",
    UserID = "sa",
    Password = password,
    TrustServerCertificate = true,
    Pooling = false,
    ConnectTimeout = 2
};
using var connection = new SqlConnection(settings.ConnectionString);
for (var attempt = 0; ; attempt++)
{
    try
    {
        connection.Open();
        break;
    }
    catch (SqlException exception) when (attempt < 29 && exception.Number is -2 or 0 or 53 or 10061 or 18456)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
using var command = connection.CreateCommand();
command.CommandText = "CREATE DATABASE step13_fixture;";
command.ExecuteNonQuery();
try
{
    connection.ChangeDatabase("step13_fixture");
    command.CommandText = """
        CREATE TABLE dbo.keyed (id int NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.keyless ([odd"name] nvarchar(20) NOT NULL, qty bigint NULL);
        """;
    command.ExecuteNonQuery();
    command.CommandText = """
        CREATE VIEW dbo.[view] AS SELECT [odd"name], qty FROM dbo.keyless;
        """;
    command.ExecuteNonQuery();
    settings.InitialCatalog = "step13_fixture";
    SqlServerSchemaGenerator.Generate(settings.ConnectionString, args[0]);
    var source = File.ReadAllText(args[0]);
    var keyed = Body("keyed");
    var keyless = Body("keyless");
    var view = Body("view");
    Require(keyed.Contains("Description(\"SQL Server TABLE\")", StringComparison.Ordinal) &&
        keyless.Contains("Description(\"SQL Server TABLE\")", StringComparison.Ordinal) &&
        view.Contains("Description(\"SQL Server VIEW\")", StringComparison.Ordinal), "catalog object kind");
    Require(keyed.Contains("DataAnnotations.Key]", StringComparison.Ordinal) &&
        keyed.Contains("Column(\"id\", TypeName = \"int\")]\n    public int Column1", StringComparison.Ordinal), "keyed id");
    foreach (var body in new[] { keyless, view })
    {
        Require(!body.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "fabricated key");
        Require(body.Contains("Column(\"odd\\u0022name\", TypeName = \"nvarchar(20)\")]\n    public string Column1", StringComparison.Ordinal), "first projected column and nullability");
        Require(body.Contains("Column(\"qty\", TypeName = \"bigint\")]\n    public long? Column2", StringComparison.Ordinal), "second projected column and nullability");
    }
    Console.WriteLine("Case 1 complete: live table/view kind, schema identity, columns and key.");

    command.CommandText = "CREATE TABLE dbo.unsafe (amount decimal(38,0) NOT NULL);";
    command.ExecuteNonQuery();
    var previous = File.ReadAllBytes(args[0]);
    try
    {
        SqlServerSchemaGenerator.Generate(settings.ConnectionString, args[0]);
        throw new InvalidOperationException("Unsafe catalog type was not refused.");
    }
    catch (InvalidDataException exception) when (
        exception.Message.Contains("""SQL Server object "dbo"."unsafe", column "amount": unsupported schema type "decimal(38,0)".""",
            StringComparison.Ordinal))
    {
        Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "prior output changed");
    }
    Console.WriteLine("Case 2 complete: live unsafe type refused; previous output byte-identical.");
    Console.WriteLine("Selected: 2; completed: 2; failed: 0; skipped: 0.");

    string Body(string name)
    {
        var marker = $"Table(\"{name}\", Schema = \"dbo\")";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Require(start >= 0 && source.LastIndexOf(marker, StringComparison.Ordinal) == start, "unique identity: " + name);
        var kindStart = source.LastIndexOf("\n[global::System.ComponentModel.Description", start, StringComparison.Ordinal);
        var previousEnd = source.LastIndexOf("\n}", start, StringComparison.Ordinal);
        return source[(kindStart > previousEnd ? kindStart : start)..source.IndexOf("\n}", start, StringComparison.Ordinal)];
    }
}
finally
{
    connection.ChangeDatabase("master");
    command.CommandText = "DROP DATABASE step13_fixture;";
    command.ExecuteNonQuery();
}

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
