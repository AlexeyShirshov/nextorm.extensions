using System.ComponentModel.DataAnnotations.Schema;

namespace Nextorm.Extensions.PostgreSql.PgCatalog;

/// <summary>A row from <see cref="PgCatalogQueries.AttributeDefaultExpressions"/>.</summary>
public sealed class PgAttributeDefaultExpression
{
    [Column("oid")] public uint Oid { get; set; }
    [Column("adrelid")] public uint Relation { get; set; }
    [Column("adnum")] public short Number { get; set; }
    [Column("expression")] public string Expression { get; set; } = "";
}
