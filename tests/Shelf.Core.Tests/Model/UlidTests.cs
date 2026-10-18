using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class UlidTests
{
    [Fact]
    public void Create_rejects_a_timestamp_beyond_48_bit() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ulid.Create(Ulid.MaxTimestamp + 1, new byte[10]));

    [Fact]
    public void Create_rejects_a_negative_timestamp() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ulid.Create(-1, new byte[10]));

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public void Create_requires_exactly_ten_random_bytes(int length) =>
        Assert.Throws<ArgumentException>(() => Ulid.Create(0, new byte[length]));

    [Fact]
    public void New_uses_the_time_of_the_given_provider()
    {
        var time = new FixedTime(DateTimeOffset.FromUnixTimeMilliseconds(1760642640000));

        Assert.Equal(1760642640000, Ulid.New(time).TimestampMilliseconds);
    }

    [Fact]
    public void Parse_throws_FormatException_for_invalid_text() =>
        Assert.Throws<FormatException>(() => Ulid.Parse("nope"));

    [Fact]
    public void TryParse_accepts_null_as_invalid() => Assert.False(Ulid.TryParse(null, out _));

    [Fact]
    public void Compare_orders_by_timestamp_first()
    {
        var earlier = Ulid.Create(1, [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff]);
        var later = Ulid.Create(2, new byte[10]);

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(earlier != later);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
