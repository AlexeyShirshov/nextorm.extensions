using NextORM.Core;

namespace Nextorm.Extensions.PostgreSql.PgCatalog;

// PostgreSQL catalog "char" columns are single bytes.
internal sealed class PgCatalogCharConverter : ValueConverter<char, byte>
{
    public override char ConvertFromProvider(byte provider) => (char)provider;

    public override byte ConvertToProvider(char model) => checked((byte)model);
}
