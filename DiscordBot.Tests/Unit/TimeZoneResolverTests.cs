namespace DiscordBot.Tests.Unit;

public class TimeZoneResolverTests
{
    // ── Fixed offsets ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("-5", "UTC-05:00")]
    [InlineData("+1", "UTC+01:00")]
    [InlineData("UTC-8", "UTC-08:00")]
    [InlineData("GMT+2", "UTC+02:00")]
    [InlineData("+5:30", "UTC+05:30")]
    [InlineData("-3:30", "UTC-03:30")]
    [InlineData("+5.5", "UTC+05:30")]
    [InlineData("utc+05:45", "UTC+05:45")]
    public void TryResolve_Offset_ProducesCanonicalToken(string input, string expected)
    {
        Assert.True(TimeZoneResolver.TryResolve(input, out _, out string canonical));
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("gmt")]
    [InlineData("+0")]
    [InlineData("-0")]
    public void TryResolve_Zero_IsUtc(string input)
    {
        Assert.True(TimeZoneResolver.TryResolve(input, out var zone, out string canonical));
        Assert.Equal("UTC", canonical);
        Assert.Equal(TimeSpan.Zero, zone.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void TryResolve_Offset_RoundTripsThroughResolve()
    {
        Assert.True(TimeZoneResolver.TryResolve("-5:30", out _, out string canonical));
        var zone = TimeZoneResolver.Resolve(canonical);
        Assert.Equal(TimeSpan.FromHours(-5.5), zone.GetUtcOffset(DateTime.UtcNow));
    }

    [Theory]
    [InlineData("+15")]        // beyond ±14
    [InlineData("-13:59:59")]  // seconds not allowed
    [InlineData("+5.1")]       // 6 minutes — not on a 15-minute mark
    [InlineData("banana")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryResolve_Invalid_ReturnsFalse(string? input)
    {
        Assert.False(TimeZoneResolver.TryResolve(input, out _, out _));
    }

    // ── IANA / Windows ids ───────────────────────────────────────────────────

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Kolkata")]
    public void TryResolve_IanaId_ResolvesAndRoundTrips(string id)
    {
        Assert.True(TimeZoneResolver.TryResolve(id, out var zone, out string canonical));
        Assert.NotNull(zone);
        Assert.False(string.IsNullOrWhiteSpace(canonical));

        // Whatever canonical form we store, feeding it back must yield the same offset.
        var reResolved = TimeZoneResolver.Resolve(canonical);
        Assert.Equal(zone.GetUtcOffset(DateTime.UtcNow), reResolved.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void TryResolve_Kolkata_IsFivePlusThirty()
    {
        Assert.True(TimeZoneResolver.TryResolve("Asia/Kolkata", out var zone, out _));
        Assert.Equal(TimeSpan.FromMinutes(330), zone.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void TryResolve_UnknownId_ReturnsFalse()
    {
        Assert.False(TimeZoneResolver.TryResolve("Middle/Earth", out _, out _));
    }

    [Fact]
    public void Resolve_Garbage_FallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, TimeZoneResolver.Resolve("not-a-zone"));
    }
}
