using Shelf.Core.Configuration;
using Shelf.Core.Model;

namespace Shelf.Core.Grouping;

/// <summary>What is compared when files are grouped into works: title, author surnames, year, ISBN, text fingerprint.</summary>
public sealed record WorkSignature(string? TitleNorm, IReadOnlyList<string> SurnameKeys, int? Year, string? Isbn13)
{
    public static WorkSignature Create(string? title, IEnumerable<string> authors, int? year, string? isbn13) =>
        new(string.IsNullOrWhiteSpace(title) ? null : TitleNormalizer.Normalize(title), AuthorNames.SurnameKeys(authors), year, isbn13);
}

/// <summary>A file that wants a work.</summary>
public sealed record IncomingFile(string FileName, WorkSignature Signature, ulong? Fingerprint);

/// <summary>A work that files can be merged into.</summary>
public sealed class WorkCandidate(Ulid id, WorkSignature signature, IEnumerable<ulong>? fingerprints = null, IEnumerable<string>? notDuplicateFileNames = null)
{
    private readonly List<ulong> _fingerprints = [.. fingerprints ?? []];

    public Ulid Id { get; } = id;

    public WorkSignature Signature { get; } = signature;

    public IReadOnlyList<ulong> Fingerprints => _fingerprints;

    /// <summary>File names that the user marked as "not a duplicate" of this work (from <c>notDuplicates.files</c>).</summary>
    public IReadOnlySet<string> NotDuplicateFileNames { get; } = new HashSet<string>(notDuplicateFileNames ?? [], StringComparer.Ordinal);

    internal void AddFingerprint(ulong fingerprint) => _fingerprints.Add(fingerprint);
}

public enum GroupingAction
{
    /// <summary>The file belongs to an existing work: <see cref="GroupingDecision.WorkId"/>.</summary>
    MergeIntoExisting,

    /// <summary>The file becomes a work of its own; <see cref="GroupingDecision.Matches"/> lists works that it may belong to (review).</summary>
    CreateNewWork,
}

public sealed record GroupingMatch(Ulid WorkId, double Confidence, IReadOnlyList<string> Signals);

public sealed record GroupingDecision(GroupingAction Action, Ulid? WorkId, double Confidence, IReadOnlyList<GroupingMatch> Matches);

/// <summary>
/// The grouping cascade (F-GRP-1, F-GRP-3, PRD 6.3): same ISBN-13, then normalized title with an author surname,
/// then text fingerprint. The first stage with a sure match decides. A match is merged only if it is the only
/// best one; everything between the review and the auto-merge threshold is written to the review queue by the caller.
/// </summary>
public sealed class WorkMatcher
{
    public const double IsbnConfidence = 1.0;
    public const double TitleAuthorConfidence = 0.95;
    public const double TitleAuthorOtherYearConfidence = 0.6;
    public const double FingerprintNearConfidence = 0.9;
    public const double FingerprintFarConfidence = 0.6;

    private readonly GroupingSettings _settings;
    private readonly List<WorkCandidate> _candidates = [];
    private readonly Dictionary<string, List<WorkCandidate>> _byIsbn = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<WorkCandidate>> _byTitle = new(StringComparer.Ordinal);

    public WorkMatcher(GroupingSettings settings, IEnumerable<WorkCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(candidates);
        _settings = settings;
        foreach (var candidate in candidates)
        {
            Add(candidate);
        }
    }

    /// <summary>Makes a work available to the files that follow (a work created in the same import).</summary>
    public void Add(WorkCandidate candidate)
    {
        _candidates.Add(candidate);
        if (candidate.Signature.Isbn13 is { } isbn)
        {
            Index(_byIsbn, isbn, candidate);
        }

        if (candidate.Signature.TitleNorm is { } title)
        {
            Index(_byTitle, title, candidate);
        }
    }

    /// <summary>Remembers the fingerprint of a file that was merged into a work, for the files that follow.</summary>
    public void AddFingerprint(Ulid workId, ulong fingerprint) =>
        _candidates.First(c => c.Id == workId).AddFingerprint(fingerprint);

    public GroupingDecision Decide(IncomingFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var stages = new[] { ByIsbn(file), ByTitleAndAuthor(file), ByFingerprint(file) };

        foreach (var stage in stages)
        {
            var sure = stage.Where(m => m.Confidence >= _settings.AutoMerge).ToList();
            if (sure.Count == 0)
            {
                continue;
            }

            var best = sure.Max(m => m.Confidence);
            var top = sure.Where(m => m.Confidence == best).ToList();
            if (top.Count == 1)
            {
                var chosen = Combine(stages).First(m => m.WorkId == top[0].WorkId);
                return new GroupingDecision(GroupingAction.MergeIntoExisting, chosen.WorkId, chosen.Confidence, [chosen]);
            }

            // Several works fit equally well: a person decides.
            return NewWork(Combine(stages));
        }

        return NewWork(Combine(stages));
    }

    private GroupingDecision NewWork(IReadOnlyList<GroupingMatch> all)
    {
        var review = all.Where(m => m.Confidence >= _settings.Review).ToList();
        return new GroupingDecision(GroupingAction.CreateNewWork, null, review.Count == 0 ? 0 : review.Max(m => m.Confidence), review);
    }

    /// <summary>One match per work: the best confidence of all stages, with the signals of every stage that found it.</summary>
    private static List<GroupingMatch> Combine(IEnumerable<IReadOnlyList<GroupingMatch>> stages) =>
        [.. stages.SelectMany(s => s)
            .GroupBy(m => m.WorkId)
            .Select(g => new GroupingMatch(g.Key, g.Max(m => m.Confidence), [.. g.SelectMany(m => m.Signals).Distinct()]))
            .OrderByDescending(m => m.Confidence)
            .ThenBy(m => m.WorkId)];

    private List<GroupingMatch> ByIsbn(IncomingFile file)
    {
        if (file.Signature.Isbn13 is not { } isbn || !_byIsbn.TryGetValue(isbn, out var found))
        {
            return [];
        }

        return [.. Allowed(found, file).Select(c => new GroupingMatch(c.Id, IsbnConfidence, [$"same ISBN-13 {isbn}"]))];
    }

    private List<GroupingMatch> ByTitleAndAuthor(IncomingFile file)
    {
        var signature = file.Signature;
        if (signature.TitleNorm is not { } title || signature.SurnameKeys.Count == 0 || !_byTitle.TryGetValue(title, out var found))
        {
            return [];
        }

        var matches = new List<GroupingMatch>();
        foreach (var candidate in Allowed(found, file))
        {
            if (!candidate.Signature.SurnameKeys.Intersect(signature.SurnameKeys, StringComparer.Ordinal).Any())
            {
                continue;
            }

            var otherYear = signature.Year is { } a && candidate.Signature.Year is { } b && a != b;
            matches.Add(new GroupingMatch(
                candidate.Id,
                otherYear ? TitleAuthorOtherYearConfidence : TitleAuthorConfidence,
                [otherYear ? "title and author match, year differs" : "title and author match"]));
        }

        return matches;
    }

    private List<GroupingMatch> ByFingerprint(IncomingFile file)
    {
        if (file.Fingerprint is not { } fingerprint)
        {
            return [];
        }

        var matches = new List<GroupingMatch>();
        foreach (var candidate in Allowed(_candidates, file))
        {
            if (candidate.Fingerprints.Count == 0)
            {
                continue;
            }

            var distance = candidate.Fingerprints.Min(f => SimHash.Distance(f, fingerprint));
            if (distance <= _settings.AutoHamming)
            {
                matches.Add(new GroupingMatch(candidate.Id, FingerprintNearConfidence, [$"text fingerprint, distance {distance}"]));
            }
            else if (distance <= _settings.MaxHamming)
            {
                matches.Add(new GroupingMatch(candidate.Id, FingerprintFarConfidence, [$"text fingerprint, distance {distance}"]));
            }
        }

        return matches;
    }

    private static IEnumerable<WorkCandidate> Allowed(IEnumerable<WorkCandidate> candidates, IncomingFile file) =>
        candidates.Where(c => !c.NotDuplicateFileNames.Contains(file.FileName));

    private static void Index(Dictionary<string, List<WorkCandidate>> index, string key, WorkCandidate candidate)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(candidate);
    }
}
