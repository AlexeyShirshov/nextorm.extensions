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
using var seed = new NpgsqlCommand("""
    INSERT INTO public.cluster_group (id) VALUES (@clusterId);
    INSERT INTO public."user" (id, email, "clusterGroupId") VALUES (@id, @email, @clusterId);
    INSERT INTO public.person_group (id, "clusterGroupId") VALUES (@personGroupId, @clusterId);
    INSERT INTO public.asset (id, "ownerId", type, "originalPath", "fileCreatedAt", "fileModifiedAt",
        checksum, "checksumAlgorithm", "originalFileName", "localDateTime")
        VALUES (@id, @id, 'IMAGE', '/smoke.jpg', now(), now(), decode(repeat('01', 20), 'hex'), 'sha1', 'smoke.jpg', now());
    INSERT INTO public.asset_face (id, "assetId") VALUES (@id, @id);
    INSERT INTO public.face_search ("faceId", embedding) VALUES (@id, CAST(@embedding AS vector));
    INSERT INTO public.person ("ownerId", "personGroupId", name, "thumbnailPath", "birthDate")
        VALUES (@id, @personGroupId, 'Example', '', '2000-02-29');
    """, connection, transaction);
seed.Parameters.AddWithValue("id", id);
seed.Parameters.AddWithValue("clusterId", clusterId);
seed.Parameters.AddWithValue("personGroupId", personGroupId);
seed.Parameters.AddWithValue("email", $"{id:N}@example.invalid");
seed.Parameters.AddWithValue("embedding", embedding);
seed.ExecuteNonQuery();

var row = repository.FaceSearch.ToList().Single(face => face.FaceId == id);
PgVector actual = row.Embedding;
if (actual.ToString() != embedding)
    throw new InvalidOperationException("The 512 embedding values did not match the fixture.");
if (repository.Person.ToList().Single(person => person.OwnerId == id && person.PersonGroupId == personGroupId)
    .BirthDate != new DateOnly(2000, 2, 29))
    throw new InvalidOperationException("The Immich birth date did not match the fixture.");

transaction.Rollback();
Console.WriteLine("Immich face embedding and birth date read through Nextorm without a plugin. Fixture rolled back.");
