namespace Shelf.Semantic;

internal static class Pooling
{
    /// <summary>
    /// Averages the token vectors (<paramref name="hidden"/>, row-major, <paramref name="tokens"/> rows) over the
    /// rows whose mask is 1 and scales the result to unit length. A zero vector stays zero.
    /// </summary>
    public static float[] MeanNormalize(ReadOnlySpan<float> hidden, ReadOnlySpan<long> mask, int dimensions)
    {
        var sum = new double[dimensions];
        var used = 0;
        for (var t = 0; t < mask.Length; t++)
        {
            if (mask[t] == 0)
            {
                continue;
            }

            used++;
            var row = hidden.Slice(t * dimensions, dimensions);
            for (var d = 0; d < dimensions; d++)
            {
                sum[d] += row[d];
            }
        }

        var norm = 0.0;
        for (var d = 0; d < dimensions; d++)
        {
            sum[d] = used == 0 ? 0 : sum[d] / used;
            norm += sum[d] * sum[d];
        }

        norm = Math.Sqrt(norm);
        var result = new float[dimensions];
        for (var d = 0; d < dimensions; d++)
        {
            result[d] = norm == 0 ? 0f : (float)(sum[d] / norm);
        }

        return result;
    }
}
