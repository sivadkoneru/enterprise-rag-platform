namespace Rag.Core.Models;

public sealed record IngestionRequest(string? Path = null, string? Strategy = null, IReadOnlyList<string>? Sources = null)
{
    /// <summary>
    /// Collapses "either <see cref="Sources"/> or a single <see cref="Path"/>" into one list, trimming
    /// whitespace and dropping blank entries so every caller (API, CLI, jobs) sees the same normalized
    /// list. When <see cref="Sources"/> is supplied but every entry is blank, this intentionally returns
    /// an empty list rather than falling back to <see cref="Path"/>: an explicit (if empty) source list
    /// should not be silently replaced.
    /// </summary>
    public IReadOnlyList<string> SourceUris
    {
        get
        {
            if (Sources is { Count: > 0 })
            {
                return Sources.Where(source => !string.IsNullOrWhiteSpace(source)).Select(source => source.Trim()).ToArray();
            }

            return string.IsNullOrWhiteSpace(Path) ? [] : [Path.Trim()];
        }
    }
}
