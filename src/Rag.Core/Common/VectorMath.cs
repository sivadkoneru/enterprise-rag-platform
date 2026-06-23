namespace Rag.Core.Common;

/// <summary>
/// Cosine similarity over two vectors, shared by the in-memory vector store's nearest-neighbor
/// search and the semantic chunking strategy's paragraph-boundary distance calculation.
/// </summary>
internal static class VectorMath
{
    public static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
