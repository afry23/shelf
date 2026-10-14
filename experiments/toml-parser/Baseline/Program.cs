using Proto;

return Harness.Run(new BaselineAdapter(), args, Console.Out, Console.Error);

// Reads the file and does no TOML parsing: the size/time baseline.
internal sealed class BaselineAdapter : IAdapter
{
    public object Parse(string text) => text;

    public Val? Get(object document, string dottedKey) => new("string", ((string)document).Length.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public bool RoundTrip(string text, string sameFile, string editFile) => false;
}
