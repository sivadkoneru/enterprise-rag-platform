using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Parsing;

namespace Rag.Core.Sources;

public sealed class LocalDirectorySource(IOptions<LocalSourceOptions> options) : IDocumentSource
{
    public string Scheme => "file";

    public bool CanRead(string sourceUri)
    {
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri))
        {
            return true;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<SourceItem> EnumerateAsync(
        string sourceUri,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var allowedRoots = AllowedRoots();
        var path = Path.GetFullPath(ToPath(sourceUri));
        EnsureAllowed(ResolvePhysicalPath(path), allowedRoots);
        var files = EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);

            // Recursive enumeration can traverse links, so validate where each file actually lives.
            EnsureAllowed(ResolvePhysicalPath(file), allowedRoots);
            yield return new SourceItem(
                info.FullName,
                info.FullName,
                Scheme,
                info.Name,
                DocumentSourceSupport.NormalizeExtension(info.Name),
                Attributes(info.FullName, allowedRoots));
            await Task.Yield();
        }
    }

    private IReadOnlyList<string> AllowedRoots()
    {
        return options.Value.AllowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.TrimEndingDirectorySeparator(ResolvePhysicalPath(root)))
            .ToArray();
    }

    private static void EnsureAllowed(string fullPath, IReadOnlyList<string> allowedRoots)
    {
        if (allowedRoots.Count == 0)
        {
            return;
        }

        var candidate = Path.TrimEndingDirectorySeparator(fullPath);
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var root in allowedRoots)
        {
            if (candidate.Equals(root, comparison) ||
                candidate.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, comparison))
            {
                return;
            }
        }

        throw new SourcePathNotAllowedException();
    }

    // Resolve each ancestor: FileInfo.ResolveLinkTarget alone misses directory symlinks.
    private static string ResolvePhysicalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var segment in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists || info.LinkTarget is not null) { current = info.ResolveLinkTarget(true)?.FullName ?? current; }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    private static string ToPath(string sourceUri)
    {
        return Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            ? uri.LocalPath
            : sourceUri;
    }

    private static IReadOnlyList<string> EnumerateFiles(string path)
    {
        if (File.Exists(path))
        {
            if (!DocumentSourceSupport.IsSupported(path))
            {
                return [];
            }

            return [path];
        }

        if (!Directory.Exists(path))
        {
            throw new FileNotFoundException($"Path '{path}' does not exist.");
        }

        return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(DocumentSourceSupport.IsSupported)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> Attributes(string path, IReadOnlyList<string> roots)
    {
        var attributes = new Dictionary<string, string>
        {
            ["path"] = path,
            ["schemaResolved"] = "true",
            [StructuredSchemaLoader.SourceFileNameAttribute] = Path.GetFileName(path)
        };

        var schema = StructuredSchemaLoader.FindLocalSchema(path, candidate => { try { EnsureAllowed(ResolvePhysicalPath(candidate), roots); return true; } catch (SourcePathNotAllowedException) { return false; } });
        if (!string.IsNullOrWhiteSpace(schema))
        {
            attributes[StructuredSchemaLoader.SchemaPathAttribute] = ResolvePhysicalPath(schema);
        }

        return attributes;
    }
}
