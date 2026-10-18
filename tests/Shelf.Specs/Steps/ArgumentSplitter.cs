using System.Text;

namespace Shelf.Specs.Steps;

/// <summary>
/// Splits the argument text of "I run shelf with arguments" into an argument vector.
/// Arguments are separated by whitespace; double quotes group text with spaces into one argument
/// (an empty pair of quotes is an empty argument). A backslash directly before a double quote makes that quote
/// part of the text; any other backslash is an ordinary character (paths). Single quotes at the start of an argument
/// group text up to the next single quote, so that a step can pass an argument with spaces (or an empty one)
/// without the double quotes that end the step's own string; a single quote inside a word is an ordinary character.
/// </summary>
internal static class ArgumentSplitter
{
    public static string[] Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var inSingle = false;
        var hasToken = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inSingle)
            {
                if (c == '\'')
                {
                    inSingle = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '\'' && !hasToken && !inQuotes)
            {
                inSingle = true;
                hasToken = true;
            }
            else if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
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

        if (inQuotes || inSingle)
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
