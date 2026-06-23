using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Llm;

public sealed class DeterministicLlmClient(IOptions<LlmOptions> options) : IEmbeddingClient, IChatClient
{
    public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dimensions = Math.Max(1, options.Value.EmbeddingDimensions);
        var vector = new float[dimensions];
        foreach (var token in Tokenize(input))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt16(hash, 0) % dimensions;
            vector[index] += 1f;
        }

        Normalize(vector);
        return Task.FromResult<IReadOnlyList<float>>(vector);
    }

    public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = messages.LastOrDefault(message => message.Role == "user")?.Content ?? string.Empty;
        var answer = HasContext(context)
            ? $"Based on the retrieved context, {FirstContextSentence(context)}"
            : "I don't know based on the supplied context.";
        return Task.FromResult(answer);
    }

    private static bool HasContext(string prompt)
    {
        var marker = prompt.IndexOf("Context:", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return false;
        }

        var context = prompt[(marker + "Context:".Length)..].Trim();
        return context.Length > 0;
    }

    private static string FirstContextSentence(string input)
    {
        var contextIndex = input.IndexOf("Context:", StringComparison.OrdinalIgnoreCase);
        var context = contextIndex < 0 ? input : input[(contextIndex + "Context:".Length)..].Trim();
        var firstContentLine = context
            .Split(["\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => !line.StartsWith("[", StringComparison.Ordinal));
        var selected = string.IsNullOrWhiteSpace(firstContentLine) ? context : firstContentLine;
        return selected.Length == 0
            ? "I don't know based on the supplied context."
            : FirstSentence(selected);
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        return input.ToLowerInvariant()
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        if (magnitude <= 0)
        {
            return;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / magnitude);
        }
    }

    private static string FirstSentence(string input)
    {
        var sentenceEnd = input.IndexOfAny(['.', '!', '?']);
        return sentenceEnd < 0 ? input : input[..(sentenceEnd + 1)];
    }
}
