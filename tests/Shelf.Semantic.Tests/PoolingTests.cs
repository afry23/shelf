using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class PoolingTests
{
    [Fact]
    public void Averages_the_masked_rows_and_scales_to_unit_length()
    {
        float[] hidden = [3, 0, 0, 4, 100, 100];
        long[] mask = [1, 1, 0];

        var vector = Pooling.MeanNormalize(hidden, mask, 2);

        // mean of (3,0) and (0,4) is (1.5,2); its length is 2.5
        Assert.Equal(0.6f, vector[0], 5);
        Assert.Equal(0.8f, vector[1], 5);
    }

    [Fact]
    public void Gives_a_zero_vector_when_nothing_is_masked_in()
    {
        Assert.All(Pooling.MeanNormalize(new float[] { 1, 2 }, new long[] { 0 }, 2), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Gives_a_zero_vector_for_zero_input()
    {
        Assert.All(Pooling.MeanNormalize(new float[] { 0, 0 }, new long[] { 1 }, 2), v => Assert.Equal(0f, v));
    }
}
