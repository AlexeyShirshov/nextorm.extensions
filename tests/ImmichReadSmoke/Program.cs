using NextORM.Core;
using NextORM.Postgres;
using Nextorm.Extensions.Immich;
using Npgsql;

if (Environment.GetEnvironmentVariable("IMMICH_SMOKE_CONNECTION") is not { Length: > 0 } connectionString)
    throw new ArgumentException("Set IMMICH_SMOKE_CONNECTION to a disposable database migrated by Immich 3.4.2.");

using var connection = new NpgsqlConnection(connectionString);
connection.Open();
using var context = new PostgresDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
var repository = new ImmichPostgreSqlRepositoryContext(context);
using var transaction = connection.BeginTransaction();
((ITransactionManager)context).UseTransaction(transaction);

var id = Guid.NewGuid();
var clusterId = Guid.NewGuid();
var personGroupId = Guid.NewGuid();
var embedding = "[" + string.Join(',', new[] { "1", "-0.5" }.Concat(Enumerable.Repeat("0", 510))) + "]";
var now = DateTime.UtcNow;
var checksum = Enumerable.Repeat((byte)1, 20).ToArray();
context.CreateInsertBuilder<ClusterGroup>().Value(row => row.Id, clusterId).Insert();
context.CreateInsertBuilder<User>()
    .Value(row => row.Id, id)
    .Value(row => row.Email, $"{id:N}@example.invalid")
    .Value(row => row.ClusterGroupId, clusterId)
    .Insert();
context.CreateInsertBuilder<PersonGroup>()
    .Value(row => row.Id, personGroupId)
    .Value(row => row.ClusterGroupId, clusterId)
    .Insert();
context.CreateInsertBuilder<Asset>()
    .Values(context.From<ClusterGroup>().Where(row => row.Id == clusterId), _ => new Asset
    {
        Id = id,
        OwnerId = id,
        Type = "IMAGE",
        OriginalPath = "/smoke.jpg",
        FileCreatedAt = now,
        FileModifiedAt = now,
        Checksum = checksum,
        ChecksumAlgorithm = ImmichSeedFunctions.ChecksumAlgorithm("sha1"),
        OriginalFileName = "smoke.jpg",
        LocalDateTime = now
    })
    .Insert();
context.CreateInsertBuilder<AssetFace>()
    .Value(row => row.Id, id)
    .Value(row => row.AssetId, id)
    .Insert();
context.CreateInsertBuilder<FaceSearch>()
    .Value(row => row.FaceId, id)
    .Value(row => row.Embedding, PgVector.Parse(embedding))
    .Insert();
context.CreateInsertBuilder<Person>()
    .Value(row => row.OwnerId, id)
    .Value(row => row.PersonGroupId, personGroupId)
    .Value(row => row.Name, "Example")
    .Value(row => row.ThumbnailPath, "")
    .Value(row => row.BirthDate, new DateOnly(2000, 2, 29))
    .Insert();

var row = repository.FaceSearch.ToList().Single(face => face.FaceId == id);
PgVector actual = row.Embedding;
if (actual.ToString() != embedding)
    throw new InvalidOperationException("The 512 embedding values did not match the fixture.");
if (repository.Person.ToList().Single(person => person.OwnerId == id && person.PersonGroupId == personGroupId)
    .BirthDate != new DateOnly(2000, 2, 29))
    throw new InvalidOperationException("The Immich birth date did not match the fixture.");

transaction.Rollback();
Console.WriteLine("Immich face embedding and birth date read through Nextorm without a plugin. Fixture rolled back.");

internal static class ImmichSeedFunctions
{
    [SqlFunction("asset_checksum_algorithm_enum", Schema = "public")]
    public static string ChecksumAlgorithm(string value) => throw new NotSupportedException();
}
