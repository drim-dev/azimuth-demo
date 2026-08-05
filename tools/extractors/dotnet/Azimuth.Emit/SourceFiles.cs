using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Azimuth.Emit;

/// <summary>
/// Maps a method back to the source file it was compiled from, by reading the assembly's portable
/// PDB.
/// </summary>
/// <remarks>
/// The <c>file</c> field is what makes a finding navigable — a dangling tag reported against
/// "some assembly" is a finding nobody acts on. Best-effort: an assembly built without a portable
/// PDB yields no path, and the emitter says so rather than inventing one.
/// </remarks>
internal sealed class SourceFiles : IDisposable
{
    private readonly MetadataReaderProvider? _provider;
    private readonly MetadataReader? _reader;
    private readonly string _root;

    private SourceFiles(MetadataReaderProvider? provider, MetadataReader? reader, string root)
    {
        _provider = provider;
        _reader = reader;
        _root = root;
    }

    public bool HasSymbols => _reader is not null;

    public static SourceFiles ForAssembly(Assembly assembly, string root)
    {
        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            return new SourceFiles(null, null, root);
        }

        var pdbPath = Path.ChangeExtension(location, ".pdb");
        if (!File.Exists(pdbPath))
        {
            return new SourceFiles(null, null, root);
        }

        try
        {
            var stream = File.OpenRead(pdbPath);
            var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            return new SourceFiles(provider, provider.GetMetadataReader(), root);
        }
        catch (Exception)
        {
            // A native or Windows PDB, or a truncated file. Degrade to no paths rather than
            // failing the emit: a manifest without file paths is still a correct manifest.
            return new SourceFiles(null, null, root);
        }
    }

    public string PathOf(MethodBase method)
    {
        var direct = Lookup(method);
        if (direct.Length > 0)
        {
            return direct;
        }

        // An async or iterator method's sequence points live on the compiler-generated state
        // machine's MoveNext, not on the method the tag sits on. Most tagged methods in a service
        // are async, so without this the manifest carries almost no paths — which is invisible
        // until something depends on them.
        var stateMachine =
            (method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType)
            ?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
        if (stateMachine is null)
        {
            return string.Empty;
        }

        var moveNext = stateMachine.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return moveNext is null ? string.Empty : Lookup(moveNext);
    }

    private string Lookup(MethodBase method)
    {
        if (_reader is null)
        {
            return string.Empty;
        }

        try
        {
            // A MethodDebugInformation row corresponds 1:1 with a MethodDef row, and the handle is
            // built from the *row number* — not the full token. Passing the token silently yielded
            // no path for most methods, which the fingerprint work surfaced: judgments would have
            // been fingerprinted over an empty evidence set and never gone stale.
            var row = method.MetadataToken & 0x00FFFFFF;
            var handle = MetadataTokens.MethodDebugInformationHandle(row);
            var info = _reader.GetMethodDebugInformation(handle);
            if (info.Document.IsNil)
            {
                return string.Empty;
            }

            var document = _reader.GetDocument(info.Document);
            var name = _reader.GetString(document.Name);
            return Relative(name);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>A type's path is taken from its first method that has one.</summary>
    public string PathOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            var path = PathOf(method);
            if (path.Length > 0)
            {
                return path;
            }
        }

        return string.Empty;
    }

    private string Relative(string absolute)
    {
        var normalized = absolute.Replace('\\', '/');
        var root = _root.Replace('\\', '/').TrimEnd('/');
        if (root.Length > 0 && normalized.StartsWith(root + "/", StringComparison.Ordinal))
        {
            return normalized[(root.Length + 1)..];
        }

        return normalized;
    }

    public void Dispose() => _provider?.Dispose();

    public static ImmutableArray<string> Empty => ImmutableArray<string>.Empty;
}
