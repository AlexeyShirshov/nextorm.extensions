using System.Text;
using System.Text.RegularExpressions;

namespace Nextorm.Contexts;

internal static class RepositoryContextFiles
{
    internal static void WriteAtomically(string destination, StringBuilder source)
    {
        var temporary = Path.Join(Path.GetDirectoryName(destination), $".{Guid.NewGuid():N}.tmp");
        var ownsTemporary = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                ownsTemporary = true;
                using var writer = new StreamWriter(stream);
                writer.Write(source);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (ownsTemporary)
            {
                File.Delete(temporary);
            }
        }
    }

    internal static void Split(string source, string output)
    {
        const string table = @"^(?:\[global::System\.ComponentModel\.Description\([^\r\n]*\)\]\r?\n)?\[global::System\.ComponentModel\.DataAnnotations\.Schema\.Table\(";
        const string context = @"^public class [A-Za-z0-9_]+\(global::NextORM\.Core\.IDataContext dataContext\)";
        var sections = Regex.Matches(source, table + "|" + context, RegexOptions.Multiline);
        if (sections.Count < 2 || !sections[^1].Value.StartsWith("public class ", StringComparison.Ordinal))
            throw new InvalidDataException("Expected entity mappings followed by a public repository context.");

        var header = source[..sections[0].Index].TrimEnd() + "\n\n";
        var owner = $"// Generated for {Path.GetFileName(output)}";
        var modelHeader = owner + "\n" + header;
        var models = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sections.Count - 1; index++)
        {
            var body = source[sections[index].Index..sections[index + 1].Index].TrimEnd();
            var name = Regex.Match(body, @"^public class ([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Multiline);
            if (!name.Success || !models.TryAdd(name.Groups[1].Value + ".g.cs", modelHeader + body + "\n"))
                throw new InvalidDataException("Missing or duplicate entity filename.");
        }

        var modelsDirectory = Path.Join(Path.GetDirectoryName(output), "Models");
        Directory.CreateDirectory(modelsDirectory);
        foreach (var path in models.Keys.Select(name => Path.Join(modelsDirectory, name)))
        {
            if (File.Exists(path) && File.ReadLines(path).FirstOrDefault() != owner)
                throw new IOException($"Refusing to overwrite a model not generated for this context: {path}");
        }
        foreach (var (name, content) in models)
            File.WriteAllText(Path.Join(modelsDirectory, name), content);
        File.WriteAllText(output, header + source[sections[^1].Index..]);
        var staleModels = Directory.EnumerateFiles(modelsDirectory, "*.g.cs").Where(path =>
            !models.ContainsKey(Path.GetFileName(path)) && File.ReadLines(path).FirstOrDefault() == owner);
        foreach (var path in staleModels)
        {
            File.Delete(path);
        }
    }
}
