# Application schema fixtures

These are small, schema-only excerpts adapted from pinned upstream releases.
They are not full application dumps and contain no application data.
Load each fixture into its own empty disposable database.

| Fixture | Upstream source | Cases |
| --- | --- | --- |
| `gitea.sqlite.sql`, `gitea.postgresql.sql` | [Gitea v1.24.7, `RepoTopic` model](https://github.com/go-gitea/gitea/blob/v1.24.7/models/repo/topic.go) | The same ordered composite key on SQLite and PostgreSQL. The SQL translates the XORM model rather than capturing an XORM dump. |
| `mastodon.postgresql.sql` | [Mastodon v4.3.0, `tags` in `db/schema.rb`](https://github.com/mastodon/mastodon/blob/v4.3.0/db/schema.rb) | Nullable booleans and timestamps, and a unique expression index with `text_pattern_ops`. |
| `wordpress.mysql.sql` | [WordPress 6.8.3, `wp-admin/includes/schema.php`](https://github.com/WordPress/WordPress/blob/6.8.3/wp-admin/includes/schema.php) | Unsigned identifiers, nullable metadata, prefix indexes, and a logical relationship without a foreign key. The PHP substitutions use `wp_`, `utf8mb4`, and a prefix length of 191. |

From the repository root, load a fixture using the matching database client:

```sh
sqlite3 /tmp/gitea-fixture.db < tests/AppSchemas/gitea.sqlite.sql
psql -X -v ON_ERROR_STOP=1 -d gitea_fixture -f tests/AppSchemas/gitea.postgresql.sql
psql -X -v ON_ERROR_STOP=1 -d mastodon_fixture -f tests/AppSchemas/mastodon.postgresql.sql
mysql wordpress_fixture < tests/AppSchemas/wordpress.mysql.sql
```

Create the server databases before running these commands. Use a new file for
SQLite. The WordPress fixture can also be loaded with the MariaDB client.

## Generate NextORM mappings

Call the provider generator directly from a .NET project that references it:

- SQLite: `SqliteSchemaGenerator.Scaffold(databasePath, outputPath, targetNamespace, contextName, databaseVersion)`.
- PostgreSQL: `PostgreSqlSchemaGenerator.Scaffold(connectionString, outputPath, targetNamespace, contextName)`.
- MySQL: `MySqlSchemaGenerator.Scaffold(connectionString, schema, outputPath, targetNamespace, contextName)`.

Use a disposable database containing only the target schema. Each call writes
one context file and individual entity files in the adjacent `Models/` directory.

## Regenerate the full Gitea SQLite context

Use the standalone Gitea 1.24.7 binary. In a temporary directory, create
`app.ini` with the following settings. Replace `/absolute/path/to/temp/gitea.db`
with a new database path in that directory.

```ini
[database]
DB_TYPE = sqlite3
PATH = /absolute/path/to/temp/gitea.db
[security]
INSTALL_LOCK = true
```

Run `gitea migrate --work-path <temp-dir> --config <temp-dir>/app.ini`.
This initializes the schema without starting a web server. From the repository
root, use the SQLite generator with namespace `Nextorm.Extensions.Gitea`, context
name `GiteaSqliteRepositoryContext`, and database version `1.24.7`. Pass
`src/RepositoryContext/Nextorm.Extensions.Gitea/GiteaSqliteRepositoryContext.cs`
as the output path.

The packaged context targets SQLite. The PostgreSQL `repo_topic` fixture above
remains a separate generator example.

## Herdr

[Herdr's session persistence](https://github.com/herdrdev/herdr/blob/e8d7d9705a7a2c89cef4dd42079379f05685af87/src/persist/io.rs)
uses `session.json`, not a SQL schema. It is not a target for these relational
generators.
