using DiscordBot.Constants;

namespace DiscordBot.Tests.Unit;

public class URLCleanupTests
{
    private readonly URLCleanup _sut = new();

    // ── CleanURLEmbed — null / empty guards ───────────────────────────────────

    [Fact]
    public void CleanURLEmbed_NullMessage_ReturnsNull()
    {
        Assert.Null(_sut.CleanURLEmbed(null!));
    }

    [Fact]
    public void CleanURLEmbed_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _sut.CleanURLEmbed(""));
    }

    [Fact]
    public void CleanURLEmbed_NoURL_ReturnsUnchanged()
    {
        const string msg = "Hello, world! No links here.";
        Assert.Equal(msg, _sut.CleanURLEmbed(msg));
    }

    // ── CleanURLEmbed — Twitter / X variants ─────────────────────────────────

    [Theory]
    [InlineData("https://twitter.com/user/status/123",     "https://dl.fxtwitter.com/user/status/123")]
    [InlineData("https://x.com/user/status/456",           "https://dl.fxtwitter.com/user/status/456")]
    [InlineData("https://fxtwitter.com/user/status/789",   "https://dl.fxtwitter.com/user/status/789")]
    [InlineData("https://vxtwitter.com/user/status/000",   "https://dl.fxtwitter.com/user/status/000")]
    [InlineData("https://girlcockx.com/user/status/111",   "https://dl.fxtwitter.com/user/status/111")]
    public void CleanURLEmbed_TwitterVariants_ReplacedWithFxtwitter(string input, string expected)
    {
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    [Fact]
    public void CleanURLEmbed_TikTok_ReplacedWithVxTikTok()
    {
        const string input    = "https://tiktok.com/@user/video/1234567890";
        const string expected = "https://vxtiktok.com/@user/video/1234567890";
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    [Fact]
    public void CleanURLEmbed_Bluesky_ReplacedWithBskx()
    {
        const string input    = "https://bsky.app/profile/someone.bsky.social";
        const string expected = "https://bskx.app/profile/someone.bsky.social";
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    [Theory]
    [InlineData("https://reddit.com/r/programming/comments/abc/title/",
                "https://rxddit.com/r/programming/comments/abc/title/")]
    [InlineData("https://www.reddit.com/r/gaming/comments/xyz/",
                "https://rxddit.com/r/gaming/comments/xyz/")]
    public void CleanURLEmbed_Reddit_ReplacedWithRxddit(string input, string expected)
    {
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    // ── CleanURLEmbed — no "https://" prefix means no replacement ─────────────

    [Fact]
    public void CleanURLEmbed_HttpWithoutS_NotReplaced()
    {
        // The method only triggers on "https://" prefix
        const string input = "http://twitter.com/user/status/123";
        Assert.Equal(input, _sut.CleanURLEmbed(input));
    }

    [Fact]
    public void CleanURLEmbed_DomainOnlyNoProtocol_NotReplaced()
    {
        const string input = "twitter.com/user/status/123";
        Assert.Equal(input, _sut.CleanURLEmbed(input));
    }

    // ── CleanURLEmbed — surrounding text is preserved ─────────────────────────

    [Fact]
    public void CleanURLEmbed_SurroundingText_Preserved()
    {
        const string input    = "Check this out: https://twitter.com/user/status/1 — amazing!";
        const string expected = "Check this out: https://dl.fxtwitter.com/user/status/1 — amazing!";
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    [Fact]
    public void CleanURLEmbed_MultipleURLs_AllReplaced()
    {
        const string input    = "https://twitter.com/a and https://reddit.com/b";
        const string expected = "https://dl.fxtwitter.com/a and https://rxddit.com/b";
        Assert.Equal(expected, _sut.CleanURLEmbed(input));
    }

    [Fact]
    public void CleanURLEmbed_NonSocialUrl_Unchanged()
    {
        const string input = "https://example.com/some/path";
        Assert.Equal(input, _sut.CleanURLEmbed(input));
    }

    // ── CleanURLEmbed — already-fixed URLs are idempotent ─────────────────────

    [Fact]
    public void CleanURLEmbed_AlreadyFixed_Idempotent()
    {
        const string already = "https://dl.fxtwitter.com/user/status/123";
        // dl.fxtwitter.com is not in the replacement keys — should be unchanged
        Assert.Equal(already, _sut.CleanURLEmbed(already));
    }

    [Fact]
    public void CleanURLEmbed_VxTikTokAlready_Idempotent()
    {
        const string already = "https://vxtiktok.com/@user/video/9999";
        Assert.Equal(already, _sut.CleanURLEmbed(already));
    }

    // ── HasSocialMediaEmbed — null / empty ────────────────────────────────────

    [Fact]
    public void HasSocialMediaEmbed_NullMessage_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed(null!));
    }

    [Fact]
    public void HasSocialMediaEmbed_EmptyString_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed(""));
    }

    [Fact]
    public void HasSocialMediaEmbed_PlainText_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed("No link here at all!"));
    }

    // ── HasSocialMediaEmbed — positive cases ──────────────────────────────────

    [Theory]
    [InlineData("https://twitter.com/user/status/1")]
    [InlineData("https://x.com/user/status/1")]
    [InlineData("https://fxtwitter.com/user/status/1")]
    [InlineData("https://vxtwitter.com/user/status/1")]
    [InlineData("https://girlcockx.com/user/status/1")]
    [InlineData("https://tiktok.com/@user/video/1")]
    [InlineData("https://bsky.app/profile/someone")]
    [InlineData("https://reddit.com/r/test")]
    [InlineData("https://www.reddit.com/r/test")]
    public void HasSocialMediaEmbed_KnownDomain_ReturnsTrue(string url)
    {
        Assert.True(_sut.HasSocialMediaEmbed(url));
    }

    [Fact]
    public void HasSocialMediaEmbed_SocialUrlInSentence_ReturnsTrue()
    {
        Assert.True(_sut.HasSocialMediaEmbed("Hey check this https://twitter.com/status/123 out!"));
    }

    // ── HasSocialMediaEmbed — negative cases ──────────────────────────────────

    [Fact]
    public void HasSocialMediaEmbed_NonSocialUrl_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed("https://example.com/page"));
    }

    [Fact]
    public void HasSocialMediaEmbed_HttpNotHttps_ReturnsFalse()
    {
        // SocialMediaDomains is built with "https://" prefix
        Assert.False(_sut.HasSocialMediaEmbed("http://twitter.com/user"));
    }

    [Fact]
    public void HasSocialMediaEmbed_DomainWithoutProtocol_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed("twitter.com/user/status/1"));
    }

    [Fact]
    public void HasSocialMediaEmbed_AlreadyFixedDomain_ReturnsFalse()
    {
        // dl.fxtwitter.com is the *target*, not a source — not in SocialMediaDomains
        Assert.False(_sut.HasSocialMediaEmbed("https://dl.fxtwitter.com/user/status/1"));
    }

    [Fact]
    public void HasSocialMediaEmbed_VxTikTokTarget_ReturnsFalse()
    {
        Assert.False(_sut.HasSocialMediaEmbed("https://vxtiktok.com/@user/video/9999"));
    }
}
