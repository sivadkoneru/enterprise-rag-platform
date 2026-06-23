using System.Security.Cryptography;
using System.Text;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Llm;

public sealed class DeterministicLlmClient : IEmbeddingClient, IChatClient
{
    private const int Dimensions = 256;

    public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[Dimensions];
        foreach (var token in Tokenize(input))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt16(hash, 0) % Dimensions;
            vector[index] += 1f;
        }

        Normalize(vector);
        return Task.FromResult<IReadOnlyList<float>>(vector);
    }

    public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = messages.LastOrDefault(message => message.Role == "user")?.Content ?? string.Empty;
        var answer = context.Length == 0
            ? "No question was provided."
            : $"Based on the retrieved context, {FirstSentence(context)}";
        return Task.FromResult(answer);
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
