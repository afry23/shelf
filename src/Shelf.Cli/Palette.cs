namespace Shelf.Cli;

/// <summary>
/// Styles for text output. With color off every method returns its text unchanged, so the text of the output
/// is the same either way; padding is computed on the plain text before styling.
/// </summary>
internal sealed class Palette(bool enabled)
{
    public static Palette Plain { get; } = new(enabled: false);

    public string Bold(string text) => enabled ? $"\e[1m{text}\e[22m" : text;

    public string Dim(string text) => enabled ? $"\e[2m{text}\e[22m" : text;

    /// <summary>Color is wanted on a terminal without NO_COLOR, and never for JSON.</summary>
    public static Palette For(CliEnvironment environment, bool json) => new(environment.UseColor && !json);
}
