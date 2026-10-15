namespace Shelf.TestData.Planning;

/// <summary>Pure integer arithmetic that turns (works, files) into the library shape. No randomness.</summary>
public static class LibraryPlanner
{
    public static LibraryPlan Plan(int works, int files)
    {
        if (works < 1)
        {
            throw new UsageException("--works must be at least 1");
        }

        var max = MaxFiles(works);
        if (files < works || files > max)
        {
            throw new UsageException($"--files must be between {works} and {max} for {works} works");
        }

        var extra = files - works;
        var triple = extra / 10;
        var twoFile = extra - (2 * triple);
        var single = works - twoFile - triple;
        var singlePdf = 2 * single / 5;
        var singleEpub = single - singlePdf;
        var exact = (triple + 1) / 2;
        return new LibraryPlan(
            Works: works,
            Files: files,
            SingleFileWorks: single,
            TwoFileWorks: twoFile,
            ThreeFileWorks: triple,
            SingleFilePdfs: singlePdf,
            SingleFileEpubs: singleEpub,
            EpubFiles: singleEpub + twoFile + (2 * triple),
            PdfFiles: singlePdf + twoFile + triple,
            ExactDuplicates: exact,
            SameFormatDuplicates: triple - exact,
            EditionPairs: works / 50,
            HardNegativePairs: works / 100,
            SeriesWorks: works / 10);
    }

    /// <summary>The largest file count for which the number of single-file works is not negative.</summary>
    public static int MaxFiles(int works)
    {
        if (works < 1)
        {
            throw new UsageException("--works must be at least 1");
        }

        // single(extra) = works - extra + floor(extra / 10) never increases with extra.
        var extra = 0;
        while (works - (extra + 1) + ((extra + 1) / 10) >= 0)
        {
            extra++;
        }

        return works + extra;
    }
}
