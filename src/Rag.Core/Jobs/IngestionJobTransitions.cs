using Rag.Core.Models;

namespace Rag.Core.Jobs;

/// <summary>
/// Shared status-transition semantics for <see cref="Rag.Core.Abstractions.IIngestionJobStore"/> implementations.
/// Keeping these rules in one place stops the in-memory store and provider-package-backed stores
/// (for example Rag.Providers.Mongo) from drifting apart. Public because
/// <see cref="Rag.Core.Abstractions.IIngestionJobStore"/> implementations can live in separate
/// provider assemblies outside Rag.Core.
/// </summary>
public static class IngestionJobTransitions
{
    public static readonly IReadOnlyList<IngestionJobStatus> TerminalStatuses =
    [
        IngestionJobStatus.Canceled,
        IngestionJobStatus.Succeeded,
        IngestionJobStatus.Failed
    ];

    public static bool IsTerminal(IngestionJobStatus status)
    {
        return TerminalStatuses.Contains(status);
    }

    public static int BackfillProcessedSourceCount(int totalSourceCount, int processedSourceCount)
    {
        return totalSourceCount > 0 ? totalSourceCount : processedSourceCount;
    }
}
