namespace Rag.Core.Models;

/// <summary>
/// Thrown when a running ingestion job observes that it has been canceled, so the pipeline can
/// unwind cooperatively and the job can be recorded as canceled rather than failed.
/// </summary>
public sealed class IngestionJobCanceledException : Exception
{
    private const string DefaultMessage = "Ingestion job was canceled.";

    public IngestionJobCanceledException()
        : base(DefaultMessage)
    {
    }

    public IngestionJobCanceledException(string message)
        : base(message)
    {
    }

    public IngestionJobCanceledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
