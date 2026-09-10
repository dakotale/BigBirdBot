using System.Globalization;
using System.Text.RegularExpressions;

namespace DiscordBot.Helper;

/// <summary>
/// Resolves a user-supplied time-zone string to a <see cref="TimeZoneInfo"/> plus a canonical
/// form to persist. Accepts an IANA id (<c>America/New_York</c>), a Windows id
/// (<c>Eastern Standard Time</c>), or a fixed UTC offset (<c>-5</c>, <c>+5.5</c>, <c>UTC-8</c>,
/// <c>GMT+05:30</c>). Fixed offsets round-trip as <c>UTC±HH:MM</c> (or <c>UTC</c> for zero).
/// Modern .NET resolves IANA and Windows ids on every platform via ICU.
/// </summary>
public static partial class TimeZoneResolver
{
    // Optional UTC/GMT prefix, sign, 1–2 digit hours, optional minutes as ":30", "30", or ".5".
    [GeneratedRegex(@"^\s*(?:UTC|GMT)?\s*([+-])\s*(\d{1,2})(?:(?::?([0-5]?\d))|(\.\d+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetPattern();

    /// <summary>
    /// Tries to resolve <paramref name="input"/>. On success <paramref name="zone"/> is the
    /// resolved zone and <paramref name="canonicalId"/> is what to store.
    /// </summary>
    public static bool TryResolve(string? input, out TimeZoneInfo zone, out string canonicalId)
    {
        zone = TimeZoneInfo.Utc;
        canonicalId = "UTC";

        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();

        if (input.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("GMT", StringComparison.OrdinalIgnoreCase))
            return true;

        // Fixed offset — "-5", "+5.5", "UTC+5:30", "GMT-08:00".
        var m = OffsetPattern().Match(input);
        if (m.Success)
        {
            int sign = m.Groups[1].Value == "-" ? -1 : 1;
            int hours = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int minutes = 0;

            if (m.Groups[3].Success)                       // ":30" or "30"
            {
                string mm = m.Groups[3].Value;
                minutes = mm.Length == 1 ? mm[0] - '0' : int.Parse(mm, CultureInfo.InvariantCulture);
            }
            else if (m.Groups[4].Success)                  // ".5" fractional hour
            {
                double frac = double.Parse("0" + m.Groups[4].Value, CultureInfo.InvariantCulture);
                minutes = (int)Math.Round(frac * 60);
                if (minutes % 15 != 0) return false;        // real zones are on 15-minute marks
            }

            var offset = new TimeSpan(sign * hours, sign * minutes, 0);
            if (offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14))
                return false;

            if (offset == TimeSpan.Zero) return true;

            canonicalId = $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
            zone = TimeZoneInfo.CreateCustomTimeZone(canonicalId, offset, canonicalId, canonicalId);
            return true;
        }

        // IANA or Windows id.
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(input);
            canonicalId = zone.Id;
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    /// <summary>Resolves a persisted token back to a zone, falling back to UTC if it no longer resolves.</summary>
    public static TimeZoneInfo Resolve(string? canonicalId) =>
        TryResolve(canonicalId, out var zone, out _) ? zone : TimeZoneInfo.Utc;
}
