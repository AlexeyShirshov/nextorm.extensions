#!/usr/bin/env dotnet
#:package Microsoft.Data.Sqlite@10.0.14
#:package Nextorm.Sqlite@1.0.11
#:property TargetFramework=net10.0
#:property PublishAot=false

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

if (args.Length != 1)
{
    Console.Error.WriteLine("Pass a SQLite database path.");
    return 1;
}

using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
{
    DataSource = args[0],
    Mode = SqliteOpenMode.ReadOnly,
    Pooling = false
}.ToString());
connection.Open();
using var db = new SqliteDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
var results = db.From<SqliteQuickCheck>().Select(row => row.Result).ToList();
if (results is ["ok"])
{
    return 0;
}

Console.Error.WriteLine(string.Join(Environment.NewLine, results));
return 1;

[Table("pragma_quick_check")]
internal sealed class SqliteQuickCheck
{
    [Column("quick_check")] public string Result { get; set; } = "";
}
