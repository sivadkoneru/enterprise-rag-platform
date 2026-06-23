namespace Rag.Core.Models;

/// <summary>
/// Thrown when a local source path falls outside the configured allowed ingestion roots.
/// The offending path is deliberately not echoed back to callers.
/// </summary>
public sealed class SourcePathNotAllowedException : Exception
{
    private const string DefaultMessage = "The requested path is outside the allowed ingestion roots.";

    public SourcePathNotAllowedException()
        : base(DefaultMessage)
    {
    }

    public SourcePathNotAllowedException(string message)
        : base(message)
    {
    }

    public SourcePathNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
