using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace Nextorm.Extensions.PostgreSql.PgCatalog;

public static class PgCatalogQueries
{
    /// <summary>Returns a query for column default and generated expressions as SQL text.</summary>
    /// <param name="db">The data context to query.</param>
    /// <param name="relationId">The relation to include, or <see langword="null"/> for all relations.</param>
    /// <returns>A query that projects the catalog identifiers and decoded SQL expression.</returns>
    public static QueryCommand<PgAttributeDefaultExpression> AttributeDefaultExpressions(
        this IDataContext db, long? relationId = null)
    {
        var source = db.From<AttributeDefaultSource>();
        if (relationId is long oid)
        {
            source = source.Where(row => row.Relation == PgCatalogFunctions.ObjectId(oid));
        }

        return source.Select(row => new PgAttributeDefaultExpression
        {
            Oid = row.Oid,
            Relation = row.Relation,
            Number = row.Number,
            Expression = PgCatalogFunctions.Expression(row.ExpressionTree, row.Relation)!
        });
    }

    [Table("pg_attrdef", Schema = "pg_catalog")]
    private sealed class AttributeDefaultSource
    {
        [Column("oid")] public uint Oid { get; set; }
        [Column("adrelid")] public uint Relation { get; set; }
        [Column("adnum")] public short Number { get; set; }

        // Only pass the tree to pg_get_expr. Npgsql can't read pg_node_tree as a string.
        [Column("adbin")] public string ExpressionTree { get; set; } = "";
    }
}
