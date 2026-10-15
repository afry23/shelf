namespace Shelf.TestData.Formats;

/// <summary>Adler-32 as used by zlib streams.</summary>
internal static class Adler32
{
    private const uint Modulus = 65521;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % Modulus;
            b = (b + a) % Modulus;
        }

        return (b << 16) | a;
    }
}
