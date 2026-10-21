using Shelf.Extraction;
using Xunit;

namespace Shelf.Extraction.Tests;

public class RunningHeadsTests
{
    private static List<(int Number, List<string> Words)> Book(int pages, Func<int, string> head, Func<int, string> foot) =>
        [.. Enumerable.Range(1, pages).Select(n => (n, Words($"{head(n)} body{n} text{n} more{n} words{n} here{n} {foot(n)}")))];

    private static List<string> Words(string text) => [.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    [Fact]
    public void A_running_head_and_page_numbers_are_removed()
    {
        var pages = Book(14, _ => "Quiet Systems", n => n.ToString(System.Globalization.CultureInfo.InvariantCulture));

        RunningHeads.Strip(pages);

        Assert.All(pages, p => Assert.DoesNotContain("Quiet", p.Words));
        Assert.All(pages, p => Assert.Equal(5, p.Words.Count));
    }

    [Fact]
    public void Heads_that_alternate_between_left_and_right_pages_are_removed()
    {
        var pages = Book(12, n => n % 2 == 0 ? "Quiet Systems" : "Chapter One", _ => "end");

        RunningHeads.Strip(pages);

        Assert.All(pages, p => Assert.DoesNotContain("Systems", p.Words));
        Assert.All(pages, p => Assert.DoesNotContain("Chapter", p.Words));
    }

    [Fact]
    public void A_short_book_keeps_its_first_words()
    {
        var pages = Book(4, _ => "Quiet Systems", _ => "end");

        RunningHeads.Strip(pages);

        Assert.All(pages, p => Assert.Contains("Quiet", p.Words));
    }

    [Fact]
    public void Pages_without_a_common_start_are_not_changed()
    {
        var pages = Book(14, n => $"unique{n} start{n}", _ => "end-" + 0);
        var before = pages.Select(p => string.Join(' ', p.Words.Take(2))).ToList();

        RunningHeads.Strip(pages);

        Assert.Equal(before, pages.Select(p => string.Join(' ', p.Words.Take(2))));
    }
}
