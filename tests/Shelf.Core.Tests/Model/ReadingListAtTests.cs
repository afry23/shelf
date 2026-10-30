using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class ReadingListAtTests
{
    private static readonly DateTimeOffset OnList = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 30, 45, TimeSpan.Zero);

    private static WorkSidecar Work(ReadStatus status, DateTimeOffset? onList = null) => new()
    {
        Id = Ulid.New(),
        Title = "T",
        Authors = ["A"],
        ReadStatus = status,
        ReadingListAt = onList,
        Files = [new SidecarFile("a.pdf")],
    };

    [Fact]
    public void Becoming_read_takes_the_work_off_the_list()
    {
        Assert.Null(Work(ReadStatus.Reading, OnList).WithReadStatus(ReadStatus.Read, Now).ReadingListAt);
    }

    [Theory]
    [InlineData(ReadStatus.Unread)]
    [InlineData(ReadStatus.Reading)]
    public void Another_status_keeps_the_work_on_the_list(ReadStatus status)
    {
        Assert.Equal(OnList, Work(ReadStatus.Unread, OnList).WithReadStatus(status, Now).ReadingListAt);
    }

    [Fact]
    public void Leaving_read_does_not_put_the_work_on_the_list_again()
    {
        var read = Work(ReadStatus.Reading, OnList).WithReadStatus(ReadStatus.Read, Now);

        Assert.Null(read.WithReadStatus(ReadStatus.Unread, Now).ReadingListAt);
    }

    [Fact]
    public void The_time_survives_a_round_trip_and_a_work_off_the_list_has_no_key()
    {
        var text = SidecarFormat.Serialize(Work(ReadStatus.Unread, OnList));

        Assert.Contains("\"readingList\": \"2026-10-01T08:00:00Z\"", text, StringComparison.Ordinal);
        Assert.Equal(OnList, SidecarFormat.Parse(text, "x").ReadingListAt);
        Assert.DoesNotContain("readingList", SidecarFormat.Serialize(Work(ReadStatus.Unread)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_time_in_the_wrong_form_names_the_key()
    {
        var error = Assert.Throws<SidecarException>(() =>
            SidecarFormat.Parse("""{ "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "T", "authors": ["A"], "readingList": "next week", "files": [] }""", "book.json"));

        Assert.Contains("readingList", error.Message, StringComparison.Ordinal);
    }
}
