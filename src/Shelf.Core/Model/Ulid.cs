using System.Security.Cryptography;

namespace Shelf.Core.Model;

/// <summary>
/// A ULID: 48 bit timestamp (milliseconds since the Unix epoch) followed by 80 random bits, written as 26
/// characters of Crockford base32. Sorts by creation time. Identifies a work for its whole life (F-LIB-5).
/// </summary>
public readonly struct Ulid : IEquatable<Ulid>, IComparable<Ulid>
{
    public const int TextLength = 26;
    public const long MaxTimestamp = (1L << 48) - 1;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int RandomLength = 10;

    private readonly UInt128 _value;

    private Ulid(UInt128 value) => _value = value;

    /// <summary>Milliseconds since the Unix epoch.</summary>
    public long TimestampMilliseconds => (long)(_value >> 80);

    /// <summary>A new ULID for the current time (or the time of <paramref name="time"/>) and random bits.</summary>
    public static Ulid New(TimeProvider? time = null)
    {
        Span<byte> random = stackalloc byte[RandomLength];
        RandomNumberGenerator.Fill(random);
        return Create((time ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds(), random);
    }

    /// <summary>The ULID with the given timestamp and 10 random bytes (most significant first).</summary>
    public static Ulid Create(long timestampMilliseconds, ReadOnlySpan<byte> random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timestampMilliseconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timestampMilliseconds, MaxTimestamp);
        if (random.Length != RandomLength)
        {
            throw new ArgumentException($"Exactly {RandomLength} random bytes are required.", nameof(random));
        }

        UInt128 randomBits = 0;
        foreach (var b in random)
        {
            randomBits = (randomBits << 8) | b;
        }

        return new Ulid(((UInt128)timestampMilliseconds << 80) | randomBits);
    }

    public static bool TryParse(string? text, out Ulid ulid)
    {
        ulid = default;
        if (text is null || text.Length != TextLength)
        {
            return false;
        }

        UInt128 value = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var digit = Alphabet.IndexOf(char.ToUpperInvariant(text[i]));
            // The first character carries only 3 bits: larger values overflow 128 bits.
            if (digit < 0 || (i == 0 && digit > 7))
            {
                return false;
            }

            value = (value << 5) | (uint)digit;
        }

        ulid = new Ulid(value);
        return true;
    }

    /// <exception cref="FormatException"/>
    public static Ulid Parse(string text) =>
        TryParse(text, out var ulid) ? ulid : throw new FormatException($"'{text}' is not a valid ULID.");

    public override string ToString()
    {
        Span<char> chars = stackalloc char[TextLength];
        var value = _value;
        for (var i = TextLength - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new string(chars);
    }

    public bool Equals(Ulid other) => _value == other._value;

    public override bool Equals(object? obj) => obj is Ulid other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public int CompareTo(Ulid other) => _value.CompareTo(other._value);

    public static bool operator ==(Ulid left, Ulid right) => left.Equals(right);

    public static bool operator !=(Ulid left, Ulid right) => !left.Equals(right);
}
