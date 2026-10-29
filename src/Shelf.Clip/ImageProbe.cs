namespace Shelf.Clip;

/// <summary>Size and kind of an image read from its first bytes (F-ART-5); no decoder is needed to tell a pixel from a photo.</summary>
public static class ImageProbe
{
    /// <summary>The file extension (<c>jpg</c>, <c>png</c>, <c>gif</c>, <c>webp</c>) and the size, or null if the bytes are none of these.</summary>
    public static (string Extension, int Width, int Height)? Read(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
        {
            return ("png", Be32(b, 16), Be32(b, 20));
        }

        if (b.Length >= 10 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F')
        {
            return ("gif", b[6] | (b[7] << 8), b[8] | (b[9] << 8));
        }

        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            return Webp(b);
        }

        return b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8 ? Jpeg(b) : null;
    }

    private static int Be32(ReadOnlySpan<byte> b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    private static (string, int, int)? Jpeg(ReadOnlySpan<byte> b)
    {
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = b[i + 1];
            if (marker is 0xFF)
            {
                i++;
                continue;
            }

            // Start-of-frame markers carry the size; 0xC4, 0xC8 and 0xCC are not frames.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return ("jpg", (b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            }

            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }

            i += 2 + ((b[i + 2] << 8) | b[i + 3]);
        }

        return null;
    }

    private static (string, int, int)? Webp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30)
        {
            return null;
        }

        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == ' ')
        {
            return ("webp", (b[26] | (b[27] << 8)) & 0x3FFF, (b[28] | (b[29] << 8)) & 0x3FFF);
        }

        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'L')
        {
            var bits = b[21] | ((uint)b[22] << 8) | ((uint)b[23] << 16) | ((uint)b[24] << 24);
            return ("webp", (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'X')
        {
            return ("webp", (b[24] | (b[25] << 8) | (b[26] << 16)) + 1, (b[27] | (b[28] << 8) | (b[29] << 16)) + 1);
        }

        return null;
    }
}
