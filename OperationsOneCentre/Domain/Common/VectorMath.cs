using System.Numerics;
using System.Runtime.InteropServices;

namespace OperationsOneCentre.Domain.Common;

/// <summary>
/// High-performance vector math utilities for semantic search.
/// Uses SIMD acceleration when available for cosine similarity calculations.
/// Replaces 5+ duplicated implementations across the codebase.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Calculate cosine similarity between two float arrays.
    /// Returns 0 if either vector is null, empty, or has zero magnitude.
    /// </summary>
    public static double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA is null || vectorB is null || vectorA.Length != vectorB.Length || vectorA.Length == 0)
            return 0;

        return CosineSimilarity(vectorA.AsSpan(), vectorB.AsSpan());
    }

    /// <summary>
    /// Calculate cosine similarity between two ReadOnlyMemory vectors.
    /// </summary>
    public static double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        return CosineSimilarity(a.Span, b.Span);
    }

    /// <summary>
    /// SIMD-accelerated cosine similarity between two float spans.
    /// Falls back to scalar computation when SIMD is not available.
    /// </summary>
    public static double CosineSimilarity(ReadOnlySpan<float> spanA, ReadOnlySpan<float> spanB)
    {
        if (spanA.Length != spanB.Length || spanA.Length == 0)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        // Use SIMD acceleration when possible
        if (Vector.IsHardwareAccelerated && spanA.Length >= Vector<float>.Count)
        {
            var simdLength = spanA.Length - (spanA.Length % Vector<float>.Count);

            var vDot = Vector<float>.Zero;
            var vNormA = Vector<float>.Zero;
            var vNormB = Vector<float>.Zero;

            for (int i = 0; i < simdLength; i += Vector<float>.Count)
            {
                var va = new Vector<float>(spanA.Slice(i, Vector<float>.Count));
                var vb = new Vector<float>(spanB.Slice(i, Vector<float>.Count));
                vDot += va * vb;
                vNormA += va * va;
                vNormB += vb * vb;
            }

            dotProduct = Vector.Sum(vDot);
            normA = Vector.Sum(vNormA);
            normB = Vector.Sum(vNormB);

            // Handle remaining elements
            for (int i = simdLength; i < spanA.Length; i++)
            {
                dotProduct += spanA[i] * spanB[i];
                normA += spanA[i] * spanA[i];
                normB += spanB[i] * spanB[i];
            }
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < spanA.Length; i++)
            {
                dotProduct += spanA[i] * spanB[i];
                normA += spanA[i] * spanA[i];
                normB += spanB[i] * spanB[i];
            }
        }

        if (normA <= 0 || normB <= 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Batch cosine similarity: compute similarity of a query vector against multiple candidate vectors.
    /// Returns an array of (index, similarity) pairs sorted by similarity descending.
    /// </summary>
    public static (int Index, double Similarity)[] BatchCosineSimilarity(
        ReadOnlyMemory<float> queryVector,
        IReadOnlyList<ReadOnlyMemory<float>> candidates,
        int topK = 10,
        double minSimilarity = 0.0)
    {
        var results = new (int Index, double Similarity)[candidates.Count];
        
        // Parallel computation for large candidate sets
        if (candidates.Count > 100)
        {
            Parallel.For(0, candidates.Count, i =>
            {
                results[i] = (i, CosineSimilarity(queryVector, candidates[i]));
            });
        }
        else
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                results[i] = (i, CosineSimilarity(queryVector, candidates[i]));
            }
        }

        return results
            .Where(r => r.Similarity >= minSimilarity)
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToArray();
    }
}
