using System.Globalization;
using Shelf.TestData.Content;
using Shelf.TestData.Formats;
using Shelf.TestData.Model;
using Shelf.TestData.Planning;
using Shelf.TestData.Random;

namespace Shelf.TestData;

/// <summary>Phase 1 of the generator: decides everything about works and files without producing any bytes.</summary>
internal sealed class LibraryBuilder(GeneratorOptions options, LibraryPlan plan)
{
    private static readonly string[] Folders = ["downloads", "downloads/old", "ebooks-backup"];
    private static readonly string[] SeriesQualifiers = ["Essentials", "Compendium", "Handbook", "Library", "Collection", "Chronicles"];

    private static readonly string[] TitleTemplatesEnglish =
    [
        "Patterns of {0} and {1}", "{0} and {1} in Practice", "Introduction to {0} and {1}",
        "Understanding {0} and {1}", "{0}: A Practical Guide to {1}", "Modern {0} with {1}",
    ];

    private static readonly string[] TitleTemplatesGerman =
    [
        "Grundlagen der {0} und der {1}", "{0} und {1} in der Praxis", "Einführung in die {0} und {1}",
        "Praxishandbuch {0} und {1}", "{0}: Ein Überblick über die {1}",
    ];

    private readonly ulong _seed = (ulong)options.Seed;
    private readonly List<GroundTruthRelation> _relations = [];

    public WorkSpec[] Works { get; private set; } = [];

    public void Build()
    {
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
        Works = new WorkSpec[plan.Works];
        Parallel.For(0, Works.Length, parallel, i => Works[i] = DrawWork(i));

        var global = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, -1, 1));
        var shapePermutation = Permutation(plan.Works, global);
        var relationPermutation = Permutation(plan.Works, global);
        AssignShapes(shapePermutation);
        AssignRelations(relationPermutation);
        DeduplicateIsbns();

        Parallel.For(0, Works.Length, parallel, i => BuildFiles(Works[i]));
        NameFiles();
    }

    public GroundTruth ToGroundTruth()
    {
        var files = Works.SelectMany(w => w.Files.Select(f => (Work: w, File: f)))
            .OrderBy(x => x.File.Path, StringComparer.Ordinal).ToList();
        var total = files.Sum(x => x.File.Size);
        return new GroundTruth
        {
            Seed = options.Seed,
            Parameters = new GroundTruthParameters
            {
                Works = plan.Works,
                Files = plan.Files,
                WordsMin = options.WordsMin,
                WordsMax = options.WordsMax,
                Compression = options.Deflate ? "deflate" : "stored",
            },
            Summary = LibraryGenerator.Summarize(plan, total),
            Works = [.. Works.OrderBy(w => w.Key, StringComparer.Ordinal).Select(w => new GroundTruthWork
            {
                Key = w.Key,
                Title = w.Title,
                Authors = [.. w.Authors.Select(a => a.Display)],
                Language = w.Language,
                Year = w.Year,
                Isbn13 = w.Isbn13,
                Series = w.SeriesName is null ? null : new GroundTruthSeries { Name = w.SeriesName, Index = w.SeriesIndex },
                Files = [.. w.Files.Select(f => f.Path).Order(StringComparer.Ordinal)],
            })],
            Relations = [.. _relations.OrderBy(r => r.Kind, StringComparer.Ordinal)
                .ThenBy(r => r.A, StringComparer.Ordinal).ThenBy(r => r.B, StringComparer.Ordinal)],
            Files = [.. files.Select(x => new GroundTruthFile
            {
                Path = x.File.Path,
                Work = x.Work.Key,
                Format = x.File.Format,
                EpubVersion = x.File.EpubVersion switch { 2 => "2.0", 3 => "3.0", _ => null },
                PageCount = x.File.PageCount,
                Size = x.File.Size,
                Sha256 = x.File.Sha256,
                DuplicateOf = x.File.DuplicateOf?.Path,
                DuplicateKind = x.File.DuplicateKind,
                Traits = [.. x.File.Traits.Order(StringComparer.Ordinal)],
                Embedded = new GroundTruthEmbedded
                {
                    Title = x.File.EmbeddedTitle,
                    Authors = [.. x.File.EmbeddedAuthors],
                    Isbn13 = x.File.EmbeddedIsbn,
                    Language = x.File.EmbeddedLanguage,
                },
            })],
        };
    }

    // ---- works ---------------------------------------------------------------------------------------------

    private WorkSpec DrawWork(int index)
    {
        var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, index, 0));
        var work = new WorkSpec(index)
        {
            Language = r.Chance(250) ? Languages.German : Languages.English,
        };
        work.Title = DrawTitle(r, work.Language);
        work.Authors = DrawAuthors(r);
        work.Year = 1990 + r.NextInt(35);
        work.Isbn13 = r.Chance(700) ? DrawIsbn(r) : null;
        work.TextSeed = LibraryGenerator.Derive(_seed, index, 10);
        work.Words = options.WordsMin + r.NextInt(options.WordsMax - options.WordsMin + 1);
        return work;
    }

    private static string DrawTitle(Xoshiro256StarStar r, string language)
    {
        var topics = Vocabulary.Topics(language);
        var templates = language == Languages.German ? TitleTemplatesGerman : TitleTemplatesEnglish;
        var first = r.NextInt(topics.Count);
        var second = r.NextInt(topics.Count - 1);
        if (second >= first)
        {
            second++;
        }

        return string.Format(CultureInfo.InvariantCulture, templates[r.NextInt(templates.Length)], topics[first], topics[second]);
    }

    private static List<AuthorName> DrawAuthors(Xoshiro256StarStar r)
    {
        var draw = r.NextInt(100);
        var count = draw < 75 ? 1 : draw < 95 ? 2 : 3;
        var authors = new List<AuthorName>(count);
        while (authors.Count < count)
        {
            var candidate = new AuthorName(Vocabulary.GivenNames[r.NextInt(Vocabulary.GivenNames.Count)], Vocabulary.Surnames[r.NextInt(Vocabulary.Surnames.Count)]);
            if (!authors.Contains(candidate))
            {
                authors.Add(candidate);
            }
        }

        return authors;
    }

    private static string DrawIsbn(Xoshiro256StarStar r)
    {
        var digits = new char[12];
        digits[0] = '9';
        digits[1] = '7';
        digits[2] = '8';
        for (var i = 3; i < 12; i++)
        {
            digits[i] = (char)('0' + r.NextInt(10));
        }

        return Isbn.Complete(new string(digits));
    }

    private static int[] Permutation(int count, Xoshiro256StarStar r)
    {
        var items = new int[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = i;
        }

        r.Shuffle(items);
        return items;
    }

    private static string Normalize(string title) => NameVariants.Fold(title).ToLowerInvariant();

    private void AssignShapes(int[] permutation)
    {
        var singlePdfsLeft = plan.SingleFilePdfs;
        var exactLeft = plan.ExactDuplicates;
        for (var position = 0; position < permutation.Length; position++)
        {
            var work = Works[permutation[position]];
            if (position < plan.ThreeFileWorks)
            {
                work.ShapeFiles = 3;
                work.ShapeExact = exactLeft-- > 0;
            }
            else if (position < plan.ThreeFileWorks + plan.TwoFileWorks)
            {
                work.ShapeFiles = 2;
            }
            else
            {
                work.ShapeFiles = 1;
                work.ShapePdfOnly = singlePdfsLeft-- > 0;
            }
        }
    }

    private void AssignRelations(int[] permutation)
    {
        var related = new bool[Works.Length];
        var cursor = 0;
        var editionPairs = new List<(WorkSpec A, WorkSpec B)>();
        var hardPairs = new List<(WorkSpec A, WorkSpec B)>();
        for (var i = 0; i < plan.EditionPairs; i++)
        {
            editionPairs.Add((Works[permutation[cursor++]], Works[permutation[cursor++]]));
        }

        for (var i = 0; i < plan.HardNegativePairs; i++)
        {
            hardPairs.Add((Works[permutation[cursor++]], Works[permutation[cursor++]]));
        }

        // Titles of the works that own their title must be unique: accidental same-title pairs
        // would add false positives to the ground truth.
        var secondary = new HashSet<int>();
        foreach (var (_, b) in editionPairs.Concat(hardPairs))
        {
            secondary.Add(b.Index);
            related[b.Index] = true;
        }

        var seriesMembers = new List<WorkSpec>();
        for (var i = 0; i < plan.SeriesWorks; i++)
        {
            var member = Works[permutation[cursor++]];
            seriesMembers.Add(member);
            related[member.Index] = true;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in Works)
        {
            if (secondary.Contains(work.Index) || related[work.Index])
            {
                continue;
            }

            EnsureUniqueTitle(work, used);
        }

        foreach (var (a, b) in editionPairs)
        {
            var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, b.Index, 4));
            b.Title = a.Title;
            b.Authors = a.Authors;
            b.Language = a.Language;
            b.Year = a.Year + 2 + r.NextInt(5);
            a.Isbn13 ??= DrawIsbn(r);
            b.Isbn13 = DrawIsbn(r);
            while (b.Isbn13 == a.Isbn13)
            {
                b.Isbn13 = DrawIsbn(r);
            }

            b.Words = a.Words;
            b.TextSeed = a.TextSeed;
            b.EditionOf = a.Index;
            b.EditionSeed = LibraryGenerator.Derive(_seed, b.Index, 5);
            _relations.Add(new GroundTruthRelation { Kind = RelationKinds.Edition, A = a.Key, B = b.Key });
        }

        foreach (var (a, b) in hardPairs)
        {
            var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, b.Index, 6));
            b.Title = a.Title;
            b.Language = a.Language;
            do
            {
                b.Authors = DrawAuthors(r);
            }
            while (b.Authors.Any(x => a.Authors.Any(y => string.Equals(x.Family, y.Family, StringComparison.Ordinal))));
            _relations.Add(new GroundTruthRelation { Kind = RelationKinds.SameTitleDifferentAuthor, A = a.Key, B = b.Key });
        }

        AssignSeries(seriesMembers);
    }

    private void EnsureUniqueTitle(WorkSpec work, HashSet<string> used)
    {
        for (var attempt = 1; !used.Add(Normalize(work.Title)); attempt++)
        {
            var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, work.Index, 100 + attempt));
            work.Title = DrawTitle(r, work.Language);
            if (attempt > 500)
            {
                // Title space exhausted (very large libraries): keep titles unique by a counter.
                work.Title += " " + attempt.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private void AssignSeries(List<WorkSpec> members)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var groupRng = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, -1, 2));
        var cursor = 0;
        while (cursor < members.Count)
        {
            var left = members.Count - cursor;
            var size = Math.Min(left, 2 + groupRng.NextInt(5));
            if (left - size == 1)
            {
                size = size < 6 ? size + 1 : size - 1;
            }

            var first = members[cursor];
            var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, first.Index, 7));
            var topics = Vocabulary.Topics(first.Language);
            string name;
            var attempt = 0;
            do
            {
                name = topics[r.NextInt(topics.Count)] + " " + SeriesQualifiers[r.NextInt(SeriesQualifiers.Length)];
                if (++attempt > 200)
                {
                    name += " " + attempt.ToString(CultureInfo.InvariantCulture);
                }
            }
            while (!names.Add(name));

            var volume = first.Language == Languages.German ? "Band" : "Volume";
            for (var k = 1; k <= size; k++)
            {
                var member = members[cursor + k - 1];
                member.Language = first.Language;
                member.Authors = first.Authors;
                member.SeriesName = name;
                member.SeriesIndex = k;
                member.Year = first.Year + k - 1;
                member.Title = $"{name}, {volume} {k.ToString(CultureInfo.InvariantCulture)}";
            }

            cursor += size;
        }
    }

    private void DeduplicateIsbns()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in Works)
        {
            var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, work.Index, 8));
            while (work.Isbn13 is not null && !seen.Add(work.Isbn13))
            {
                work.Isbn13 = DrawIsbn(r);
            }
        }
    }

    // ---- files ---------------------------------------------------------------------------------------------

    private void BuildFiles(WorkSpec work)
    {
        var r = new Xoshiro256StarStar(LibraryGenerator.Derive(_seed, work.Index, 9));
        switch (work.ShapeFiles)
        {
            case 1:
                work.Files.Add(work.ShapePdfOnly ? NewPdf(work, r) : NewEpub(work, r, null, null, null));
                break;
            case 2:
                work.Files.Add(NewEpub(work, r, null, null, null));
                work.Files.Add(NewPdf(work, r));
                break;
            default:
                var epub = NewEpub(work, r, null, null, null);
                work.Files.Add(epub);
                work.Files.Add(NewPdf(work, r));
                if (work.ShapeExact)
                {
                    work.Files.Add(CopyOf(epub, r));
                }
                else
                {
                    work.Files.Add(NewEpub(work, r, epub.EpubVersion, epub, DuplicateKinds.SameFormat));
                }

                break;
        }
    }

    private static FileSpec CopyOf(FileSpec original, Xoshiro256StarStar r)
    {
        var pattern = (FileNamePattern)((((int)original.NamePattern) + 1 + r.NextInt(4)) % 5);
        var traits = original.Traits.Where(t => t != Traits.FileNameAuthorTitle).ToList();
        if (pattern == FileNamePattern.AuthorTitleYear)
        {
            traits.Add(Traits.FileNameAuthorTitle);
        }

        return new FileSpec
        {
            Format = original.Format,
            EpubVersion = original.EpubVersion,
            Cover = original.Cover,
            EmbeddedTitle = original.EmbeddedTitle,
            EmbeddedAuthors = original.EmbeddedAuthors,
            EpubAuthors = original.EpubAuthors,
            EmbeddedLanguage = original.EmbeddedLanguage,
            EmbeddedIsbn = original.EmbeddedIsbn,
            SeriesEmbedded = original.SeriesEmbedded,
            Traits = traits,
            NamePattern = pattern,
            Folder = Folders[r.NextInt(Folders.Length)],
            DuplicateOf = original,
            DuplicateKind = DuplicateKinds.Exact,
        };
    }

    private static FileSpec NewEpub(WorkSpec work, Xoshiro256StarStar r, int? avoidVersion, FileSpec? original, string? kind)
    {
        var drawnVersion = r.Chance(300) ? 2 : 3;
        var version = avoidVersion is { } other ? (other == 2 ? 3 : 2) : drawnVersion;
        var cover = r.Chance(500);
        var completeness = r.NextInt(100);
        var authorVariant = DrawAuthorVariant(r);
        var titleVariant = DrawTitleVariant(r);
        var suffix = r.NextInt(TitleVariants.SuffixCount(work.Language));
        var isbn = work.Isbn13 is not null && r.Chance(850);
        var series = work.SeriesName is not null && r.Chance(800);
        var pattern = (FileNamePattern)r.NextInt(5);
        var folder = Folders[r.NextInt(Folders.Length)];

        var title = TitleVariants.Apply(work.Title, work.Language, titleVariant, suffix);
        var noAuthor = completeness >= 95;
        var noLanguage = completeness is >= 85 and < 95;
        var rendered = RenderAuthors(work, authorVariant, out var effective);
        var traits = new List<string>
        {
            version == 2 ? Traits.Epub2 : Traits.Epub3,
            work.Language == Languages.German ? Traits.LanguageDe : Traits.LanguageEn,
        };
        AddIf(traits, cover, Traits.Cover);
        AddIf(traits, isbn, Traits.Isbn);
        AddIf(traits, series, Traits.Series);
        if (!noAuthor)
        {
            AddAuthorTrait(traits, effective);
        }

        AddTitleTraits(traits, titleVariant, title);
        AddIf(traits, pattern == FileNamePattern.AuthorTitleYear, Traits.FileNameAuthorTitle);
        return new FileSpec
        {
            Format = "epub",
            EpubVersion = version,
            Cover = cover,
            EmbeddedTitle = title,
            EmbeddedAuthors = noAuthor ? [] : rendered,
            EpubAuthors = noAuthor ? [] : [.. work.Authors.Select((a, i) => new EpubAuthor(rendered[i], NameVariants.FileAs(a)))],
            EmbeddedLanguage = noLanguage ? null : work.Language,
            EmbeddedIsbn = isbn ? work.Isbn13 : null,
            SeriesEmbedded = series,
            Traits = traits,
            NamePattern = pattern,
            Folder = folder,
            DuplicateOf = original,
            DuplicateKind = original is null ? null : kind,
        };
    }

    private static FileSpec NewPdf(WorkSpec work, Xoshiro256StarStar r)
    {
        var modeDraw = r.NextInt(100);
        var mode = modeDraw < 50 ? PdfMetadataMode.Info : modeDraw < 80 ? PdfMetadataMode.InfoAndXmp : PdfMetadataMode.None;
        var authorVariant = DrawAuthorVariant(r);
        var titleVariant = DrawTitleVariant(r);
        var suffix = r.NextInt(TitleVariants.SuffixCount(work.Language));
        var isbnInfo = work.Isbn13 is not null && r.Chance(500);
        var isbnText = work.Isbn13 is not null && r.Chance(500);
        var drawnPattern = (FileNamePattern)r.NextInt(5);
        var folder = Folders[r.NextInt(Folders.Length)];

        var hasMetadata = mode != PdfMetadataMode.None;
        var pattern = hasMetadata ? drawnPattern : FileNamePattern.AuthorTitleYear;
        var title = TitleVariants.Apply(work.Title, work.Language, titleVariant, suffix);
        var rendered = RenderAuthors(work, authorVariant, out var effective);
        isbnInfo &= hasMetadata;
        var traits = new List<string> { work.Language == Languages.German ? Traits.LanguageDe : Traits.LanguageEn };
        AddIf(traits, isbnInfo || isbnText, Traits.Isbn);
        AddIf(traits, isbnText, Traits.PdfIsbnInText);
        AddIf(traits, mode == PdfMetadataMode.None, Traits.PdfNoMetadata);
        AddIf(traits, mode != PdfMetadataMode.None, Traits.PdfInfo);
        AddIf(traits, mode == PdfMetadataMode.InfoAndXmp, Traits.PdfXmp);
        if (hasMetadata)
        {
            AddAuthorTrait(traits, effective);
            AddTitleTraits(traits, titleVariant, title);
        }

        AddIf(traits, pattern == FileNamePattern.AuthorTitleYear, Traits.FileNameAuthorTitle);
        return new FileSpec
        {
            Format = "pdf",
            EmbeddedTitle = hasMetadata ? title : null,
            EmbeddedAuthors = hasMetadata ? rendered : [],
            EmbeddedIsbn = isbnInfo || isbnText ? work.Isbn13 : null,
            IsbnInText = isbnText,
            IsbnInInfo = isbnInfo,
            PdfMode = mode,
            Traits = traits,
            NamePattern = pattern,
            Folder = folder,
        };
    }

    private static AuthorVariant DrawAuthorVariant(Xoshiro256StarStar r)
    {
        var draw = r.NextInt(1000);
        return draw < 600 ? AuthorVariant.Canonical
            : draw < 750 ? AuthorVariant.SurnameFirst
            : draw < 850 ? AuthorVariant.Initials
            : AuthorVariant.NoDiacritics;
    }

    private static TitleVariant DrawTitleVariant(Xoshiro256StarStar r)
    {
        var draw = r.NextInt(1000);
        return draw < 700 ? TitleVariant.Canonical : draw < 850 ? TitleVariant.EditionSuffix : TitleVariant.LeadingArticle;
    }

    private static List<string> RenderAuthors(WorkSpec work, AuthorVariant variant, out AuthorVariant effective)
    {
        var chosen = variant;
        if (variant == AuthorVariant.NoDiacritics && !work.Authors.Any(a => NameVariants.HasDiacritics(a.Display)))
        {
            chosen = AuthorVariant.Canonical;
        }

        effective = chosen;
        return [.. work.Authors.Select(a => NameVariants.Render(a, chosen))];
    }

    private static void AddAuthorTrait(List<string> traits, AuthorVariant variant)
    {
        switch (variant)
        {
            case AuthorVariant.SurnameFirst: traits.Add(Traits.AuthorSurnameFirst); break;
            case AuthorVariant.Initials: traits.Add(Traits.AuthorInitials); break;
            case AuthorVariant.NoDiacritics: traits.Add(Traits.AuthorNoDiacritics); break;
        }
    }

    private static void AddTitleTraits(List<string> traits, TitleVariant variant, string embeddedTitle)
    {
        AddIf(traits, variant == TitleVariant.EditionSuffix, Traits.TitleEditionSuffix);
        AddIf(traits, variant == TitleVariant.LeadingArticle, Traits.TitleLeadingArticle);
        AddIf(traits, TitleVariants.HasNonAsciiLetter(embeddedTitle), Traits.TitleNonAscii);
    }

    private static void AddIf(List<string> traits, bool condition, string trait)
    {
        if (condition)
        {
            traits.Add(trait);
        }
    }

    private void NameFiles()
    {
        var usedByFolder = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var work in Works)
        {
            foreach (var file in work.Files)
            {
                if (!usedByFolder.TryGetValue(file.Folder, out var used))
                {
                    used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    usedByFolder[file.Folder] = used;
                }

                var stem = FileNames.Stem(file.NamePattern, work.Authors[0].Display, work.Title, work.Year, work.Isbn13);
                var name = FileNames.MakeUnique(stem, "." + file.Format, used);
                file.Path = "input/" + file.Folder + "/" + name;
            }
        }
    }
}
