using System.CommandLine;
#if !NATIVE_AOT
using System.Reflection;
using System.Runtime.Loader;
#endif
using System.Text.Json;
using System.Text.RegularExpressions;
using Nextorm.Scaffold;
using Nextorm.Scaffolding;

var root = new RootCommand("Nextorm schema tools");
var scaffold = new Command("scaffold", "Generate C# repository contexts from an existing database.");
root.Subcommands.Add(scaffold);
var scalarOptions = new[] { "--provider", "--database", "--connection-env", "--namespace", "--context",
    "--output-dir", "--models-dir", "--customize", "--import" }.Select(name => new Option<string>(name)).ToArray();
var listOptions = new[] { "--table", "--exclude-table", "--schema", "--exclude-schema" }
    .Select(name => new Option<string[]>(name)).ToArray();
var flagOptions = new[] { "--force", "--single-file" }.Select(name => new Option<bool>(name)).ToArray();
foreach (var option in scalarOptions.Cast<Option>().Concat(listOptions).Concat(flagOptions))
    scaffold.Options.Add(option);
scaffold.SetAction(result => Run(
    scalarOptions.Where(option => result.GetValue(option) is not null)
        .ToDictionary(option => option.Name, option => result.GetValue(option)!, StringComparer.Ordinal),
    listOptions.ToDictionary(option => option.Name,
        option => new HashSet<string>(result.GetValue(option) ?? [], StringComparer.Ordinal), StringComparer.Ordinal),
    flagOptions.Where(option => result.GetValue(option)).Select(option => option.Name).ToHashSet(StringComparer.Ordinal)));
return root.Parse(args.Length == 0 ? ["--help"] : args).Invoke();

static int Run(Dictionary<string, string> options, Dictionary<string, HashSet<string>> lists, HashSet<string> flags)
{
try
{
    var tables = lists["--table"];
    var excludedTables = lists["--exclude-table"];
    var schemas = lists["--schema"];
    var excludedSchemas = lists["--exclude-schema"];
    var valueOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "--provider", "--database", "--connection-env", "--schema", "--namespace",
        "--context", "--output-dir", "--table", "--exclude-table", "--exclude-schema", "--models-dir", "--customize",
    };
    if (options.TryGetValue("--import", out var configPath))
    {
        var fullPath = Path.GetFullPath(configPath);
        var configDirectory = Path.GetDirectoryName(fullPath)!;
        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Scaffold configuration must be a JSON object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new ArgumentException($"Duplicate configuration key: {property.Name}");
            var option = "--" + property.Name;
            if (lists.TryGetValue(option, out var values))
            {
                if (option == "--schema" && property.Value.ValueKind == JsonValueKind.String)
                {
                    values.Add(ConfigString(property.Value, property.Name));
                    continue;
                }
                if (property.Value.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException($"Configuration key '{property.Name}' must be an array of strings.");
                foreach (var item in property.Value.EnumerateArray())
                    values.Add(ConfigString(item, property.Name));
            }
            else if (option is "--force" or "--single-file")
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException($"Configuration key '{property.Name}' must be a boolean.");
                if (property.Value.GetBoolean())
                    flags.Add(option);
            }
            else if (valueOptions.Contains(option))
            {
                var value = ConfigString(property.Value, property.Name);
                if (option is "--database" or "--output-dir" or "--customize")
                    value = Path.GetFullPath(value, configDirectory);
                options.TryAdd(option, value);
            }
            else
                throw new ArgumentException($"Unknown configuration key: {property.Name}");
        }
    }

    string Required(string key) => options.GetValueOrDefault(key)
        ?? throw new ArgumentException($"Missing required option {key}.");

    var provider = Required("--provider");
    if (provider == "mssql")
        provider = "sqlserver";
    var context = Required("--context");
    var targetNamespace = Required("--namespace");
    const string contextIdentifier = @"[A-Z][A-Za-z0-9_]*";
    const string namespaceIdentifier = @"[A-Za-z_][A-Za-z0-9_]*";
    if (!Regex.IsMatch(context, $@"\A(?:{contextIdentifier})\z") ||
        !Regex.IsMatch(targetNamespace, $@"\A{namespaceIdentifier}(?:\.{namespaceIdentifier})*\z"))
        throw new ArgumentException("Use a PascalCase context name and a dotted C# namespace.");

    var output = Path.GetFullPath(Required("--output-dir"));
    var singleFile = flags.Contains("--single-file");
    var configuredModels = options.GetValueOrDefault("--models-dir");
    if (singleFile && configuredModels is not null)
        throw new ArgumentException("--models-dir does not apply to --single-file.");
    var modelsPath = configuredModels ?? "Models";
    var modelsDirectory = Path.GetFullPath(modelsPath, output);
    var relativeModels = Path.GetRelativePath(output, modelsDirectory);
    if (Path.IsPathRooted(modelsPath) || relativeModels == "." || relativeModels == ".." ||
        relativeModels.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new ArgumentException("--models-dir must be a subdirectory of --output-dir.");
    if (provider is not ("sqlite" or "postgresql" or "mysql" or "sqlserver"))
        throw new ArgumentException("Supported providers: sqlite, postgresql, mysql, sqlserver (mssql).");
    if (provider == "sqlite" && (schemas.Count > 0 || excludedSchemas.Count > 0 || options.ContainsKey("--connection-env")))
        throw new ArgumentException("SQLite accepts --database, not schema filters or --connection-env.");
    if (provider != "sqlite" && options.ContainsKey("--database"))
        throw new ArgumentException("--database is only for SQLite.");
    var database = provider == "sqlite" ? Required("--database") : null;
    if (database is not null && !File.Exists(database))
        throw new ArgumentException("The SQLite database file does not exist.");
    var connection = provider == "sqlite" ? null : Environment.GetEnvironmentVariable(Required("--connection-env"));
    if (provider != "sqlite" && string.IsNullOrWhiteSpace(connection))
        throw new ArgumentException("The connection environment variable is empty or not set.");
    bool IncludeSchema(string name) => (schemas.Count == 0 || schemas.Contains(name)) && !excludedSchemas.Contains(name);
    if (provider == "mysql" && (schemas.Count != 1 || !IncludeSchema(schemas.Single())))
        throw new ArgumentException("MySQL requires exactly one --schema that is not excluded.");

    IScaffoldInterceptor? plugin = null;
    if (options.TryGetValue("--customize", out var dll))
    {
#if NATIVE_AOT
        throw new PlatformNotSupportedException("--customize is not available in Native AOT builds.");
#else
        plugin = LoadInterceptor(dll);
#endif
    }
    var interceptor = new FilteredInterceptor(tables, excludedTables, plugin);
    var filename = context + ".cs";
    var owner = $"// Generated for {filename}";
    var staging = Directory.CreateTempSubdirectory("nextorm-scaffold-");
    try
    {
        var destination = Path.Combine(staging.FullName, filename);
        SchemaScaffolder.Generate(provider, database ?? connection!, destination, targetNamespace, context,
            interceptor, singleFile, IncludeSchema, provider == "mysql" ? schemas.Single() : null);

        File.WriteAllText(destination, owner + "\n" + File.ReadAllText(destination));
        var files = Directory.GetFiles(staging.FullName, "*.cs", SearchOption.AllDirectories)
            .ToDictionary(path => path == destination ? filename : Path.Combine(relativeModels, Path.GetFileName(path)),
                StringComparer.OrdinalIgnoreCase);
        var oldModels = Directory.Exists(modelsDirectory)
            ? Directory.GetFiles(modelsDirectory, "*.g.cs").Where(IsOwned).ToArray()
            : [];
        foreach (var relativePath in files.Keys)
        {
            var path = Path.Combine(output, relativePath);
            if (File.Exists(path) && (!flags.Contains("--force") || !IsOwned(path)))
                throw new IOException($"Refusing to overwrite {path}.");
        }
        if (oldModels.Length > 0 && !flags.Contains("--force"))
            throw new IOException("Use --force to replace the previously generated models.");

        foreach (var (relativePath, source) in files)
        {
            var path = Path.Combine(output, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(source, path, overwrite: flags.Contains("--force"));
        }
        foreach (var oldModel in oldModels)
            if (!files.ContainsKey(Path.GetRelativePath(output, oldModel)))
                File.Delete(oldModel);

        Console.WriteLine($"Generated {files.Count} file(s) in {output}.");
    }
    finally
    {
        staging.Delete(recursive: true);
    }

    bool IsOwned(string path) => File.ReadLines(path).FirstOrDefault() == owner;
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
}

static string ConfigString(JsonElement value, string key) =>
    value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
        ? text
        : throw new ArgumentException($"Configuration key '{key}' requires a non-empty string.");

#if !NATIVE_AOT
static IScaffoldInterceptor LoadInterceptor(string path)
{
    var fullPath = Path.GetFullPath(path);
    if (!Path.GetExtension(fullPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("--customize requires a compiled .dll.");
    var loadContext = new PluginLoadContext(fullPath);
    var types = loadContext.LoadFromAssemblyPath(fullPath).GetExportedTypes()
        .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters &&
            typeof(IScaffoldInterceptor).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null)
        .ToArray();
    if (types.Length != 1)
        throw new ArgumentException("The plugin must export exactly one concrete IScaffoldInterceptor with a public parameterless constructor.");
    return (IScaffoldInterceptor)Activator.CreateInstance(types[0])!;
}
#endif

internal sealed class FilteredInterceptor(HashSet<string> tables, HashSet<string> excludedTables,
    IScaffoldInterceptor? plugin) : IScaffoldInterceptor
{
    public bool IncludeObject(string databaseName) =>
        (tables.Count == 0 || tables.Contains(databaseName)) && !excludedTables.Contains(databaseName) &&
        (plugin?.IncludeObject(databaseName) ?? true);

    public string GetEntityName(string databaseName) => plugin?.GetEntityName(databaseName) ?? databaseName;

    public string GetPropertyName(string databaseObjectName, string databaseColumnName) =>
        plugin?.GetPropertyName(databaseObjectName, databaseColumnName) ?? databaseColumnName;

    public string? GetClrType(string databaseColumnName, string sqlDeclaration) =>
        plugin?.GetClrType(databaseColumnName, sqlDeclaration);
}

#if !NATIVE_AOT
internal sealed class PluginLoadContext(string path) : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver = new(path);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var contract = typeof(IScaffoldInterceptor).Assembly;
        if (assemblyName.Name == contract.GetName().Name)
            return contract;
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolved is null ? null : LoadFromAssemblyPath(resolved);
    }
}
#endif
