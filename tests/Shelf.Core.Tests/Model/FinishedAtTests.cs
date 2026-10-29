using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class FinishedAtTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 30, 45, 678, TimeSpan.Zero);

    private static WorkSidecar Work(ReadStatus status, DateTimeOffset? finished = null) => new()
    {
        Id = Ulid.New(),
        Title = "T",
        Authors = ["A"],
        ReadStatus = status,
        FinishedAt = finished,
        Files = [new SidecarFile("a.pdf")],
    };

    [Fact]
    public void Becoming_read_dates_the_work_to_the_second()
    {
        var read = Work(ReadStatus.Reading).WithReadStatus(ReadStatus.Read, Now);

        Assert.Equal(new DateTimeOffset(2026, 10, 29, 12, 30, 45, TimeSpan.Zero), read.FinishedAt);
    }

    [Theory]
    [InlineData(ReadStatus.Unread)]
    [InlineData(ReadStatus.Reading)]
    public void A_work_that_is_read_already_keeps_its_date_and_leaving_read_takes_it_away(ReadStatus other)
    {
        var read = Work(ReadStatus.Read, Earlier);

        Assert.Equal(Earlier, read.WithReadStatus(ReadStatus.Read, Now).FinishedAt);
        Assert.Null(read.WithReadStatus(other, Now).FinishedAt);
    }

    [Fact]
    public void A_work_that_is_read_without_a_date_stays_without_one()
    {
        Assert.Null(Work(ReadStatus.Read).WithReadStatus(ReadStatus.Read, Now).FinishedAt);
    }

    [Fact]
    public void The_date_survives_a_round_trip_and_is_written_after_the_status()
    {
        var text = SidecarFormat.Serialize(Work(ReadStatus.Read, Earlier));

        Assert.Contains("\"readStatus\": \"read\",\n  \"finishedAt\": \"2026-09-12T10:00:00Z\"", text, StringComparison.Ordinal);
        Assert.Equal(Earlier, SidecarFormat.Parse(text, "x").FinishedAt);
    }

    [Fact]
    public void A_date_in_the_wrong_form_names_the_key()
    {
        var error = Assert.Throws<SidecarException>(() =>
            SidecarFormat.Parse("""{ "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "T", "authors": ["A"], "finishedAt": "last week", "files": [] }""", "book.json"));

        Assert.Contains("finishedAt", error.Message, StringComparison.Ordinal);
    }
}
