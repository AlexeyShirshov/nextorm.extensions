using System.Data;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;
using SqlServerDatabaseMappings;

namespace Nextorm.Contexts.SqlServer;

internal static class SqlServerCatalog
{
    internal static DataTable Read(SqlConnection connection)
    {
        using var context = new SqlServerDataContext(connection, new DataContextBuilder().UseQuotedIdentifiers());
        var catalog = new SqlServerRepositoryContext(context);
        var objects = catalog.SysObject.Where(row => !row.IsMsShipped &&
            (row.Type == "U" || row.Type == "V" || row.Type == "ET")).ToList();
        var schemas = catalog.SysSchema.ToList().ToDictionary(row => row.SchemaId, row => row.Name);
        var types = catalog.SysType.ToList().ToDictionary(row => row.UserTypeId);
        var columns = catalog.SysColumn.ToList().ToLookup(row => row.ObjectId);
        var indexes = catalog.SysIndex.Where(row => row.IsPrimaryKey).ToList()
            .ToDictionary(row => row.ObjectId, row => row.IndexId);
        var indexColumns = catalog.SysIndexColumn.Where(row => row.KeyOrdinal > 0).ToList()
            .ToDictionary(row => (row.ObjectId, row.IndexId, row.ColumnId), row => (int)row.KeyOrdinal);

        // Match the metadata layout shared with the offline writer.
        var metadata = new DataTable();
        Type[] fieldTypes = [typeof(int), typeof(string), typeof(string), typeof(string), typeof(string),
            typeof(string), typeof(bool), typeof(byte), typeof(byte), typeof(short), typeof(int), typeof(bool), typeof(string)];
        foreach (var fieldType in fieldTypes)
            metadata.Columns.Add(null, fieldType);
        foreach (var entry in objects
            .Select(item => (Item: item, Found: schemas.TryGetValue(item.SchemaId, out var schema), Schema: schema))
            .Where(entry => entry.Found)
            .OrderBy(entry => entry.Item.ObjectId))
        {
            var item = entry.Item;
            var schema = entry.Schema;
            foreach (var column in columns[item.ObjectId].OrderBy(row => row.ColumnId).DefaultIfEmpty())
            {
                var native = column is not null ? types.GetValueOrDefault(column.UserTypeId) : null;
                var ordinal = column is not null && item.Type.TrimEnd() == "U" && indexes.TryGetValue(item.ObjectId, out var index)
                    ? indexColumns.GetValueOrDefault((item.ObjectId, index, column.ColumnId))
                    : 0;
                metadata.Rows.Add(item.ObjectId, schema, item.Name, column?.Name,
                    native is null ? null : schemas.GetValueOrDefault(native.SchemaId), native?.Name,
                    column?.IsNullable, column?.Precision, column?.Scale, column?.MaxLength,
                    ordinal, native is not null && (native.IsUserDefined || native.IsAssemblyType), item.Type);
            }
        }
        return metadata;
    }
}
