namespace Rag.Core.Models;

public sealed class SourceItem : IAsyncDisposable
{
    private readonly Func<ValueTask>? _cleanup;

    public SourceItem(
        string localPath,
        string source,
        string origin,
        string fileName,
        string extension,
        IReadOnlyDictionary<string, string>? attributes = null,
        Func<ValueTask>? cleanup = null)
    {
        LocalPath = localPath;
        Source = source;
        Origin = origin;
        FileName = fileName;
        Extension = extension;
        Attributes = attributes;
        _cleanup = cleanup;
    }

    public string LocalPath { get; }

    public string Source { get; }

    public string Origin { get; }

    public string FileName { get; }

    public string Extension { get; }

    public IReadOnlyDictionary<string, string>? Attributes { get; }

    public ValueTask DisposeAsync()
    {
        return _cleanup is null ? ValueTask.CompletedTask : _cleanup();
    }
}
