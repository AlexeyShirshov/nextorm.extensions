using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

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
    [Column("relkind"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char Kind { get; set; }
    [Column("reltuples")] public float EstimatedRows { get; set; }
    [Column("relispartition")] public bool IsPartition { get; set; }
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
    [Column("atthasdef")] public bool HasDefault { get; set; }
    [Column("attidentity"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char Identity { get; set; }

    [Column("attgenerated"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char Generated { get; set; }
}

[Table("pg_attrdef", Schema = "pg_catalog")]
public sealed class PgAttributeDefault
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("adrelid")] public uint Relation { get; set; }
    [Column("adnum")] public short Number { get; set; }
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

[Table("pg_enum", Schema = "pg_catalog")]
public sealed class PgEnum
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("enumtypid")] public uint Type { get; set; }
    [Column("enumsortorder")] public float SortOrder { get; set; }
    [Column("enumlabel")] public string Label { get; set; } = "";
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
    [Column("contype"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char Kind { get; set; }
    [Column("conkey")] public short[]? Keys { get; set; }
    [Column("confkey")] public short[]? ReferencedKeys { get; set; }
    [Column("confupdtype"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char OnUpdate { get; set; }

    [Column("confdeltype"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char OnDelete { get; set; }
}

[Table("pg_indexes", Schema = "pg_catalog")]
public sealed class PgIndex
{
    [Column("schemaname")] public string Schema { get; set; } = "";
    [Column("tablename")] public string Table { get; set; } = "";
    [Column("indexname")] public string Name { get; set; } = "";
    [Column("indexdef")] public string Definition { get; set; } = "";
}

[Table("pg_index", Schema = "pg_catalog")]
public sealed class PgIndexMetadata
{
    [Column("indexrelid")] public uint Index { get; set; }
    [Column("indrelid")] public uint Relation { get; set; }
    [Column("indisunique")] public bool IsUnique { get; set; }
    [Column("indisprimary")] public bool IsPrimary { get; set; }
    [Column("indnkeyatts")] public short KeyCount { get; set; }
    [Column("indkey")] public short[] Keys { get; set; } = [];
}

[Table("pg_partitioned_table", Schema = "pg_catalog")]
public sealed class PgPartitionedTable
{
    [Column("partrelid")] public uint Relation { get; set; }
    [Column("partstrat"), ValueConverter(typeof(PgCatalogCharConverter))]
    public char Strategy { get; set; }
    [Column("partdefid")] public uint DefaultPartition { get; set; }
    [Column("partattrs")] public short[] Keys { get; set; } = [];
}

[Table("pg_inherits", Schema = "pg_catalog")]
public sealed class PgInheritance
{
    [Column("inhrelid")] public uint Relation { get; set; }
    [Column("inhparent")] public uint Parent { get; set; }
    [Column("inhseqno")] public int SequenceNumber { get; set; }
}

[Table("pg_sequence", Schema = "pg_catalog")]
public sealed class PgSequence
{
    [Column("seqrelid")] public uint Relation { get; set; }
    [Column("seqtypid")] public uint Type { get; set; }
    [Column("seqstart")] public long Start { get; set; }
    [Column("seqincrement")] public long Increment { get; set; }
    [Column("seqmin")] public long Minimum { get; set; }
    [Column("seqmax")] public long Maximum { get; set; }
    [Column("seqcache")] public long Cache { get; set; }
    [Column("seqcycle")] public bool Cycle { get; set; }
}
