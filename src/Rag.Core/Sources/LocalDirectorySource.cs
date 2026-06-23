using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Sources;

public sealed class LocalDirectorySource : IDocumentSource
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
        var path = ToPath(sourceUri);
        var files = EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
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
