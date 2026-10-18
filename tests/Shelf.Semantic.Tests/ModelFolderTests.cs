using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class ModelFolderTests
{
    [Fact]
    public void An_empty_folder_is_not_installed()
    {
        var dir = Directory.CreateTempSubdirectory("shelf-model-");
        try
        {
            var folder = new ModelFolder(dir.FullName);

            Assert.False(folder.IsInstalled);
            var ex = Assert.Throws<ModelNotInstalledException>(folder.EnsureInstalled);
            Assert.StartsWith("model not installed: ", ex.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_folder_with_both_files_is_installed()
    {
        var dir = Directory.CreateTempSubdirectory("shelf-model-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, ModelFolder.OnnxFileName), "x");
            File.WriteAllText(Path.Combine(dir.FullName, ModelFolder.TokenizerFileName), "x");

            Assert.True(new ModelFolder(dir.FullName).IsInstalled);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
