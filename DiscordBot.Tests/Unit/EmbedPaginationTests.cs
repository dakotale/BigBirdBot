using DiscordBot.Helper;

namespace DiscordBot.Tests.Unit;

public class EmbedPaginationTests
{
    // ── Empty / trivial input ───────────────────────────────────────────────

    [Fact]
    public void BuildPages_EmptyInput_ReturnsSingleEmptyPage()
    {
        var pages = EmbedPagination.BuildPages([], 4096);

        Assert.Single(pages);
        Assert.Equal("", pages[0]);
    }

    [Fact]
    public void BuildPages_LinesFitOnOnePage_ReturnsSinglePage()
    {
        var pages = EmbedPagination.BuildPages(["a\n", "b\n", "c\n"], 4096);

        Assert.Single(pages);
        Assert.Equal("a\nb\nc\n", pages[0]);
    }

    // ── Splitting ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPages_OverflowingLines_SplitsIntoMultiplePages()
    {
        string line = new string('x', 30) + "\n"; // 31 chars
        var lines = Enumerable.Repeat(line, 10).ToList(); // 310 chars total

        var pages = EmbedPagination.BuildPages(lines, 100);

        Assert.True(pages.Count > 1);
        Assert.All(pages, p => Assert.True(p.Length <= 100));
    }

    [Fact]
    public void BuildPages_NeverSplitsALineAcrossPages()
    {
        var lines = new[] { "aaaa\n", "bbbb\n", "cccc\n", "dddd\n" }; // 5 chars each

        var pages = EmbedPagination.BuildPages(lines, 12); // fits exactly 2 lines per page

        Assert.Equal(["aaaa\nbbbb\n", "cccc\ndddd\n"], pages);
    }

    [Fact]
    public void BuildPages_PreservesAllContent_NoDataLost()
    {
        var lines = Enumerable.Range(0, 50).Select(i => $"line{i}\n").ToList();

        var pages = EmbedPagination.BuildPages(lines, 50);

        Assert.Equal(string.Concat(lines), string.Concat(pages));
    }

    // ── Oversized single line (previously a bug: emitted a spurious empty page) ─

    [Fact]
    public void BuildPages_SingleLineLongerThanMax_GetsItsOwnPageWithNoLeadingEmptyPage()
    {
        string hugeLine = new string('x', 200) + "\n";

        var pages = EmbedPagination.BuildPages([hugeLine], 100);

        Assert.Single(pages);
        Assert.Equal(hugeLine, pages[0]);
    }

    [Fact]
    public void BuildPages_OversizedLineAfterNormalLines_FlushesFirstWithNoEmptyPageBetween()
    {
        string normal = "short\n";
        string huge = new string('x', 200) + "\n";

        var pages = EmbedPagination.BuildPages([normal, huge], 100);

        Assert.Equal(2, pages.Count);
        Assert.Equal(normal, pages[0]);
        Assert.Equal(huge, pages[1]);
    }
}
