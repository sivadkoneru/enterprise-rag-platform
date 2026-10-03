using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Rag.Core.Common;

/// <summary>Content-free instrumentation. Hosts choose sampling, exporters and retention.</summary>
public static class RagTelemetry
{
    public static readonly ActivitySource Activities = new("Rag.Workbench", "0.1.0");
    public static readonly Meter Meter = new("Rag.Workbench", "0.1.0");
    public static readonly Histogram<double> StageLatency = Meter.CreateHistogram<double>("rag.stage.duration", "ms");
    public static readonly Counter<long> ProviderFailures = Meter.CreateCounter<long>("rag.provider.failures");
    public static readonly Counter<long> Tokens = Meter.CreateCounter<long>("rag.provider.tokens", "token");
    public static readonly Histogram<int> QueueDepth = Meter.CreateHistogram<int>("rag.jobs.pending", "job");
}
