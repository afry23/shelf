using System.Text;

namespace Shelf.TestData.Formats;

internal static class Xml
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static byte[] Bytes(string text) => Utf8NoBom.GetBytes(text);

    /// <summary>Escapes text for element content and attribute values; drops characters that are illegal in XML 1.0.</summary>
    public static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\t' or '\n' or '\r': builder.Append(c); break;
                case < ' ': break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }
}
