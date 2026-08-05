using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Azimuth.Emit;

/// <summary>
/// Reflects over assemblies and collects their linkage tags into the language-neutral manifest the
/// core reads.
/// </summary>
/// <remarks>
/// Each ecosystem emits the manifest natively; the core only ever reads manifests. That seam is why
/// adding a language is a day's work rather than a fork of the core, and why the core can stay
/// dependency-free while this side does metadata work.
/// <para>
/// Attributes are matched by full <em>name</em> rather than CLR identity, so the emitter works when
/// the target assembly references a differently-located copy of Azimuth.Annotations.
/// </para>
/// </remarks>
internal static class Collector
{
    private const string Lang = "csharp";
    private const string RealizesName = "Azimuth.Annotations.RealizesAttribute";
    private const string CoversName = "Azimuth.Annotations.CoversAttribute";
    private const string UntracedName = "Azimuth.Annotations.UntracedAttribute";

    /// <summary>
    /// Matched by simple name so no test-framework reference is needed. A list rather than one
    /// hard-wired name, so another framework can be added without touching the walk.
    /// </summary>
    private static readonly HashSet<string> TestAttributes = new(StringComparer.Ordinal)
    {
        "FactAttribute",
        "TheoryAttribute",
        "TestAttribute",
        "TestMethodAttribute",
    };

    private const BindingFlags Members = BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    public sealed record Entry(
        string Spec,
        string Scenario,
        string Site,
        string File,
        string? Scope,
        string? Quantification,
        string? Oracle);

    public sealed record Untraced(string Site, string File);

    public sealed record Result(
        List<Entry> Realizes,
        List<Entry> Covers,
        List<Untraced> UntracedTests,
        List<string> Warnings);

    public static Result Collect(
        IEnumerable<Assembly> assemblies,
        string root,
        IReadOnlyList<string> tracedRoots)
    {
        var result = new Result([], [], [], []);

        foreach (var assembly in assemblies)
        {
            using var files = SourceFiles.ForAssembly(assembly, root);
            if (!files.HasSymbols)
            {
                result.Warnings.Add(
                    $"{assembly.GetName().Name}: no portable PDB beside the assembly, so tags "
                    + "carry no file path and findings will not be navigable");
            }

            foreach (var type in Types(assembly, result.Warnings))
            {
                CollectType(type, files, tracedRoots, result);
            }
        }

        result.Realizes.Sort(Compare);
        result.Covers.Sort(Compare);
        result.UntracedTests.Sort((a, b) => string.CompareOrdinal(a.Site, b.Site));
        return result;
    }

    private static IEnumerable<Type> Types(Assembly assembly, List<string> warnings)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            warnings.Add(
                $"{assembly.GetName().Name}: {e.LoaderExceptions.Length} type(s) failed to load "
                + "and were skipped; tags on them are missing from this manifest");
            return e.Types.Where(t => t is not null)!;
        }
    }

    private static void CollectType(
        Type type,
        SourceFiles files,
        IReadOnlyList<string> tracedRoots,
        Result result)
    {
        foreach (var attribute in type.GetCustomAttributesData())
        {
            if (FullName(attribute) != RealizesName)
            {
                continue;
            }

            var (spec, scenario) = Pair(attribute);
            result.Realizes.Add(
                new Entry(spec, scenario, type.FullName ?? type.Name, files.PathOf(type), null, null, null));
        }

        var traced = IsTraced(type, tracedRoots);

        foreach (var method in type.GetMethods(Members))
        {
            var site = $"{type.FullName}.{method.Name}";
            var file = files.PathOf(method);
            var data = method.GetCustomAttributesData();
            var covered = false;
            var exempt = false;

            foreach (var attribute in data)
            {
                var name = FullName(attribute);
                if (name == RealizesName)
                {
                    var (spec, scenario) = Pair(attribute);
                    result.Realizes.Add(new Entry(spec, scenario, site, file, null, null, null));
                }
                else if (name == CoversName)
                {
                    covered = true;
                    result.Covers.Add(Covers(attribute, site, file));
                }
                else if (name == UntracedName)
                {
                    exempt = true;
                }
            }

            // The dual of an uncovered claim: a test in a traced area that declares nothing and is
            // not exempt. Only meaningful under an opt-in root — holding every test in a repo to
            // this would be noise, and D8's ratchet is what makes partial adoption work.
            if (traced && !covered && !exempt && IsTest(data))
            {
                result.UntracedTests.Add(new Untraced(site, file));
            }
        }
    }

    private static bool IsTraced(Type type, IReadOnlyList<string> tracedRoots)
    {
        if (tracedRoots.Count == 0)
        {
            return false;
        }

        var ns = type.Namespace ?? string.Empty;
        return tracedRoots.Any(root =>
            ns.Equals(root, StringComparison.Ordinal)
            || ns.StartsWith(root + ".", StringComparison.Ordinal));
    }

    private static bool IsTest(IList<CustomAttributeData> data) =>
        data.Any(a => TestAttributes.Contains(a.AttributeType.Name));

    private static string FullName(CustomAttributeData attribute) =>
        attribute.AttributeType.FullName ?? string.Empty;

    private static (string Spec, string Scenario) Pair(CustomAttributeData attribute)
    {
        var args = attribute.ConstructorArguments;
        var spec = args.Count > 0 ? args[0].Value as string ?? string.Empty : string.Empty;
        var scenario = args.Count > 1 ? args[1].Value as string ?? string.Empty : string.Empty;
        return (spec, scenario);
    }

    private static Entry Covers(CustomAttributeData attribute, string site, string file)
    {
        var args = attribute.ConstructorArguments;
        var (spec, scenario) = Pair(attribute);
        return new Entry(
            spec,
            scenario,
            site,
            file,
            args.Count > 2 ? EnumName(args[2]) : null,
            args.Count > 3 ? EnumName(args[3]) : null,
            args.Count > 4 ? EnumName(args[4]) : null);
    }

    /// <summary>
    /// Enum arguments arrive through <c>CustomAttributeData</c> as their boxed underlying integer,
    /// so the name is resolved against the declared argument type rather than read off the value.
    /// </summary>
    private static string? EnumName(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is null)
        {
            return null;
        }

        var type = argument.ArgumentType;
        if (!type.IsEnum)
        {
            return Kebab(argument.Value.ToString());
        }

        var name = Enum.GetName(type, argument.Value);
        return name is null ? null : Kebab(name);
    }

    /// <summary>
    /// The manifest speaks kebab-case because the specs do. A format that spoke two casings would
    /// invite exactly one bug, at the boundary, in the field nobody re-reads.
    /// </summary>
    private static string? Kebab(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var (c, index) in value.Select((c, i) => (c, i)))
        {
            if (char.IsUpper(c) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static int Compare(Entry a, Entry b)
    {
        var bySpec = string.CompareOrdinal(a.Spec, b.Spec);
        if (bySpec != 0)
        {
            return bySpec;
        }

        var byScenario = string.CompareOrdinal(a.Scenario, b.Scenario);
        return byScenario != 0 ? byScenario : string.CompareOrdinal(a.Site, b.Site);
    }

    public static string ToJson(Result result)
    {
        var options = new JsonWriterOptions { Indented = true };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            WriteEntries(writer, "realizes", result.Realizes, form: false);
            WriteEntries(writer, "covers", result.Covers, form: true);
            writer.WriteStartArray("untraced_tests");
            foreach (var test in result.UntracedTests)
            {
                writer.WriteStartObject();
                writer.WriteString("site", test.Site);
                writer.WriteString("file", test.File);
                writer.WriteString("lang", Lang);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteEntries(
        Utf8JsonWriter writer,
        string name,
        List<Entry> entries,
        bool form)
    {
        writer.WriteStartArray(name);
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("spec", entry.Spec);
            writer.WriteString("scenario", entry.Scenario);
            writer.WriteString("site", entry.Site);
            writer.WriteString("file", entry.File);
            writer.WriteString("lang", Lang);
            if (form)
            {
                if (entry.Scope is not null)
                {
                    writer.WriteString("scope", entry.Scope);
                }

                if (entry.Quantification is not null)
                {
                    writer.WriteString("quantification", entry.Quantification);
                }

                if (entry.Oracle is not null)
                {
                    writer.WriteString("oracle", entry.Oracle);
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
