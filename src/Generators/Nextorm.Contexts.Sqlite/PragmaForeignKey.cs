using System.ComponentModel.DataAnnotations.Schema;

namespace Nextorm.Contexts.Sqlite;

[Table("pragma_foreign_key_list")]
internal sealed class PragmaForeignKey
{
    [Column("arg")] public string TableArgument { get; set; } = "";
    [Column("schema")] public string SchemaArgument { get; set; } = "";
    [Column("id")] public long Id { get; set; }
    [Column("seq")] public long Sequence { get; set; }
    [Column("table")] public string Table { get; set; } = "";
    [Column("from")] public string From { get; set; } = "";
    [Column("to")] public string? To { get; set; }
}
