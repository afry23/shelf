namespace Shelf.Semantic;

/// <summary>One file of a model: where it comes from and what it must look like (size and SHA-256, lower-case hex).</summary>
public sealed record ModelFile(string Name, string Url, long Size, string Sha256);

/// <summary>A model that shelf can install. File names are the names in <see cref="ModelFolder"/>.</summary>
public sealed record ModelDefinition(string Name, IReadOnlyList<ModelFile> Files);

/// <summary>The models shelf knows. Hashes pin the files; a file that differs is never installed.</summary>
public sealed class ModelCatalog(IReadOnlyList<ModelDefinition> models)
{
    private const string E5Revision = "614241f622f53c4eeff9890bdc4f31cfecc418b3";
    private const string E5Base = "https://huggingface.co/intfloat/multilingual-e5-small/resolve/" + E5Revision + "/onnx/";

    public static ModelCatalog Default { get; } = new(
    [
        new ModelDefinition(
            "multilingual-e5-small",
            [
                new ModelFile(ModelFolder.OnnxFileName, E5Base + "model.onnx", 470_268_510, "ca456c06b3a9505ddfd9131408916dd79290368331e7d76bb621f1cba6bc8665"),
                new ModelFile(ModelFolder.TokenizerFileName, E5Base + "sentencepiece.bpe.model", 5_069_051, "cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865"),
            ]),
    ]);

    public IReadOnlyList<ModelDefinition> Models { get; } = models;

    public ModelDefinition? Find(string name) => Models.FirstOrDefault(m => m.Name == name);
}
