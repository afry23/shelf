using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Shelf.Semantic;

/// <summary>
/// multilingual-e5-small through ONNX Runtime on the CPU (F-SEM-1). The model expects the prefixes
/// <c>query: </c> and <c>passage: </c>; the vector is the masked mean of the last hidden state, scaled to unit length.
/// Loading the files is the cost; one instance is meant to be reused.
/// </summary>
public sealed class E5Embedder : IEmbedder, IDisposable
{
    /// <summary>The size of multilingual-e5-small; other models of the family (base, large) report their own.</summary>
    public const int EmbeddingDimensions = 384;

    private readonly E5Tokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly bool _needsTokenTypes;
    private readonly int _dimensions;

    private E5Embedder(E5Tokenizer tokenizer, InferenceSession session)
    {
        _tokenizer = tokenizer;
        _session = session;
        _needsTokenTypes = session.InputMetadata.ContainsKey("token_type_ids");
        _dimensions = session.OutputMetadata.TryGetValue("last_hidden_state", out var output) && output.Dimensions is [_, _, > 0 and var size]
            ? size
            : EmbeddingDimensions;
    }

    public int Dimensions => _dimensions;

    public static E5Embedder Open(ModelFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        folder.EnsureInstalled();
        QuietRuntime();
        var tokenizer = E5Tokenizer.Load(folder.TokenizerPath);
        return new E5Embedder(tokenizer, new InferenceSession(folder.OnnxPath));
    }

    /// <summary>
    /// ONNX Runtime writes its own warnings to standard error (on Linux, a device-discovery warning at start-up). The
    /// environment is created before the first session, with the level set at creation; stderr stays for shelf.
    /// </summary>
    private static void QuietRuntime()
    {
        if (!OrtEnv.IsCreated)
        {
            var options = new EnvironmentCreationOptions { logLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
            OrtEnv.CreateInstanceWithOptions(ref options);
        }
    }

    public static string QueryText(string text) => "query: " + text;

    public static string PassageText(string text) => "passage: " + text;

    public float[] EmbedQuery(string text) => EmbedRaw(QueryText(text));

    public float[] EmbedPassage(string text) => EmbedRaw(PassageText(text));

    /// <summary>Embeds <paramref name="prefixed"/> as it is, without adding a prefix.</summary>
    public float[] EmbedRaw(string prefixed)
    {
        var ids = _tokenizer.Encode(prefixed);
        return Embed(ids);
    }

    public void Dispose() => _session.Dispose();

    private float[] Embed(int[] ids)
    {
        var shape = new[] { 1, ids.Length };
        var inputIds = new DenseTensor<long>(shape);
        var mask = new DenseTensor<long>(shape);
        var types = new DenseTensor<long>(shape);
        var maskValues = new long[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            inputIds[0, i] = ids[i];
            mask[0, i] = 1;
            maskValues[i] = 1;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        };
        if (_needsTokenTypes)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", types));
        }

        using var results = _session.Run(inputs);
        var hidden = results.First().AsTensor<float>().ToArray();
        return Pooling.MeanNormalize(hidden, maskValues, _dimensions);
    }
}
