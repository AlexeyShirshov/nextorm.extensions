using System.ComponentModel.DataAnnotations.Schema;

namespace Nextorm.Extensions.PostgreSql.PgCatalog;

[Table("pg_roles", Schema = "pg_catalog")]
public sealed class PgRole
{
    [Column("rolname")] public string Name { get; set; } = "";
    [Column("rolsuper")] public bool Super { get; set; }
    [Column("rolcreaterole")] public bool CreateRole { get; set; }
    [Column("rolcreatedb")] public bool CreateDatabase { get; set; }
    [Column("rolreplication")] public bool Replication { get; set; }
    [Column("rolbypassrls")] public bool BypassRls { get; set; }
}

[Table("pg_namespace", Schema = "pg_catalog")]
public sealed class PgNamespace
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("nspname")] public string Name { get; set; } = "";
}

[Table("pg_class", Schema = "pg_catalog")]
public sealed class PgRelation
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("relnamespace")] public uint Namespace { get; set; }
    [Column("relname")] public string Name { get; set; } = "";
    [Column("relkind")] public char Kind { get; set; }
    [Column("reltuples")] public float EstimatedRows { get; set; }
}

[Table("pg_attribute", Schema = "pg_catalog")]
public sealed class PgAttribute
{
    [Column("attrelid")] public uint Relation { get; set; }
    [Column("attnum")] public short Number { get; set; }
    [Column("attname")] public string Name { get; set; } = "";
    [Column("atttypid")] public uint Type { get; set; }
    [Column("atttypmod")] public int Modifier { get; set; }
    [Column("attnotnull")] public bool NotNull { get; set; }
    [Column("attisdropped")] public bool Dropped { get; set; }
}

[Table("pg_type", Schema = "pg_catalog")]
public sealed class PgType
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("typnamespace")] public uint Namespace { get; set; }
    [Column("typname")] public string Name { get; set; } = "";
    [Column("typbasetype")] public uint BaseType { get; set; }
    [Column("typtypmod")] public int Modifier { get; set; }
    [Column("typnotnull")] public bool NotNull { get; set; }
}

[Table("pg_extension", Schema = "pg_catalog")]
public sealed class PgExtension
{
    [Column("extname")] public string Name { get; set; } = "";
    [Column("extversion")] public string Version { get; set; } = "";
    [Column("extnamespace")] public uint Namespace { get; set; }
}

[Table("pg_constraint", Schema = "pg_catalog")]
public sealed class PgConstraint
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("conrelid")] public uint Relation { get; set; }
    [Column("confrelid")] public uint Referenced { get; set; }
    [Column("conname")] public string Name { get; set; } = "";
    [Column("contype")] public char Kind { get; set; }
    [Column("conkey")] public short[]? Keys { get; set; }
}

[Table("pg_indexes", Schema = "pg_catalog")]
public sealed class PgIndex
{
    [Column("schemaname")] public string Schema { get; set; } = "";
    [Column("tablename")] public string Table { get; set; } = "";
    [Column("indexname")] public string Name { get; set; } = "";
    [Column("indexdef")] public string Definition { get; set; } = "";
}
