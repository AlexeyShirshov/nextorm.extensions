using System.Globalization;
using System.Text;

namespace Nextorm.Contexts;

internal static class RepositoryContextSource
{
    internal static void Append(StringBuilder source, string contextName, IEnumerable<string> mappings, bool publicContext = false)
    {
        source.Append(CultureInfo.InvariantCulture, $$"""

            {{(publicContext ? "public" : "internal")}} sealed class {{contextName}}(global::NextORM.Core.IDataContext dataContext)
                : global::NextORM.Core.Repositories.RepositoryContext(dataContext)
            {
            """);
        source.Append('\n');
        foreach (var mapping in mappings)
        {
            source.Append(CultureInfo.InvariantCulture,
                $"    public global::NextORM.Core.IRepository<{mapping}> {mapping} => Repository<{mapping}>();\n");
        }
        source.Append("}\n");
    }
}
