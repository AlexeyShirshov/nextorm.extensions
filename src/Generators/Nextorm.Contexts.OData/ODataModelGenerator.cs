using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Nextorm.Contexts.OData;

/// <summary>
/// Generates C# model declarations from OData XML metadata.
/// </summary>
public static class ODataModelGenerator
{
    private static readonly XNamespace Edm = "http://docs.oasis-open.org/odata/ns/edm";
    private static readonly XNamespace Edmx = "http://docs.oasis-open.org/odata/ns/edmx";

    private static readonly Regex SimpleIdentifier = new(
        @"\A[_\p{L}\p{Nl}][_\p{L}\p{Nl}\p{Nd}\p{Pc}\p{Mn}\p{Mc}]*\z", RegexOptions.NonBacktracking);

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
        "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        "__arglist", "__makeref", "__reftype", "__refvalue", "dynamic", "file", "global", "nint", "nuint", "record",
        "required", "scoped", "var",
    };

    /// <summary>
    /// Generates one C# source file containing the entity, complex, and enumeration types in the document.
    /// </summary>
    /// <param name="metadata">A readable stream containing OData 4.0 or 4.01 XML metadata. The stream stays open.</param>
    /// <param name="targetNamespace">The C# namespace prefix to place before each OData schema namespace.</param>
    /// <returns>The generated C# source, with nullable reference types enabled.</returns>
    /// <exception cref="InvalidDataException">The document uses an unsupported schema or cannot produce valid models.</exception>
    /// <exception cref="XmlException">The document cannot be read as XML.</exception>
    public static string Generate(Stream metadata, string targetNamespace)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNamespace);
        var namespacePrefix = QualifiedIdentifier(targetNamespace);

        using var reader = XmlReader.Create(metadata, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024,
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != Edmx + "Edmx" || (string?)root.Attribute("Version") is not ("4.0" or "4.01") ||
            root.Element(Edmx + "DataServices") is not { } services)
        {
            throw new InvalidDataException("Expected an OData 4.0 or 4.01 EDMX XML metadata document.");
        }

        var schemas = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var schemaPrefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schema in services.Elements(Edm + "Schema"))
        {
            var name = Attribute(schema, "Namespace");
            _ = QualifiedIdentifier(name);
            if (!schemas.TryAdd(name, schema))
            {
                throw new InvalidDataException($"Duplicate OData schema namespace '{name}'.");
            }

            for (var prefix = name; schemaPrefixes.Add(prefix);)
            {
                var separator = prefix.LastIndexOf('.');
                if (separator < 0)
                {
                    break;
                }
                prefix = prefix[..separator];
            }
        }

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var types = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var (schemaName, schema) in schemas)
        {
            if ((string?)schema.Attribute("Alias") is { } alias)
            {
                _ = Identifier(alias);
                if (alias == "Edm" || schemas.ContainsKey(alias) || !aliases.TryAdd(alias, schemaName))
                {
                    throw new InvalidDataException($"Conflicting OData schema alias '{alias}'.");
                }
            }

            foreach (var type in DeclaredTypes(schema))
            {
                var name = Attribute(type, "Name");
                _ = Identifier(name);
                var fullName = $"{schemaName}.{name}";
                if (!types.TryAdd(fullName, type))
                {
                    throw new InvalidDataException($"Duplicate OData type '{fullName}'.");
                }
            }
        }

        var source = new StringBuilder().AppendLine("// <auto-generated/>").AppendLine("#nullable enable");
        var emitted = 0;
        var checkedInheritance = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (schemaName, schema) in schemas)
        {
            var models = DeclaredTypes(schema).Where(type => type.Name != Edm + "TypeDefinition").ToArray();
            if (models.Length == 0)
            {
                continue;
            }

            source.AppendLine().AppendLine($"namespace {namespacePrefix}.{QualifiedIdentifier(schemaName)}").AppendLine("{");
            foreach (var model in models)
            {
                var modelName = Attribute(model, "Name");
                var fullName = $"{schemaName}.{modelName}";
                if (schemaPrefixes.Contains(fullName))
                {
                    throw new InvalidDataException($"OData type '{fullName}' conflicts with a schema namespace.");
                }

                if (model.Name == Edm + "EnumType")
                {
                    AppendEnum(source, model, modelName);
                }
                else
                {
                    AppendClass(model, modelName, fullName);
                }
                emitted++;
            }
            source.AppendLine("}");
        }

        return emitted > 0 ? source.ToString() : throw new InvalidDataException("The metadata contains no model types.");

        void AppendClass(XElement model, string modelName, string fullName)
        {
            var baseName = BaseName(model);
            var chain = new HashSet<string>(StringComparer.Ordinal) { fullName };
            for (var ancestor = baseName; ancestor is not null && !checkedInheritance.Contains(ancestor);
                ancestor = BaseName(types[ancestor]))
            {
                if (!chain.Add(ancestor))
                {
                    throw new InvalidDataException($"Circular OData inheritance for '{fullName}'.");
                }
            }
            checkedInheritance.UnionWith(chain);

            var modifier = BooleanAttribute(model, "Abstract", false) ? "abstract " : "";
            var inheritance = baseName is null ? "" : $" : {ModelTypeName(baseName)}";
            source.AppendLine($"    public {modifier}partial class {Identifier(modelName)}{inheritance}").AppendLine("    {");
            var members = new HashSet<string>(StringComparer.Ordinal) { modelName };
            foreach (var property in model.Elements().Where(element =>
                element.Name == Edm + "Property" || element.Name == Edm + "NavigationProperty"))
            {
                var name = Attribute(property, "Name");
                if (!members.Add(name))
                {
                    throw new InvalidDataException($"Conflicting OData property name '{fullName}.{name}'.");
                }

                var declaredType = Attribute(property, "Type");
                var collection = declaredType.StartsWith("Collection(", StringComparison.Ordinal) && declaredType.EndsWith(')');
                var item = ResolveType(collection ? declaredType[11..^1] : declaredType, $"{fullName}.{name}");
                var nullable = !(collection && property.Name == Edm + "NavigationProperty") &&
                    BooleanAttribute(property, "Nullable", true);
                var typeName = item.Name + (nullable ? "?" : "") + (collection ? "[]" : "");
                var initializer = collection ? " = [];" : "";
                var required = !collection && item.IsReferenceType && !nullable ? "required " : "";
                var hiding = name is "Equals" or "Finalize" or "GetHashCode" or "GetType" or "MemberwiseClone"
                    or "ReferenceEquals" or "ToString" ? "new " : "";
                source.AppendLine($"        public {hiding}{required}{typeName} {Identifier(name)} {{ get; set; }}{initializer}");
            }
            source.AppendLine("    }").AppendLine();
        }

        string? BaseName(XElement derived)
        {
            if ((string?)derived.Attribute("BaseType") is not { } declaredBase)
            {
                return null;
            }
            var name = ExpandAlias(declaredBase);
            if (!types.TryGetValue(name, out var model) || model.Name != derived.Name)
            {
                throw new InvalidDataException($"Unsupported OData base type '{declaredBase}' for '{Attribute(derived, "Name")}'.");
            }
            return name;
        }

        ClrType ResolveType(string declaredType, string property)
        {
            var name = ExpandAlias(declaredType);
            if (types.TryGetValue(name, out var type))
            {
                if (type.Name != Edm + "TypeDefinition")
                {
                    return new(ModelTypeName(name), type.Name != Edm + "EnumType");
                }
                declaredType = Attribute(type, "UnderlyingType");
            }
            return Primitive(declaredType) ??
                throw new InvalidDataException($"Unsupported OData type '{declaredType}' for '{property}'.");
        }

        string ExpandAlias(string name)
        {
            var separator = name.LastIndexOf('.');
            return separator > 0 && aliases.TryGetValue(name[..separator], out var schemaName)
                ? schemaName + name[separator..]
                : name;
        }

        string ModelTypeName(string name) => $"global::{namespacePrefix}.{QualifiedIdentifier(name)}";
    }

    private static void AppendEnum(StringBuilder source, XElement model, string modelName)
    {
        var underlying = (string?)model.Attribute("UnderlyingType") ?? "Edm.Int32";
        var type = Primitive(underlying)?.Name;
        (long Min, long Max) bounds = type switch
        {
            "byte" => (byte.MinValue, byte.MaxValue),
            "sbyte" => (sbyte.MinValue, sbyte.MaxValue),
            "short" => (short.MinValue, short.MaxValue),
            "int" => (int.MinValue, int.MaxValue),
            "long" => (long.MinValue, long.MaxValue),
            _ => throw new InvalidDataException($"Unsupported OData enum base '{underlying}' for '{modelName}'."),
        };
        var flags = BooleanAttribute(model, "IsFlags", false);
        if (flags)
        {
            source.AppendLine("    [global::System.FlagsAttribute]");
        }
        source.AppendLine($"    public enum {Identifier(modelName)} : {type}").AppendLine("    {");
        var names = new HashSet<string>(StringComparer.Ordinal) { modelName, "value__" };
        var members = model.Elements(Edm + "Member").ToArray();
        var explicitValues = flags || members.Any(member => member.Attribute("Value") is not null);
        for (var i = 0; i < members.Length; i++)
        {
            var name = Attribute(members[i], "Name");
            if (!names.Add(name))
            {
                throw new InvalidDataException($"Conflicting OData enum member name '{modelName}.{name}'.");
            }
            long value = i;
            if ((explicitValues && !long.TryParse((string?)members[i].Attribute("Value"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value)) || value < (flags ? 0 : bounds.Min) || value > bounds.Max)
            {
                throw new InvalidDataException($"Invalid OData enum value for '{modelName}.{name}'.");
            }
            source.AppendLine($"        {Identifier(name)} = {value.ToString(CultureInfo.InvariantCulture)},");
        }
        source.AppendLine("    }").AppendLine();
    }

    private static IEnumerable<XElement> DeclaredTypes(XElement schema) =>
        schema.Elements().Where(type => type.Name == Edm + "EntityType" || type.Name == Edm + "ComplexType" ||
            type.Name == Edm + "EnumType" || type.Name == Edm + "TypeDefinition");

    private static string Attribute(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value ? value :
            throw new InvalidDataException($"OData element '{element.Name.LocalName}' requires '{name}'.");

    private static bool BooleanAttribute(XElement element, string name, bool defaultValue) =>
        (string?)element.Attribute(name) switch
        {
            "true" => true,
            "false" => false,
            null => defaultValue,
            _ => throw new InvalidDataException($"Invalid OData '{name}' value on '{Attribute(element, "Name")}'."),
        };

    private static string QualifiedIdentifier(string name) => string.Join('.', name.Split('.').Select(Identifier));

    private static string Identifier(string name)
    {
        if (!SimpleIdentifier.IsMatch(name))
        {
            throw new InvalidDataException($"'{name}' is not a supported C# identifier.");
        }
        return Keywords.Contains(name) ? "@" + name : name;
    }

    private static ClrType? Primitive(string name) => name switch
    {
        "Edm.Binary" => new("byte[]", true),
        "Edm.Boolean" => new("bool"),
        "Edm.Byte" => new("byte"),
        "Edm.Date" => new("global::System.DateOnly"),
        "Edm.DateTimeOffset" => new("global::System.DateTimeOffset"),
        "Edm.Decimal" => new("decimal"),
        "Edm.Double" => new("double"),
        "Edm.Duration" => new("global::System.TimeSpan"),
        "Edm.Guid" => new("global::System.Guid"),
        "Edm.Int16" => new("short"),
        "Edm.Int32" => new("int"),
        "Edm.Int64" => new("long"),
        "Edm.SByte" => new("sbyte"),
        "Edm.Single" => new("float"),
        "Edm.Stream" => new("global::System.IO.Stream", true),
        "Edm.String" => new("string", true),
        "Edm.TimeOfDay" => new("global::System.TimeOnly"),
        "Edm.Untyped" => new("global::System.Text.Json.JsonElement"),
        _ => null,
    };

    private readonly record struct ClrType(string Name, bool IsReferenceType = false);
}
