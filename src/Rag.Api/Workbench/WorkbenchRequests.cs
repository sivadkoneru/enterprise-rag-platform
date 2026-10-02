using Rag.Core.Workbench;

namespace Rag.Api.Workbench;

public sealed record ApiDetailedQueryRequest(string Question, string CorpusId, string ProfileId, int? TopK = null, string? Mode = null, bool? Reranker = null, double? MinRelevance = null, bool? Neighbors = null, int? MaxContextTokens = null)
{
    public DetailedQueryRequest ToCore(WorkbenchQueryOptions defaults) => new(Question, CorpusId, ProfileId, TopK ?? defaults.TopK, Mode ?? defaults.Mode, Reranker ?? defaults.Reranker, MinRelevance ?? defaults.MinRelevance, Neighbors ?? defaults.Neighbors, MaxContextTokens ?? defaults.MaxContextTokens);
}
public sealed record ApiLiveEvaluationRequest(IReadOnlyList<string> ProfileIds, IReadOnlyList<LiveEvaluationQuestion> Questions, int? TopK = null, string? Mode = null, bool? Reranker = null, double? MinRelevance = null, bool? Neighbors = null, int? MaxContextTokens = null)
{
    public LiveEvaluationRequest ToCore(WorkbenchQueryOptions defaults) => new(ProfileIds, Questions, TopK ?? defaults.TopK, Mode ?? defaults.Mode, Reranker ?? defaults.Reranker, MinRelevance ?? defaults.MinRelevance, Neighbors ?? defaults.Neighbors, MaxContextTokens ?? defaults.MaxContextTokens);
}
