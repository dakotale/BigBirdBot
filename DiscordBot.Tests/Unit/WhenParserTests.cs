namespace DiscordBot.Tests.Unit;

public class WhenParserTests
{
    // Fixed-offset zone (UTC-5, no DST) and a fixed "now" for deterministic assertions.
    private static readonly TimeZoneInfo Est = TimeZoneResolver.Resolve("UTC-05:00");
    private static readonly DateTime NowUtc = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc); // 07:00 local

    private static DateTime Parse(string when)
    {
        Assert.True(WhenParser.TryParse(when, Est, NowUtc, out var utc), $"failed to parse: {when}");
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        return utc;
    }

    // ── Relative durations ───────────────────────────────────────────────────

    [Theory]
    [InlineData("in 90m", 90)]
    [InlineData("90m", 90)]
    [InlineData("in 2h", 120)]
    [InlineData("2h30m", 150)]
    [InlineData("in 2 hours 30 min", 150)]
    [InlineData("in 3 days", 3 * 24 * 60)]
    [InlineData("1w", 7 * 24 * 60)]
    [InlineData("in 1d 12h", 36 * 60)]
    public void TryParse_RelativeDuration_AddsToNow(string when, int expectedMinutes)
    {
        Assert.Equal(NowUtc.AddMinutes(expectedMinutes), Parse(when));
    }

    [Theory]
    [InlineData("in 0m")]
    [InlineData("0h")]
    public void TryParse_ZeroDuration_Fails(string when)
    {
        Assert.False(WhenParser.TryParse(when, Est, NowUtc, out _));
    }

    // ── today / tomorrow ─────────────────────────────────────────────────────

    [Fact]
    public void TryParse_TomorrowWithTime_IsNextDayLocal()
    {
        // 09:00 local on Mar 11 == 14:00 UTC
        Assert.Equal(new DateTime(2026, 3, 11, 14, 0, 0, DateTimeKind.Utc), Parse("tomorrow 9am"));
    }

    [Fact]
    public void TryParse_TodayWithTime_IsSameDayLocal()
    {
        // 18:00 local on Mar 10 == 23:00 UTC
        Assert.Equal(new DateTime(2026, 3, 10, 23, 0, 0, DateTimeKind.Utc), Parse("today 18:00"));
    }

    [Fact]
    public void TryParse_TomorrowNoTime_IsMidnightLocal()
    {
        // 00:00 local on Mar 11 == 05:00 UTC
        Assert.Equal(new DateTime(2026, 3, 11, 5, 0, 0, DateTimeKind.Utc), Parse("tomorrow"));
    }

    // ── Absolute dates (month/day order, invariant) ──────────────────────────

    [Theory]
    [InlineData("2026-03-25 15:30")]
    [InlineData("03/25/2026 3:30 PM")]
    [InlineData("March 25, 2026 15:30")]
    public void TryParse_AbsoluteDateTime_ConvertsFromUserZone(string when)
    {
        // 15:30 local on Mar 25 == 20:30 UTC
        Assert.Equal(new DateTime(2026, 3, 25, 20, 30, 0, DateTimeKind.Utc), Parse(when));
    }

    [Fact]
    public void TryParse_BareTime_IsTodayAtThatLocalTime()
    {
        // "3:30 PM" with no date -> today (Mar 10) 15:30 local == 20:30 UTC
        Assert.Equal(new DateTime(2026, 3, 10, 20, 30, 0, DateTimeKind.Utc), Parse("3:30 PM"));
    }

    [Fact]
    public void TryParse_DdMmYy_IsNotInterpretedAsDayFirst()
    {
        // "04/05/2026" is April 5 (month/day), never May 4.
        Assert.True(WhenParser.TryParse("04/05/2026 12:00", Est, NowUtc, out var utc));
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Est);
        Assert.Equal(4, local.Month);
        Assert.Equal(5, local.Day);
    }

    // ── Rejections ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("whenever")]
    [InlineData("next tuesday-ish")]
    public void TryParse_Garbage_Fails(string when)
    {
        Assert.False(WhenParser.TryParse(when, Est, NowUtc, out _));
    }
}
