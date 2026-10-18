using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class ModelInstallerTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("shelf-installer-");

    private static readonly ModelDefinition Model = new(
        "tiny",
        [Describe(ModelFolder.OnnxFileName, "weights"), Describe(ModelFolder.TokenizerFileName, "vocabulary")]);

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Installs_from_the_onnx_subfolder_and_leaves_no_staging_folder()
    {
        var source = Source("onnx");

        var result = ModelInstaller.InstallFromFolder(Model, _root.FullName, source);

        Assert.Equal(InstallStatus.Installed, result.Status);
        Assert.True(ModelInstaller.IsInstalled(Model, result.Folder));
        Assert.False(Directory.Exists(result.Folder.Directory + ".part"));
    }

    [Fact]
    public void A_second_install_replaces_a_broken_folder()
    {
        var folder = ModelInstaller.FolderFor(_root.FullName, "tiny");
        Directory.CreateDirectory(folder.Directory);
        File.WriteAllText(folder.OnnxPath, "broken");

        ModelInstaller.InstallFromFolder(Model, _root.FullName, Source(null));

        Assert.Equal("weights", File.ReadAllText(folder.OnnxPath));
        Assert.True(ModelInstaller.IsInstalled(Model, folder));
    }

    [Fact]
    public void A_wrong_hash_with_the_right_size_is_rejected()
    {
        var source = Source(null);
        File.WriteAllText(Path.Combine(source, ModelFolder.OnnxFileName), "weighTs");

        var ex = Assert.Throws<ModelInstallException>(() => ModelInstaller.InstallFromFolder(Model, _root.FullName, source));

        Assert.Equal("checksum mismatch for model.onnx", ex.Message);
        Assert.False(Directory.Exists(ModelInstaller.FolderFor(_root.FullName, "tiny").Directory));
    }

    [Fact]
    public void A_download_that_fails_midway_leaves_nothing()
    {
        var ex = Assert.Throws<ModelInstallException>(() => ModelInstaller.Download(Model, _root.FullName, new Failing()));

        Assert.StartsWith("download failed: boom", ex.Message);
        Assert.Empty(Directory.GetDirectories(Path.Combine(_root.FullName, ".shelf", "models")));
    }

    [Fact]
    public void A_complete_model_is_not_downloaded_again()
    {
        ModelInstaller.InstallFromFolder(Model, _root.FullName, Source(null));

        var result = ModelInstaller.Download(Model, _root.FullName, new Failing());

        Assert.Equal(InstallStatus.AlreadyInstalled, result.Status);
    }

    [Fact]
    public void The_default_catalog_uses_the_file_names_of_the_model_folder()
    {
        var model = Assert.IsType<ModelDefinition>(ModelCatalog.Default.Find("multilingual-e5-small"));

        Assert.Equal([ModelFolder.OnnxFileName, ModelFolder.TokenizerFileName], model.Files.Select(f => f.Name));
        Assert.All(model.Files, f => Assert.Matches("^[0-9a-f]{64}$", f.Sha256));
        Assert.All(model.Files, f => Assert.StartsWith("https://huggingface.co/intfloat/multilingual-e5-small/resolve/614241f6", f.Url));
    }

    private string Source(string? subfolder)
    {
        var dir = Path.Combine(_root.FullName, "source");
        var target = subfolder is null ? dir : Path.Combine(dir, subfolder);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, ModelFolder.OnnxFileName), "weights");
        File.WriteAllText(Path.Combine(target, ModelFolder.TokenizerFileName), "vocabulary");
        return dir;
    }

    private static ModelFile Describe(string name, string content) =>
        new(name, "https://x/" + name, Encoding.UTF8.GetByteCount(content), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));

    private sealed class Failing : IModelDownloader
    {
        public Stream Open(string url) => throw new IOException("boom");
    }
}
