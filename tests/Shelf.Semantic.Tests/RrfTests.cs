using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class RrfTests
{
    [Fact]
    public void A_key_at_the_top_of_every_list_scores_one()
    {
        var fused = Rrf.Fuse<string>([["a", "b"], ["a", "c"]]);

        Assert.Equal("a", fused[0].Key);
        Assert.Equal(1.0, fused[0].Score, 6);
    }

    [Fact]
    public void Scores_add_up_over_the_lists_with_one_over_k_plus_rank()
    {
        var fused = Rrf.Fuse<string>([["a", "b"], ["b"]]).ToDictionary(f => f.Key, f => f.Score);

        var best = 2.0 / 61;
        Assert.Equal((1.0 / 61) / best, fused["a"], 6);
        Assert.Equal(((1.0 / 62) + (1.0 / 61)) / best, fused["b"], 6);
    }

    [Fact]
    public void A_key_in_two_lists_beats_a_key_that_is_first_in_one()
    {
        var fused = Rrf.Fuse<string>([["x", "y"], ["z", "y"]]);

        Assert.Equal("y", fused[0].Key);
    }

    [Fact]
    public void Ties_keep_the_order_in_which_the_keys_were_first_seen()
    {
        var fused = Rrf.Fuse<string>([["a"], ["b"]]);

        Assert.Equal(["a", "b"], fused.Select(f => f.Key));
    }

    [Fact]
    public void An_empty_list_does_not_change_the_order_of_the_other()
    {
        var fused = Rrf.Fuse<string>([["a", "b", "c"], []]);

        Assert.Equal(["a", "b", "c"], fused.Select(f => f.Key));
        Assert.Equal(1.0, fused[0].Score, 6);
    }

    [Fact]
    public void No_lists_give_no_keys() => Assert.Empty(Rrf.Fuse<string>([]));
}
