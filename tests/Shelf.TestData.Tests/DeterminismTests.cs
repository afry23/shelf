using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Shelf.TestData.Tests;

// Changes the process-wide default culture; serialized with itself and cheap enough to not matter.
[Collection("Culture")]
public sealed class DeterminismTests
{
    // Digest of `generate --seed 42 --preset tiny` (see Digest). If .NET, the generator or the vocabulary change the
    // bytes, this fails loudly: update it together with GroundTruth.CurrentGeneratorFormat and say so in the PR.
    private const string TinySeed42Digest = "19016dfdfcbc65df735f61a600ea9d852194c8916e2798f6f99160f5aff6d721";

    [Fact]
    public void TheTinyLibraryHasTheFrozenDigest()
    {
        Assert.Equal(TinySeed42Digest, GenerateTinyDigest(parallelism: -1));
    }

    [Fact]
    public void TheDigestDoesNotDependOnTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.DefaultThreadCurrentCulture = german;
            Assert.Equal(TinySeed42Digest, GenerateTinyDigest(parallelism: -1));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.DefaultThreadCurrentCulture = previousDefault;
        }
    }

    [Fact]
    public void TheDigestDoesNotDependOnParallelism()
    {
        Assert.Equal(TinySeed42Digest, GenerateTinyDigest(parallelism: 1));
    }

    private static string GenerateTinyDigest(int parallelism)
    {
        var directory = Path.Combine(Path.GetTempPath(), "shelf-testdata-tests", Guid.NewGuid().ToString("N"));
        try
        {
            LibraryGenerator.Generate(new GeneratorOptions(20, 30, 42, MaxDegreeOfParallelism: parallelism), directory);
            return Digest(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>SHA-256 over the ordinal-sorted lines "path TAB sha256 LF" of every generated file (incl. ground-truth.json).</summary>
    internal static string Digest(string directory)
    {
        var lines = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .Select(f => (Relative: Path.GetRelativePath(directory, f).Replace('\\', '/'), Full: f))
                     .OrderBy(x => x.Relative, StringComparer.Ordinal))
        {
            lines.Append(file.Relative).Append('\t')
                .Append(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file.Full)))).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())));
    }
}
