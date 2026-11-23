using System.Globalization;
using System.Text;

namespace Nextorm.Contexts;

internal static class RepositoryContextSource
{
    internal static string Identifier(string name, string fallback, HashSet<string> used)
    {
        var result = new StringBuilder();
        var capitalize = true;
        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                capitalize = true;
                continue;
            }
            result.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }
        if (result.Length == 0 || char.IsAsciiDigit(result[0]))
            result.Insert(0, fallback);
        var stem = result.ToString();
        var candidate = stem;
        for (var suffix = 2; !used.Add(candidate); suffix++)
            candidate = stem + suffix.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }

    internal static void Append(StringBuilder source, string contextName, IEnumerable<string> mappings, bool publicContext = false)
    {
        source.Append(CultureInfo.InvariantCulture, $$"""

            {{(publicContext ? "public" : "internal")}} class {{contextName}}(global::NextORM.Core.IDataContext dataContext)
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
