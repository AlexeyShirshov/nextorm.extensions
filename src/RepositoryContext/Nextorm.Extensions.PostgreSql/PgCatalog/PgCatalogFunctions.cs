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
}
