namespace Shelf.Semantic;

/// <summary>The files of an embedding model in one folder (<c>.shelf/models/&lt;name&gt;/</c>).</summary>
public sealed record ModelFolder(string Directory)
{
    public const string OnnxFileName = "model.onnx";
    public const string TokenizerFileName = "sentencepiece.bpe.model";

    public string OnnxPath => Path.Combine(Directory, OnnxFileName);

    public string TokenizerPath => Path.Combine(Directory, TokenizerFileName);

    public bool IsInstalled => File.Exists(OnnxPath) && File.Exists(TokenizerPath);

    /// <summary>Throws <see cref="ModelNotInstalledException"/> when a file is missing.</summary>
    public void EnsureInstalled()
    {
        if (!IsInstalled)
        {
            throw new ModelNotInstalledException(this);
        }
    }
}

public sealed class ModelNotInstalledException(ModelFolder folder)
    : Exception($"model not installed: {folder.Directory} needs {ModelFolder.OnnxFileName} and {ModelFolder.TokenizerFileName}")
{
}
