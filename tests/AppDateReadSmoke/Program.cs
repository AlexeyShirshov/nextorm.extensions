using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using Nextorm.Extensions.Redmine;
using Npgsql;
using OpenWebUI = Nextorm.Extensions.OpenWebUI;

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
    context.CreateInsertBuilder<OpenWebUI.Chat>()
        .Value(row => row.Id, "nextorm-smoke")
        .Value(row => row.UserId, "fixture")
        .Value(row => row.Title, "Example")
        .Value(row => row.Archived, false)
        .Value(row => row.CreatedAt, 1760000000L)
        .Value(row => row.UpdatedAt, 1760000001L)
        .Value(row => row.Chat2, """{"messages":[]}""")
        .Value(row => row.Pinned, true)
        .Value(row => row.Meta, "{}")
        .Insert();
    context.CreateInsertBuilder<OpenWebUI.ConfigOld>()
        .Value(row => row.Id, 101L)
        .Value(row => row.Data, "{}")
        .Value(row => row.Version, 1L)
        .Value(row => row.CreatedAt, new DateTime(2025, 1, 2, 3, 4, 5))
        .Value(row => row.UpdatedAt, (DateTime?)null)
        .Insert();
    InsertUser("with-date", "date@example.invalid", new DateOnly(2000, 2, 29));
    InsertUser("without-date", "no-date@example.invalid", null);

    var repository = new OpenWebUI.OpenWebUISqliteRepositoryContext(context);
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

    void InsertUser(string id, string email, DateOnly? birthDate) =>
        context.CreateInsertBuilder<OpenWebUI.User>()
            .Value(row => row.Id, id)
            .Value(row => row.Name, "Example")
            .Value(row => row.Email, email)
            .Value(row => row.Role, "user")
            .Value(row => row.ProfileImageUrl, "")
            .Value(row => row.CreatedAt, 1760000000L)
            .Value(row => row.UpdatedAt, 1760000000L)
            .Value(row => row.LastActiveAt, 1760000000L)
            .Value(row => row.DateOfBirth, birthDate)
            .Insert();
}

static void CheckRedmine(string connectionString)
{
    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();
    using var context = new PostgresDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
    using var transaction = connection.BeginTransaction();
    ((ITransactionManager)context).UseTransaction(transaction);
    var timestamp = new DateTime(2025, 1, 2, 3, 4, 5);
    var expected = new DateOnly(2025, 1, 2);
    context.CreateInsertBuilder<TimeEntries>()
        .Value(row => row.Id, 7654321)
        .Value(row => row.ProjectId, 1)
        .Value(row => row.UserId, 1)
        .Value(row => row.Hours, 1.25)
        .Value(row => row.ActivityId, 1)
        .Value(row => row.SpentOn, expected)
        .Value(row => row.Tyear, 2025)
        .Value(row => row.Tmonth, 1)
        .Value(row => row.Tweek, 1)
        .Value(row => row.CreatedOn, timestamp)
        .Value(row => row.UpdatedOn, timestamp)
        .Insert();
    InsertChangeset(7654321, "with-date", expected);
    InsertChangeset(7654322, "without-date", null);

    var repository = new RedminePostgreSqlRepositoryContext(context);
    var timeEntry = repository.TimeEntries.ToList().Single(row => row.Id == 7654321);
    var changesets = repository.Changesets.ToList();
    if (timeEntry.SpentOn != expected ||
        changesets.Single(row => row.Id == 7654321).CommitDate != expected ||
        changesets.Single(row => row.Id == 7654322).CommitDate is not null)
        throw new InvalidOperationException("Redmine dates did not match the fixture.");

    transaction.Rollback();
    Console.WriteLine("Redmine: date, nullable date, and NULL passed.");

    void InsertChangeset(int id, string revision, DateOnly? date) =>
        context.CreateInsertBuilder<Changesets>()
            .Value(row => row.Id, id)
            .Value(row => row.RepositoryId, 1)
            .Value(row => row.Revision, revision)
            .Value(row => row.CommittedOn, timestamp)
            .Value(row => row.CommitDate, date)
            .Insert();
}
