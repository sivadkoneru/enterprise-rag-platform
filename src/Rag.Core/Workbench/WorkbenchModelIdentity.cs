using System.Security.Cryptography;
using System.Text;
using Rag.Core.Configuration;

namespace Rag.Core.Workbench;

public static class WorkbenchModelIdentity
{
    public static string SystemPromptHash(LlmOptions options) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.SystemPrompt)));
    public static string EmbeddingModel(LlmOptions options) => options.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ? "deterministic" : options.EmbeddingModel?.Trim() ?? "";
    public static string ChatModel(LlmOptions options) => options.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ? "deterministic-extractive" : options.ChatModel?.Trim() ?? "";
}
