namespace Shelf.Semantic;

/// <summary>Turns text into unit-length vectors (F-SEM-1). Queries and passages are embedded differently.</summary>
public interface IEmbedder
{
    int Dimensions { get; }

    float[] EmbedQuery(string text);

    float[] EmbedPassage(string text);
}
