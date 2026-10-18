using Microsoft.ML.Tokenizers;

namespace Shelf.Semantic;

/// <summary>
/// The XLM-RoBERTa tokenizer of multilingual-e5: SentencePiece with the ID layout of the reference
/// implementation (<c>&lt;s&gt;</c>=0, <c>&lt;pad&gt;</c>=1, <c>&lt;/s&gt;</c>=2, <c>&lt;unk&gt;</c>=3, every other piece is
/// the SentencePiece ID plus one). The output is cut to <see cref="MaxTokens"/> including both markers.
/// </summary>
public sealed class E5Tokenizer
{
    public const int MaxTokens = 512;
    public const int BeginId = 0;
    public const int EndId = 2;
    public const int PadId = 1;
    private const int UnknownId = 3;

    private readonly SentencePieceTokenizer _tokenizer;

    private E5Tokenizer(SentencePieceTokenizer tokenizer) => _tokenizer = tokenizer;

    public static E5Tokenizer Load(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        return new E5Tokenizer(SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false));
    }

    public int[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pieces = _tokenizer.EncodeToIds(text);
        var count = Math.Min(pieces.Count, MaxTokens - 2);
        var ids = new int[count + 2];
        ids[0] = BeginId;
        for (var i = 0; i < count; i++)
        {
            ids[i + 1] = pieces[i] == 0 ? UnknownId : pieces[i] + 1;
        }

        ids[^1] = EndId;
        return ids;
    }
}
