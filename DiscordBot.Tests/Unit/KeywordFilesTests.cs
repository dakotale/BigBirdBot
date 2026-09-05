using DiscordBot.Helper;

namespace DiscordBot.Tests.Unit;

public class KeywordFilesTests
{
    [Theory]
    [InlineData("http://example.com/a.jpg")]
    [InlineData("https://example.com/a.jpg")]
    [InlineData("HTTPS://EXAMPLE.COM/a.jpg")]
    public void IsUrl_TrueForHttpValues(string value) => Assert.True(KeywordFiles.IsUrl(value));

    [Theory]
    [InlineData("file:cat/x.jpg")]
    [InlineData("just some plain text")]
    [InlineData("ftp://example.com/a")]
    public void IsUrl_FalseForNonHttp(string value) => Assert.False(KeywordFiles.IsUrl(value));

    [Theory]
    [InlineData("file:cat/social_x.jpg")]
    [InlineData(@"C:\Temp\DiscordBot\cat\x.jpg")]      // legacy absolute path still recognised
    [InlineData("/srv/keywords/cat/x.jpg")]            // legacy rooted path (other OS)
    public void IsLocalFile_TrueForFileSchemeAndAbsolutePaths(string value) =>
        Assert.True(KeywordFiles.IsLocalFile(value));

    [Theory]
    [InlineData("https://example.com/a.jpg")]
    [InlineData("i would spread that dick cheese on toast")]
    [InlineData("him/her")]                            // plain text with a slash is NOT a file
    public void IsLocalFile_FalseForUrlsAndText(string value) =>
        Assert.False(KeywordFiles.IsLocalFile(value));

    [Fact]
    public void ToStored_ProducesFileSchemeValue() =>
        Assert.Equal("file:cat/social_x.jpg", KeywordFiles.ToStored("cat", "social_x.jpg"));

    [Fact]
    public void Resolve_FileScheme_ProducesAbsolutePathEndingWithKeywordAndName()
    {
        string resolved = KeywordFiles.Resolve("file:cat/social_x.jpg");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.Equal("social_x.jpg", Path.GetFileName(resolved));
        Assert.Contains($"cat{Path.DirectorySeparatorChar}social_x.jpg", resolved);
    }

    [Fact]
    public void Resolve_LegacyAbsolutePath_ReturnedUnchanged()
    {
        const string legacy = @"C:\Temp\DiscordBot\cat\x.jpg";
        Assert.Equal(legacy, KeywordFiles.Resolve(legacy));
    }

    [Fact]
    public void Resolve_RejectsPathTraversal() =>
        Assert.Throws<InvalidOperationException>(() => KeywordFiles.Resolve("file:cat/../../../x"));
}
