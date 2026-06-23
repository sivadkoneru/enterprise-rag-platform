namespace Rag.Core.Models;

/// <summary>
/// Thrown when a running ingestion job observes that it has been paused, so the pipeline can
/// unwind cooperatively and the job can later be resumed from a queued state.
/// </summary>
public sealed class IngestionJobPausedException : Exception
{
    private const string DefaultMessage = "Ingestion job was paused.";

    public IngestionJobPausedException()
        : base(DefaultMessage)
    {
    }

    public IngestionJobPausedException(string message)
        : base(message)
    {
    }

    public IngestionJobPausedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
