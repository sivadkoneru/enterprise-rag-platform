namespace Rag.Core.Models;

public enum IngestionJobStatus
{
    Queued,
    Running,
    Paused,
    Canceled,
    Succeeded,
    Failed
}
