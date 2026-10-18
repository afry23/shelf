using System.Security.Cryptography;

namespace Shelf.Semantic;

public enum InstallStatus
{
    Installed,
    Downloaded,
    AlreadyInstalled,
}

public sealed record InstallResult(ModelFolder Folder, InstallStatus Status);

public sealed class ModelInstallException(string message) : Exception(message)
{
}

/// <summary>
/// Puts the files of a model into <c>.shelf/models/&lt;name&gt;/</c>, from a folder (offline) or by download. Every file is
/// checked against the catalog (size and SHA-256) while it is copied. The files are written into a staging folder first, so
/// a failed run leaves nothing behind and a model folder is complete or absent.
/// </summary>
public static class ModelInstaller
{
    private const string StagingSuffix = ".part";

    public static ModelFolder FolderFor(string libraryRoot, string modelName) =>
        new(Path.Combine(libraryRoot, ".shelf", "models", modelName));

    /// <summary>True when every file of the model is in the folder with the size the catalog names (the hash is checked on install).</summary>
    public static bool IsInstalled(ModelDefinition model, ModelFolder folder) =>
        model.Files.All(f => new FileInfo(Path.Combine(folder.Directory, f.Name)) is { Exists: true } info && info.Length == f.Size);

    /// <exception cref="ModelInstallException"/>
    public static InstallResult InstallFromFolder(ModelDefinition model, string libraryRoot, string sourceFolder, Action<string>? log = null)
    {
        if (!Directory.Exists(sourceFolder))
        {
            throw new ModelInstallException($"{sourceFolder}: not a folder");
        }

        var sources = model.Files.ToDictionary(f => f, f => Locate(sourceFolder, f));
        return Install(model, libraryRoot, InstallStatus.Installed, log, file => File.OpenRead(sources[file]));
    }

    /// <exception cref="ModelInstallException"/>
    public static InstallResult Download(ModelDefinition model, string libraryRoot, IModelDownloader downloader, Action<string>? log = null)
    {
        var folder = FolderFor(libraryRoot, model.Name);
        if (IsInstalled(model, folder))
        {
            return new InstallResult(folder, InstallStatus.AlreadyInstalled);
        }

        try
        {
            return Install(model, libraryRoot, InstallStatus.Downloaded, log, file =>
            {
                log?.Invoke($"downloading {file.Name} ({file.Size / (1024 * 1024)} MiB)");
                return downloader.Open(file.Url);
            });
        }
        catch (IOException e)
        {
            throw new ModelInstallException($"download failed: {e.Message}. To install without a network, run: shelf model install <folder>");
        }
    }

    private static string Locate(string sourceFolder, ModelFile file)
    {
        foreach (var candidate in new[] { Path.Combine(sourceFolder, file.Name), Path.Combine(sourceFolder, "onnx", file.Name) })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new ModelInstallException($"{file.Name} not found in {sourceFolder}");
    }

    private static InstallResult Install(ModelDefinition model, string libraryRoot, InstallStatus status, Action<string>? log, Func<ModelFile, Stream> open)
    {
        var final = FolderFor(libraryRoot, model.Name);
        var staging = final.Directory + StagingSuffix;
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in model.Files)
            {
                log?.Invoke($"checking {file.Name}");
                using var source = open(file);
                using var target = File.Create(Path.Combine(staging, file.Name));
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1 << 20];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    target.Write(buffer, 0, read);
                }

                if (target.Length != file.Size || !Convert.ToHexStringLower(hash.GetHashAndReset()).Equals(file.Sha256, StringComparison.Ordinal))
                {
                    throw new ModelInstallException($"checksum mismatch for {file.Name}");
                }
            }

            if (Directory.Exists(final.Directory))
            {
                Directory.Delete(final.Directory, recursive: true);
            }

            Directory.Move(staging, final.Directory);
            return new InstallResult(final, status);
        }
        catch
        {
            Directory.Delete(staging, recursive: true);
            throw;
        }
    }
}
