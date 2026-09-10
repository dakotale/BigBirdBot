using System.Globalization;
using System.Text.RegularExpressions;

namespace DiscordBot.Helper;

/// <summary>
/// Parses the free-text <c>when</c> argument of <c>/remind</c> into a UTC instant, interpreting
/// bare clock times and dates in the user's own time zone. Supports:
///   • relative durations — <c>in 90m</c>, <c>2h30m</c>, <c>in 3 days</c>, <c>1w</c>
///   • <c>today</c> / <c>tomorrow</c> with an optional time — <c>tomorrow 9am</c>
///   • absolute dates/times parsed with the invariant culture — <c>2026-03-25 15:30</c>,
///     <c>03/25/2026 3:30 PM</c> (month/day order, never locale-dependent)
/// </summary>
public static partial class WhenParser
{
    // Longest alternatives first — regex alternation is ordered, so "week" must precede "w".
    [GeneratedRegex(@"(\d+)\s*(weeks?|w|days?|d|hours?|hrs?|h|minutes?|mins?|m)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationUnit();

    [GeneratedRegex(@"^\s*(?:in\s+)?(?:\d+\s*(?:weeks?|w|days?|d|hours?|hrs?|h|minutes?|mins?|m)\s*)+$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativePattern();

    [GeneratedRegex(@"^\s*(today|tomorrow)(?:\s+(?:at\s+)?(.+?))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DayWordPattern();

    /// <summary>
    /// Tries to parse <paramref name="when"/> relative to <paramref name="nowUtc"/> and the
    /// user's <paramref name="zone"/>. On success <paramref name="resultUtc"/> is the target
    /// instant with <see cref="DateTimeKind.Utc"/>. Does not enforce any min/max lead time —
    /// the caller does that.
    /// </summary>
    public static bool TryParse(string when, TimeZoneInfo zone, DateTime nowUtc, out DateTime resultUtc)
    {
        resultUtc = default;
        if (string.IsNullOrWhiteSpace(when)) return false;
        when = when.Trim();
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        // ── Relative duration ────────────────────────────────────────────────
        if (RelativePattern().IsMatch(when))
        {
            var span = TimeSpan.Zero;
            foreach (Match part in DurationUnit().Matches(when))
            {
                int n = int.Parse(part.Groups[1].Value, CultureInfo.InvariantCulture);
                span += part.Groups[2].Value.ToLowerInvariant().TrimEnd('s') switch
                {
                    "w" or "week"          => TimeSpan.FromDays(7 * n),
                    "d" or "day"           => TimeSpan.FromDays(n),
                    "h" or "hr" or "hour"  => TimeSpan.FromHours(n),
                    _                      => TimeSpan.FromMinutes(n),
                };
            }

            if (span <= TimeSpan.Zero) return false;
            resultUtc = nowUtc + span;
            return true;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);

        // ── today / tomorrow [+ time] ────────────────────────────────────────
        var dayWord = DayWordPattern().Match(when);
        if (dayWord.Success)
        {
            var date = nowLocal.Date;
            if (dayWord.Groups[1].Value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
                date = date.AddDays(1);

            var timeOfDay = TimeSpan.Zero;
            if (dayWord.Groups[2].Success && dayWord.Groups[2].Value.Length > 0 &&
                !TryParseTime(dayWord.Groups[2].Value, out timeOfDay))
                return false;

            resultUtc = ToUtc(date + timeOfDay, zone);
            return true;
        }

        // ── Absolute date/time (invariant culture, month/day order) ──────────
        if (DateTime.TryParse(when, CultureInfo.InvariantCulture,
                DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            // A time with no date parses onto year 1 — treat it as "today, that time".
            if (parsed.Year == 1)
                parsed = nowLocal.Date + parsed.TimeOfDay;

            resultUtc = ToUtc(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), zone);
            return true;
        }

        return false;
    }

    private static bool TryParseTime(string text, out TimeSpan time)
    {
        time = default;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.NoCurrentDateDefault, out var t))
        {
            time = t.TimeOfDay;
            return true;
        }
        return false;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))   // clock skipped forward over this local time (DST)
            local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
