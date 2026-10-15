using Shelf.TestData.Content;
using Shelf.TestData.Formats;
using Shelf.TestData.Random;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class PrimitivesTests
{
    // Golden values computed once with the reference C code of https://prng.di.unimi.it/ and frozen.
    [Theory]
    [InlineData(0UL, new[] { 16294208416658607535UL, 7960286522194355700UL, 487617019471545679UL, 17909611376780542444UL, 1961750202426094747UL })]
    [InlineData(42UL, new[] { 13679457532755275413UL, 2949826092126892291UL, 5139283748462763858UL, 6349198060258255764UL, 701532786141963250UL })]
    public void SplitMix64MatchesTheReferenceImplementation(ulong seed, ulong[] expected)
    {
        var generator = new SplitMix64(seed);
        Assert.Equal(expected, expected.Select(_ => generator.Next()).ToArray());
    }

    [Theory]
    [InlineData(0UL, new[] { 11091344671253066420UL, 13793997310169335082UL, 1900383378846508768UL, 7684712102626143532UL, 13521403990117723737UL })]
    [InlineData(42UL, new[] { 1546998764402558742UL, 6990951692964543102UL, 12544586762248559009UL, 17057574109182124193UL, 18295552978065317476UL })]
    public void Xoshiro256StarStarMatchesTheReferenceImplementation(ulong seed, ulong[] expected)
    {
        var generator = new Xoshiro256StarStar(seed);
        Assert.Equal(expected, expected.Select(_ => generator.Next()).ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void NextIntStaysInRange(int bound)
    {
        var generator = new Xoshiro256StarStar(7);
        for (var i = 0; i < 2000; i++)
        {
            var value = generator.NextInt(bound);
            Assert.InRange(value, 0, bound - 1);
        }
    }

    [Fact]
    public void NextIntRejectsAZeroBound() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Xoshiro256StarStar(1).NextInt(0));

    [Fact]
    public void NextInt64StaysInRange()
    {
        var generator = new Xoshiro256StarStar(3);
        for (var i = 0; i < 2000; i++)
        {
            Assert.InRange(generator.NextInt64(10_000_000_000L), 0L, 9_999_999_999L);
        }
    }

    [Fact]
    public void Crc32OfTheCheckStringIsTheStandardValue() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    [Fact]
    public void Crc32OfTheIendChunkTypeIsTheStandardValue() =>
        Assert.Equal(0xAE426082u, Crc32.Compute("IEND"u8));

    [Fact]
    public void Adler32OfWikipediaIsTheDocumentedValue() =>
        Assert.Equal(0x11E60398u, Adler32.Compute("Wikipedia"u8));

    [Theory]
    [InlineData("978030640615", 7)]
    [InlineData("978144937332", 0)]
    [InlineData("978000000001", 9)]
    [InlineData("978000000002", 6)]
    public void IsbnCheckDigit(string first12, int expected) => Assert.Equal(expected, Isbn.CheckDigit(first12));

    [Fact]
    public void IsbnHyphenation() => Assert.Equal("978-0-00-000001-9", Isbn.Hyphenate("9780000000019"));

    [Fact]
    public void IsbnValidation()
    {
        Assert.True(Isbn.IsValid("9780000000026"));
        Assert.False(Isbn.IsValid("9780000000027"));
        Assert.False(Isbn.IsValid("97800000000"));
    }
}
