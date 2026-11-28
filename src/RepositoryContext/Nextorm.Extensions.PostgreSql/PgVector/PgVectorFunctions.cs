using NextORM.Core;
using Vector = NextORM.Postgres.PgVector;

namespace Nextorm.Extensions.PostgreSql.PgVector;

/// <summary>NextORM query functions for pgvector installed in the public schema.</summary>
public static class PgVectorFunctions
{
    [SqlFunction("cosine_distance", Schema = "public")]
    public static double CosineDistance(object stored, Vector query) => throw new NotSupportedException();
}
