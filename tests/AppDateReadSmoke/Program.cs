using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using Nextorm.Extensions.Redmine;
using Npgsql;
using OpenWebUISqliteRepositoryContext = Nextorm.Extensions.OpenWebUI.OpenWebUISqliteRepositoryContext;

if (args.Length != 1 || !File.Exists(args[0]) ||
    Environment.GetEnvironmentVariable("REDMINE_SMOKE_CONNECTION") is not { Length: > 0 } redmineConnection)
    throw new ArgumentException(
        "Pass a clean Open WebUI 0.13.5 webui.db, and set REDMINE_SMOKE_CONNECTION to a disposable Redmine 7.0.3 database.");

CheckOpenWebUI(args[0]);
CheckRedmine(redmineConnection);

static void CheckOpenWebUI(string path)
{
    using var source = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadOnly
    }.ToString());
    using var connection = new SqliteConnection("Data Source=:memory:");
    source.Open();
    connection.Open();
    source.BackupDatabase(connection);
    using var context = new SqliteDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
    using var seed = connection.CreateCommand();
    seed.CommandText = """
        INSERT INTO chat (id,user_id,title,archived,created_at,updated_at,chat,pinned,meta)
        VALUES ('nextorm-smoke','fixture','Example',0,1760000000,1760000001,'{"messages":[]}',1,'{}');
        INSERT INTO config_old (id,data,version,created_at,updated_at) VALUES (101,'{}',1,'2025-01-02 03:04:05',NULL);
        INSERT INTO "user" (id,name,email,role,profile_image_url,created_at,updated_at,last_active_at,date_of_birth)
        VALUES ('with-date','Example','date@example.invalid','user','',1760000000,1760000000,1760000000,'2000-02-29'),
               ('without-date','Example','no-date@example.invalid','user','',1760000000,1760000000,1760000000,NULL);
        """;
    seed.ExecuteNonQuery();

    var repository = new OpenWebUISqliteRepositoryContext(context);
    var chat = repository.Chat.ToList().Single(row => row.Id == "nextorm-smoke");
    var config = repository.ConfigOld.ToList().Single(row => row.Id == 101);
    var users = repository.User.ToList();
    if (chat.CreatedAt != 1760000000L || chat.UpdatedAt != 1760000001L ||
        config.CreatedAt != new DateTime(2025, 1, 2, 3, 4, 5) || config.UpdatedAt is not null)
        throw new InvalidOperationException(
            $"Unexpected Open WebUI timestamps: chat {chat.CreatedAt}/{chat.UpdatedAt}, config {config.CreatedAt:O}/{config.UpdatedAt:O}.");
    if (users.Single(row => row.Id == "with-date").DateOfBirth != new DateOnly(2000, 2, 29) ||
        users.Single(row => row.Id == "without-date").DateOfBirth is not null)
        throw new InvalidOperationException("Open WebUI birth dates did not match the fixture.");

    Console.WriteLine("Open WebUI: chat Unix seconds, configuration date/time, and nullable birth dates passed.");
}

static void CheckRedmine(string connectionString)
{
    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();
    using var context = new PostgresDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
    using var transaction = connection.BeginTransaction();
    ((ITransactionManager)context).UseTransaction(transaction);
    using var seed = new NpgsqlCommand("""
        INSERT INTO time_entries (id,project_id,user_id,hours,activity_id,spent_on,tyear,tmonth,tweek,created_on,updated_on)
        VALUES (7654321,1,1,1.25,1,'2025-01-02',2025,1,1,'2025-01-02 03:04:05','2025-01-02 03:04:05');
        INSERT INTO changesets (id,repository_id,revision,committed_on,commit_date)
        VALUES (7654321,1,'with-date','2025-01-02 03:04:05','2025-01-02'),
               (7654322,1,'without-date','2025-01-02 03:04:05',NULL);
        """, connection, transaction);
    seed.ExecuteNonQuery();

    var repository = new RedminePostgreSqlRepositoryContext(context);
    var timeEntry = repository.TimeEntries.ToList().Single(row => row.Id == 7654321);
    var changesets = repository.Changesets.ToList();
    var expected = new DateOnly(2025, 1, 2);
    if (timeEntry.SpentOn != expected ||
        changesets.Single(row => row.Id == 7654321).CommitDate != expected ||
        changesets.Single(row => row.Id == 7654322).CommitDate is not null)
        throw new InvalidOperationException("Redmine dates did not match the fixture.");

    transaction.Rollback();
    Console.WriteLine("Redmine: date, nullable date, and NULL passed.");
}
