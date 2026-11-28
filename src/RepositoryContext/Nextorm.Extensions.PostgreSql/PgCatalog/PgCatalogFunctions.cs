using NextORM.Core;

namespace Nextorm.Extensions.PostgreSql.PgCatalog;

/// <summary>PostgreSQL built-in functions for use in NextORM query expressions.</summary>
public static class PgCatalogFunctions
{
    [SqlFunction("current_setting", Schema = "pg_catalog")] public static string Setting(string key) => throw new NotSupportedException();
    [SqlFunction("current_database", Schema = "pg_catalog")] public static string DatabaseName() => throw new NotSupportedException();
    [SqlFunction("has_schema_privilege", Schema = "pg_catalog")] public static bool CanUse(uint oid, string privilege) => throw new NotSupportedException();
    [SqlFunction("has_table_privilege", Schema = "pg_catalog")] public static bool CanRead(uint oid, string privilege) => throw new NotSupportedException();
    [SqlFunction("has_any_column_privilege", Schema = "pg_catalog")] public static bool CanReadAny(uint oid, string privilege) => throw new NotSupportedException();
    [SqlFunction("has_column_privilege", Schema = "pg_catalog")] public static bool CanReadColumn(uint oid, string column, string privilege) => throw new NotSupportedException();
    [SqlFunction("obj_description", Schema = "pg_catalog")] public static string? Comment(uint oid, string catalog) => throw new NotSupportedException();
    [SqlFunction("col_description", Schema = "pg_catalog")] public static string? ColumnComment(uint oid, int column) => throw new NotSupportedException();
    [SqlFunction("format_type", Schema = "pg_catalog")] public static string TypeName(uint oid, int modifier) => throw new NotSupportedException();
    [SqlFunction("pg_get_constraintdef", Schema = "pg_catalog")] public static string Constraint(uint oid) => throw new NotSupportedException();
    [SqlFunction("format", Schema = "pg_catalog")] public static string Format(string format, object value) => throw new NotSupportedException();
    [SqlFunction("left", Schema = "pg_catalog")] public static string Left(string value, int length) => throw new NotSupportedException();

    /// <summary>Converts a numeric query parameter to a PostgreSQL object identifier.</summary>
    /// <param name="value">The numeric object identifier.</param>
    /// <returns>The value as a PostgreSQL <c>oid</c>.</returns>
    /// <exception cref="NotSupportedException">Called outside a NextORM query.</exception>
    [SqlFunction("oid", Schema = "pg_catalog")]
    public static uint ObjectId(long value) => throw new NotSupportedException();

    /// <summary>Gets the SQL text of a PostgreSQL expression tree within a query.</summary>
    /// <param name="expressionTree">A catalog column of type <c>pg_node_tree</c>.</param>
    /// <param name="relation">The relation's object identifier, or zero if the expression doesn't reference a relation.</param>
    /// <returns>The deparsed expression, or <see langword="null"/> if the tree is null or the relation doesn't exist.</returns>
    /// <exception cref="NotSupportedException">Called outside a NextORM query.</exception>
    [SqlFunction("pg_get_expr", Schema = "pg_catalog")]
    public static string? Expression(string? expressionTree, uint relation) => throw new NotSupportedException();

    /// <summary>Gets the SQL definition of a partition key within a query.</summary>
    /// <param name="relation">The partitioned relation's object identifier.</param>
    /// <returns>The partition key definition, or <see langword="null"/> for other relations.</returns>
    /// <exception cref="NotSupportedException">Called outside a NextORM query.</exception>
    [SqlFunction("pg_get_partkeydef", Schema = "pg_catalog")]
    public static string? PartitionKey(uint relation) => throw new NotSupportedException();

    /// <summary>Gets the SQL constraint that applies to a partition within a query.</summary>
    /// <param name="relation">The partition's object identifier.</param>
    /// <returns>The partition constraint, or <see langword="null"/> if there isn't one.</returns>
    /// <exception cref="NotSupportedException">Called outside a NextORM query.</exception>
    [SqlFunction("pg_get_partition_constraintdef", Schema = "pg_catalog")]
    public static string? PartitionConstraint(uint relation) => throw new NotSupportedException();
}
