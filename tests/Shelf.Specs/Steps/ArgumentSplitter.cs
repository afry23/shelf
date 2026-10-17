using System.Text;

namespace Shelf.Specs.Steps;

/// <summary>
/// Splits the argument text of "I run shelf with arguments" into an argument vector.
/// Arguments are separated by whitespace; double quotes group text with spaces into one argument
/// (an empty pair of quotes is an empty argument). A backslash directly before a double quote makes that quote
/// part of the text; any other backslash is an ordinary character (paths).
/// </summary>
internal static class ArgumentSplitter
{
    public static string[] Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
            {
                current.Append('"');
                hasToken = true;
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (inQuotes)
        {
            throw new ArgumentException($"Unterminated quote in arguments: {text}", nameof(text));
        }

        if (hasToken)
        {
            args.Add(current.ToString());
        }

        return [.. args];
    }
}
