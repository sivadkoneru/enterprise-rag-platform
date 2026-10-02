using Rag.Core.Configuration;

namespace Rag.Core.Workbench;

public static class WorkbenchModelIdentity
{
    public static string EmbeddingModel(LlmOptions options) => options.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ? "deterministic" : options.EmbeddingModel?.Trim() ?? "";
    public static string ChatModel(LlmOptions options) => options.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ? "deterministic-extractive" : options.ChatModel?.Trim() ?? "";
}
