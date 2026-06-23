using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

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
        EnsureAllowed(path, allowedRoots);
        var files = EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);

            // Recursive enumeration can traverse links, so validate where each file actually lives.
            EnsureAllowed(info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName, allowedRoots);
            yield return new SourceItem(
                info.FullName,
                info.FullName,
                Scheme,
                info.Name,
                DocumentSourceSupport.NormalizeExtension(info.Name),
                new Dictionary<string, string>
                {
                    ["path"] = info.FullName
                });
            await Task.Yield();
        }
    }

    private IReadOnlyList<string> AllowedRoots()
    {
        return options.Value.AllowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)))
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
                candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            {
                return;
            }
        }

        throw new SourcePathNotAllowedException();
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
}
