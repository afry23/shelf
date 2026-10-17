using Reqnroll;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class UlidSteps
{
    private readonly List<Ulid> _ulids = [];
    private bool _parsed;
    private bool _parseFailed;

    [When("shelf creates a ULID for the timestamp {long} and the random bytes {string}")]
    public void WhenShelfCreatesAUlid(long milliseconds, string hex) =>
        _ulids.Add(Ulid.Create(milliseconds, Convert.FromHexString(hex)));

    [When("shelf creates ULIDs for the timestamps {long}, {long} and {long}")]
    public void WhenShelfCreatesUlidsForTheTimestamps(long first, long second, long third)
    {
        // Equal random bytes: only the timestamp may decide the order.
        var random = new byte[10];
        foreach (var milliseconds in new[] { first, second, third })
        {
            _ulids.Add(Ulid.Create(milliseconds, random));
        }
    }

    [When("shelf creates {int} new ULIDs")]
    public void WhenShelfCreatesNewUlids(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _ulids.Add(Ulid.New());
        }
    }

    [When("shelf parses the ULID text {string}")]
    public void WhenShelfParsesTheUlidText(string text)
    {
        _parsed = true;
        if (Ulid.TryParse(text, out var ulid))
        {
            _ulids.Add(ulid);
        }
        else
        {
            _parseFailed = true;
        }
    }

    [Then("the ULID text is {string}")]
    public void ThenTheUlidTextIs(string expected) => Assert.Equal(expected, Assert.Single(_ulids).ToString());

    [Then("the ULID timestamp is {long}")]
    public void ThenTheUlidTimestampIs(long expected) => Assert.Equal(expected, Assert.Single(_ulids).TimestampMilliseconds);

    [Then("the ULID texts are in ascending order")]
    public void ThenTheUlidTextsAreInAscendingOrder()
    {
        var texts = _ulids.Select(u => u.ToString()).ToList();
        Assert.Equal(texts.Order(StringComparer.Ordinal), texts);
        Assert.Equal(texts.Count, texts.Distinct().Count());
    }

    [Then("all ULIDs are valid and different from each other")]
    public void ThenAllUlidsAreValidAndDifferent()
    {
        Assert.All(_ulids, u => Assert.True(Ulid.TryParse(u.ToString(), out var parsed) && parsed == u));
        Assert.Equal(_ulids.Count, _ulids.Distinct().Count());
    }

    [Then("parsing the ULID fails")]
    public void ThenParsingTheUlidFails()
    {
        Assert.True(_parsed);
        Assert.True(_parseFailed);
    }
}
