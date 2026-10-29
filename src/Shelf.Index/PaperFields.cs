using Shelf.Core.Model;
using Shelf.Extraction;

namespace Shelf.Index;

/// <summary>The paper fields a new work takes from the metadata of its first file (F-PAP-2).</summary>
internal static class PaperFields
{
    /// <summary>The sidecar with DOI and arXiv ID of <paramref name="metadata"/>; a work that has one of them is a paper.</summary>
    public static WorkSidecar From(this WorkSidecar sidecar, BookMetadata metadata)
    {
        if (metadata.Doi is null && metadata.ArxivId is null)
        {
            return sidecar;
        }

        return sidecar with { Kind = WorkKind.Paper, Doi = metadata.Doi, ArxivId = metadata.ArxivId };
    }
}
