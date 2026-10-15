namespace Shelf.TestData.Planning;

/// <summary>Counts of a synthetic library shape (slice plan section 3.4).</summary>
public sealed record LibraryPlan(
    int Works,
    int Files,
    int SingleFileWorks,
    int TwoFileWorks,
    int ThreeFileWorks,
    int SingleFilePdfs,
    int SingleFileEpubs,
    int EpubFiles,
    int PdfFiles,
    int ExactDuplicates,
    int SameFormatDuplicates,
    int EditionPairs,
    int HardNegativePairs,
    int SeriesWorks);
