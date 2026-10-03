namespace Rag.Core.Common;

public static class EmbeddingValidation
{
    public static void Validate(IReadOnlyList<float> vector, int dimensions)
    {
        if (vector.Count == 0 || vector.Count != dimensions || vector.Any(value => !float.IsFinite(value)) || !vector.Any(value => value != 0))
        {
            throw new InvalidDataException("Embedding must be finite, nonzero, and match the configured dimensions.");
        }
    }
}
