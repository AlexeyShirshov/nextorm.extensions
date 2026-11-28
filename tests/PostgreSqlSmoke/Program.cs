using Nextorm.Contexts.PostgreSql;
using Nextorm.Extensions.PostgreSql.PgCatalog;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;

// Run against a fresh disposable database, with the generated file outside the checkout.
if (args.Length != 1 || Environment.GetEnvironmentVariable("POSTGRESQL_SMOKE_CONNECTION") is not { } connectionString)
    throw new ArgumentException("Supply the disposable fixture connection and one output path.");

using var connection = new NpgsqlConnection(connectionString);
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = """
    CREATE SCHEMA mappings_fixture;
    CREATE TABLE mappings_fixture.keyed (first integer, second integer, PRIMARY KEY (second, first));
    CREATE TABLE mappings_fixture.keyless ("odd""column" text NOT NULL, qty bigint, points integer[] NOT NULL, durations integer[]);
    CREATE VIEW mappings_fixture.readable AS SELECT "odd""column", qty FROM mappings_fixture.keyless;
    """;
command.ExecuteNonQuery();
PostgreSqlSchemaGenerator.Generate(connectionString, args[0]);
var source = File.ReadAllText(args[0]);
var keyed = Body("keyed");
var keyless = Body("keyless");
var view = Body("readable");
Require(keyed.Split("DataAnnotations.Key]", StringSplitOptions.None).Length == 3, "Two declared key columns");
foreach (var body in new[] { keyless, view })
{
    Require(body.Contains("Column(\"odd\\u0022column\"", StringComparison.Ordinal), "Escaped column name");
    Require(body.Contains("Column(\"qty\"", StringComparison.Ordinal), "Second column");
    Require(!body.Contains("DataAnnotations.Key]", StringComparison.Ordinal), "No fabricated key");
}
Require(keyless.Contains("Column(\"points\", TypeName = \"integer[]\")]\n    public int[] Column3 { get; set; } = null!;", StringComparison.Ordinal),
    "Non-null PostgreSQL integer array mapping");
Require(keyless.Contains("Column(\"durations\", TypeName = \"integer[]\")]\n    public int[]? Column4 { get; set; }", StringComparison.Ordinal),
    "Nullable PostgreSQL integer array mapping");
Console.WriteLine("Case 1: table, view, both column mappings, and key assertions completed.");

command.CommandText = "CREATE TABLE mappings_fixture.z_unknown (amount numeric);";
command.ExecuteNonQuery();
var previous = File.ReadAllBytes(args[0]);
try
{
    PostgreSqlSchemaGenerator.Generate(connectionString, args[0]);
    throw new InvalidOperationException("Unsupported type was not refused.");
}
catch (InvalidDataException exception) when (
    exception.Message.Contains("""PostgreSQL object "mappings_fixture"."z_unknown", column "amount": unsupported schema type "numeric".""",
        StringComparison.Ordinal))
{
    Require(previous.AsSpan().SequenceEqual(File.ReadAllBytes(args[0])), "Previous source must remain byte-identical");
}
Console.WriteLine("Case 2: reason-specific refusal with previous source preserved.");

command.CommandText = """
    DROP TABLE mappings_fixture.z_unknown;
    CREATE TABLE mappings_fixture.futo_backups_schedule (ordering uuid[] NOT NULL);
    CREATE TABLE mappings_fixture.app_dates (event_date date NOT NULL);
    """;
command.ExecuteNonQuery();
var directory = Directory.CreateTempSubdirectory();
try
{
    var filtered = Path.Join(directory.FullName, "FilteredContext.cs");
    PostgreSqlSchemaGenerator.Scaffold(connectionString, filtered, "Fixtures.Mappings", "FilteredContext",
        include: static name => !name.StartsWith("futo_backups_", StringComparison.Ordinal),
        clrType: static (_, declared) => declared == "date" ? "global::System.DateOnly" : null,
        converterType: static (_, declared) => declared == "date" ? "FixtureDateConverter" : null);
    var model = File.ReadAllText(Path.Join(directory.FullName, "Models", "AppDates.g.cs"));
    Require(model.Contains("public global::System.DateOnly EventDate", StringComparison.Ordinal) &&
        model.Contains("typeof(FixtureDateConverter)", StringComparison.Ordinal), "Filtered scaffold preserves type and converter overrides");
    Require(!File.ReadAllText(filtered).Contains("FutoBackupsSchedule", StringComparison.Ordinal),
        "Excluded backup schedule should not be generated");
}
finally
{
    directory.Delete(recursive: true);
}
Console.WriteLine("Case 3: filtered objects and custom type conversion.");

command.CommandText = """
    CREATE SCHEMA catalog_smoke;
    CREATE TYPE catalog_smoke.mood AS ENUM ('low', 'high');
    CREATE TABLE catalog_smoke.parent (id integer PRIMARY KEY);
    CREATE TABLE catalog_smoke.child (
        id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        parent_id integer REFERENCES catalog_smoke.parent ON UPDATE CASCADE ON DELETE SET NULL,
        base_value integer DEFAULT 42,
        calculated integer GENERATED ALWAYS AS (base_value + 1) STORED
    );
    CREATE UNIQUE INDEX child_expression ON catalog_smoke.child ((abs(base_value)))
        INCLUDE (parent_id) WHERE base_value > 0;
    CREATE TABLE catalog_smoke.partitioned (id integer) PARTITION BY RANGE (id);
    CREATE TABLE catalog_smoke.partition_default PARTITION OF catalog_smoke.partitioned DEFAULT;
    CREATE SEQUENCE catalog_smoke.sequence AS integer START 7 INCREMENT 3 CYCLE;
    """;
command.ExecuteNonQuery();

using var db = new PostgresDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
long catalogSchemaId = db.From<PgNamespace>().Where(row => row.Name == "catalog_smoke").ToList().Single().Oid;
var relations = db.From<PgRelation>()
    .Where(row => row.Namespace == PgCatalogFunctions.ObjectId(catalogSchemaId)).ToList();
long childId = relations.Single(row => row.Name == "child").Oid;
var columns = db.From<PgAttribute>().Where(row => row.Relation == PgCatalogFunctions.ObjectId(childId)).ToList();
var generated = columns.Single(row => row.Name == "calculated");
Require(columns.Single(row => row.Name == "id").Identity == 'a' && generated.Generated == 's',
    "Identity and generated column catalog codes");
var defaults = db.AttributeDefaultExpressions(childId).ToList();
Require(defaults.Any(row => row.Expression == "42") &&
    defaults.Single(row => row.Number == generated.Number).Expression.Contains("base_value", StringComparison.Ordinal),
    "SQL text for default and generated expressions");

var constraints = db.From<PgConstraint>().Where(row => row.Relation == PgCatalogFunctions.ObjectId(childId)).ToList();
var foreignKey = constraints.Single(row => row.Kind == 'f');
Require(foreignKey.Keys is [2] && foreignKey.ReferencedKeys is [1] && foreignKey.OnUpdate == 'c' && foreignKey.OnDelete == 'n',
    "Foreign key column numbers and action codes");
var indexes = db.From<PgIndexMetadata>().Where(row => row.Relation == PgCatalogFunctions.ObjectId(childId)).ToList();
Require(indexes.Any(row => row.IsPrimary && row.IsUnique && row.Keys is [1]) &&
    indexes.Any(row => row.IsUnique && row.KeyCount == 1 && row.Keys is [0, 2]),
    "Primary key, expression key, and included column");

var partition = relations.Single(row => row.Name == "partition_default");
long partitionId = partition.Oid;
long partitionedId = relations.Single(row => row.Name == "partitioned").Oid;
var partitioning = db.From<PgPartitionedTable>()
    .Where(row => row.Relation == PgCatalogFunctions.ObjectId(partitionedId)).ToList().Single();
var inheritance = db.From<PgInheritance>()
    .Where(row => row.Relation == PgCatalogFunctions.ObjectId(partitionId)).ToList().Single();
Require(partition.IsPartition && partitioning.Strategy == 'r' && partitioning.Keys is [1] &&
    partitioning.DefaultPartition == partition.Oid && inheritance.Parent == partitionedId,
    "Partition strategy, default, and parent relation");

long moodId = db.From<PgType>()
    .Where(row => row.Namespace == PgCatalogFunctions.ObjectId(catalogSchemaId) && row.Name == "mood").ToList().Single().Oid;
var labels = db.From<PgEnum>().Where(row => row.Type == PgCatalogFunctions.ObjectId(moodId))
    .OrderBy(row => row.SortOrder).Select(row => row.Label).ToList();
Require(labels.SequenceEqual(["low", "high"]), "Enum label order");
long sequenceId = relations.Single(row => row.Name == "sequence").Oid;
var sequence = db.From<PgSequence>().Where(row => row.Relation == PgCatalogFunctions.ObjectId(sequenceId)).ToList().Single();
Require(sequence is { Start: 7, Increment: 3, Cycle: true }, "Sequence settings");
Console.WriteLine("Case 4: public catalog models and decoded metadata.");
Console.WriteLine("Selected: 4; completed: 4; failed: 0; skipped: 0.");

string Body(string name)
{
    var start = source.IndexOf($"Table(\"{name}\", Schema = \"mappings_fixture\")", StringComparison.Ordinal);
    Require(start >= 0, $"{name} mapping");
    return source[start..source.IndexOf("\n}", start, StringComparison.Ordinal)];
}

static void Require(bool condition, string reason)
{
    if (!condition)
        throw new InvalidOperationException(reason);
}
