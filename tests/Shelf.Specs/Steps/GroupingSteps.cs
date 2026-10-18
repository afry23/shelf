using System.Globalization;
using Reqnroll;
using Shelf.Core.Configuration;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Extraction;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Title normalization, author names, text fingerprints and the grouping cascade.</summary>
[Binding]
public sealed class GroupingSteps
{
    private static readonly string[] Syllables = ["ka", "lo", "mi", "ré", "tü", "ba", "ne", "so", "ça", "vi"];

    private readonly List<string> _titles = [];
    private AuthorName? _author;
    private bool? _authorsMatch;

    private readonly Dictionary<string, List<string>> _texts = [];
    private readonly Dictionary<string, List<string>> _segments = [];
    private readonly Dictionary<string, ulong?> _fingerprints = [];
    private int? _fixtureDistance;

    private readonly List<(string Label, WorkSignature Signature, ulong? Fingerprint)> _works = [];
    private readonly Dictionary<string, HashSet<string>> _notDuplicates = [];
    private readonly StringWriter _configText = new();
    private WorkMatcher? _matcher;
    private IncomingFile? _incoming;
    private GroupingDecision? _decision;

    // ---- titles ----

    [When("shelf normalizes the title {string}")]
    public void WhenShelfNormalizesTheTitle(string title) => _titles.Add(TitleNormalizer.Normalize(title));

    [When("shelf normalizes the titles {string} and {string}")]
    public void WhenShelfNormalizesTheTitles(string first, string second)
    {
        _titles.Add(TitleNormalizer.Normalize(first));
        _titles.Add(TitleNormalizer.Normalize(second));
    }

    [Then("the normalized title is {string}")]
    public void ThenTheNormalizedTitleIs(string expected) => Assert.Equal(expected, Assert.Single(_titles));

    [Then("the normalized titles are different")]
    public void ThenTheNormalizedTitlesAreDifferent()
    {
        Assert.Equal(2, _titles.Count);
        Assert.NotEqual(_titles[0], _titles[1]);
    }

    // ---- author names ----

    [When("shelf reads the author name {string}")]
    public void WhenShelfReadsTheAuthorName(string name) => _author = AuthorNames.Parse(name);

    [Then("the surname key is {string}")]
    public void ThenTheSurnameKeyIs(string key) => Assert.Equal(key, _author!.SurnameKey);

    [Then("there is no surname key")]
    public void ThenThereIsNoSurnameKey() => Assert.Null(_author!.SurnameKey);

    [Then("the sort key is {string}")]
    public void ThenTheSortKeyIs(string key) => Assert.Equal(key, _author!.SortKey);

    [When("shelf compares the authors {string} and {string}")]
    public void WhenShelfComparesTheAuthors(string first, string second) =>
        _authorsMatch = AuthorNames.Match(Split(first), Split(second));

    [Then(@"^the authors (match|do not match)$")]
    public void ThenTheAuthors(string result) => Assert.Equal(result == "match", _authorsMatch);

    // ---- fingerprints ----

    [Given("a text {string} of {int} words from the seed {int}")]
    public void GivenAText(string name, int words, int seed) => _texts[name] = Words(words, seed);

    [Given("the text {string} is the text {string}")]
    public void GivenTheTextIsTheText(string name, string source) => _texts[name] = [.. _texts[source]];

    [Given("the text {string} is the text {string} with {int} percent of the words replaced")]
    public void GivenTheTextWithWordsReplaced(string name, string source, int percent)
    {
        var words = new List<string>(_texts[source]);
        var random = new Random(4711);
        var replacements = Words(words.Count, 9001);
        for (var i = 0; i < words.Count; i++)
        {
            if (random.Next(100) < percent)
            {
                words[i] = replacements[i];
            }
        }

        _texts[name] = words;
    }

    [Given("the text {string} is the text {string} in upper case with extra punctuation and line breaks")]
    public void GivenTheTextInUpperCase(string name, string source)
    {
        var words = _texts[source];
        var separators = new[] { ", ", ".\n", " — ", "; ", "\n\n", " (", ") " };
        _texts[name] = [.. words.Select((w, i) => w.ToUpperInvariant() + separators[i % separators.Length])];
    }

    [Given("the text {string} is the first {int} words of the text {string} followed by {int} words from the seed {int}")]
    public void GivenTheTextIsTheFirstWordsFollowedBy(string name, int first, string source, int more, int seed) =>
        _texts[name] = [.. _texts[source].Take(first), .. Words(more, seed)];

    [Given("the text {string} is the text {string} cut into segments of {int} words")]
    public void GivenTheTextCutIntoSegments(string name, string source, int size)
    {
        _texts[name] = [.. _texts[source]];
        _segments[name] = [.. _texts[source].Chunk(size).Select(chunk => string.Join(' ', chunk))];
    }

    [When("shelf computes the fingerprints")]
    public void WhenShelfComputesTheFingerprints()
    {
        foreach (var (name, words) in _texts)
        {
            _fingerprints[name] = SimHash.Compute(_segments.TryGetValue(name, out var segments) ? segments : [string.Join(' ', words)]);
        }
    }

    [When("shelf computes the fingerprints of the fixtures {string} and {string}")]
    public void WhenShelfComputesTheFingerprintsOfTheFixtures(string first, string second)
    {
        ulong? Fingerprint(string fixture) => SimHash.Compute(
            BookExtractor.Extract(Path.Combine(RepositoryPaths.Root(), "tests", "fixtures", fixture)).Segments.Select(s => s.Text));

        var a = Fingerprint(first);
        var b = Fingerprint(second);
        Assert.NotNull(a);
        Assert.NotNull(b);
        _fixtureDistance = SimHash.Distance(a.Value, b.Value);
    }

    [Then("the distance between {string} and {string} is {int}")]
    public void ThenTheDistanceIs(string first, string second, int distance) => Assert.Equal(distance, Distance(first, second));

    [Then("the distance between {string} and {string} is at most {int}")]
    public void ThenTheDistanceIsAtMost(string first, string second, int distance) =>
        Assert.True(Distance(first, second) <= distance, $"distance {Distance(first, second)}");

    [Then("the distance between {string} and {string} is at least {int}")]
    public void ThenTheDistanceIsAtLeast(string first, string second, int distance) =>
        Assert.True(Distance(first, second) >= distance, $"distance {Distance(first, second)}");

    [Then("the distance between the fixtures is at most {int}")]
    public void ThenTheFixtureDistanceIsAtMost(int distance) =>
        Assert.True(_fixtureDistance <= distance, $"distance {_fixtureDistance}");

    [Then(@"^the text ""(.*)"" has (no fingerprint|a fingerprint)$")]
    public void ThenTheTextHasAFingerprint(string name, string result) =>
        Assert.Equal(result == "a fingerprint", _fingerprints[name] is not null);

    // ---- matching ----

    [Given("the library has these works:")]
    public void GivenTheLibraryHasTheseWorks(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var fingerprint = row["fingerprint"].Length == 0 ? (ulong?)null : ulong.Parse(row["fingerprint"], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var year = row["year"].Length == 0 ? (int?)null : int.Parse(row["year"], CultureInfo.InvariantCulture);
            var isbn = row["isbn13"].Length == 0 ? null : row["isbn13"];
            _works.Add((row["work"], WorkSignature.Create(row["title"], Split(row["authors"]), year, isbn), fingerprint));
        }
    }

    [Given("the work {string} has the file {string} marked as not a duplicate")]
    public void GivenTheWorkHasTheFileMarked(string work, string file)
    {
        if (!_notDuplicates.TryGetValue(work, out var set))
        {
            _notDuplicates[work] = set = [];
        }

        set.Add(file);
    }

    [Given("the configuration contains the line {string}")]
    public void GivenTheConfigurationContainsTheLine(string line) => _configText.WriteLine(line);

    [When("a file arrives with the title {string}, the authors {string}, the year {string}, the ISBN {string} and the fingerprint {string}")]
    public void WhenAFileArrives(string title, string authors, string year, string isbn, string fingerprint) =>
        Arrive("file.epub", title, authors, year, isbn, fingerprint);

    [When("a file named {string} arrives with the title {string}, the authors {string}, the year {string}, the ISBN {string} and the fingerprint {string}")]
    public void WhenAFileNamedArrives(string name, string title, string authors, string year, string isbn, string fingerprint) =>
        Arrive(name, title, authors, year, isbn, fingerprint);

    [When("the file is stored as a new work {string}")]
    public void WhenTheFileIsStoredAsANewWork(string label)
    {
        Assert.Equal(GroupingAction.CreateNewWork, _decision!.Action);
        _works.Add((label, _incoming!.Signature, _incoming.Fingerprint));
        _matcher!.Add(new WorkCandidate(IdOf(label), _incoming.Signature, _incoming.Fingerprint is { } f ? [f] : []));
    }

    [Then("the decision is {string} with the confidence {double}")]
    public void ThenTheDecisionIs(string expected, double confidence)
    {
        Assert.Equal(expected, Describe(_decision!));
        Assert.Equal(confidence, _decision!.Confidence, 9);
    }

    // ---- helpers ----

    private void Arrive(string name, string title, string authors, string year, string isbn, string fingerprint)
    {
        _matcher ??= BuildMatcher();
        ulong? print = null;
        if (fingerprint.Length > 0)
        {
            // "W1~3": the fingerprint of W1 with its three lowest bits flipped.
            var parts = fingerprint.Split('~');
            var baseFingerprint = parts.Length == 2
                ? _works.Single(w => w.Label == parts[0]).Fingerprint!.Value
                : ulong.Parse(fingerprint, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var flipped = parts.Length == 2 ? (1UL << int.Parse(parts[1], CultureInfo.InvariantCulture)) - 1 : 0;
            print = baseFingerprint ^ flipped;
        }

        _incoming = new IncomingFile(
            name,
            WorkSignature.Create(title, Split(authors), year.Length == 0 ? null : int.Parse(year, CultureInfo.InvariantCulture), isbn.Length == 0 ? null : isbn),
            print);
        _decision = _matcher.Decide(_incoming);
        if (_decision.Action == GroupingAction.MergeIntoExisting && print is { } p)
        {
            _matcher.AddFingerprint(_decision.WorkId!.Value, p);
        }
    }

    private WorkMatcher BuildMatcher()
    {
        var text = _configText.ToString();
        var settings = ShelfSettings.Resolve(null, text.Length == 0 ? null : ConfigFile.Parse(text, "library.toml")).Grouping;
        return new WorkMatcher(
            settings,
            _works.Select(w => new WorkCandidate(
                IdOf(w.Label),
                w.Signature,
                w.Fingerprint is { } f ? [f] : [],
                _notDuplicates.GetValueOrDefault(w.Label))));
    }

    private static Ulid IdOf(string label) =>
        Ulid.Create(long.Parse(label.TrimStart('W'), CultureInfo.InvariantCulture), new byte[10]);

    private static string LabelOf(Ulid id) => "W" + id.TimestampMilliseconds.ToString(CultureInfo.InvariantCulture);

    private static string Describe(GroupingDecision decision)
    {
        if (decision.Action == GroupingAction.MergeIntoExisting)
        {
            return "merge into " + LabelOf(decision.WorkId!.Value);
        }

        return decision.Matches.Count == 0
            ? "new work"
            : "new work, review " + string.Join(" and ", decision.Matches.Select(m => LabelOf(m.WorkId)));
    }

    private static string[] Split(string authors) => authors.Length == 0 ? [] : authors.Split("; ");

    private int Distance(string first, string second)
    {
        Assert.NotNull(_fingerprints[first]);
        Assert.NotNull(_fingerprints[second]);
        return SimHash.Distance(_fingerprints[first]!.Value, _fingerprints[second]!.Value);
    }

    private const int Vocabulary = 12000;
    private const int SharedRanks = 120;

    /// <summary>
    /// Text that behaves like running prose: Zipf-distributed words (a few very frequent, a long tail), the most
    /// frequent ones shared by all texts like function words, the tail ranked differently in every text.
    /// </summary>
    private static List<string> Words(int count, int seed)
    {
        var order = Enumerable.Range(0, Vocabulary).ToArray();
        var shuffle = new Random(seed);
        for (var i = Vocabulary - 1; i > SharedRanks; i--)
        {
            var j = SharedRanks + shuffle.Next(i - SharedRanks + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var cumulative = new double[Vocabulary];
        double sum = 0;
        for (var i = 0; i < Vocabulary; i++)
        {
            sum += 1.0 / (i + 1);
            cumulative[i] = sum;
        }

        var pick = new Random(seed * 7919 + 13);
        var words = new List<string>(count);
        for (var k = 0; k < count; k++)
        {
            var rank = Array.BinarySearch(cumulative, pick.NextDouble() * sum);
            if (rank < 0)
            {
                rank = ~rank;
            }

            words.Add(Word(order[Math.Min(rank, Vocabulary - 1)]));
        }

        return words;
    }

    /// <summary>A pronounceable word for a number: one syllable per decimal digit (some with accents).</summary>
    private static string Word(int number) =>
        string.Concat(number.ToString("D5", CultureInfo.InvariantCulture).Select(d => Syllables[d - '0']));
}
