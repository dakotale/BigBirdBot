namespace DiscordBot.Data;

/// <summary>
/// One occurrence of a registered birthday. <c>/addbirthday</c> inserts 9 rows (one per year,
/// this year through +8) so the exact-date match in <see cref="SchedulingService.GetTodaysBirthdaysAsync"/>
/// fires once annually with no wraparound logic. Table <c>dbo.Birthday</c>.
/// </summary>
public sealed class Birthday
{
    public int BirthdayId { get; set; }
    public DateTime BirthdayDate { get; set; }
    public string BirthdayUser { get; set; } = "";
    public string BirthdayGuild { get; set; } = "";
    public bool Sent { get; set; }
    public string? BirthdayChannel { get; set; }
}

/// <summary>A one-off DM reminder scheduled via <c>/remind</c>. Table <c>dbo.Reminders</c>.</summary>
public sealed class Reminder
{
    public int ReminderId { get; set; }
    public string UserId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime RemindAtUtc { get; set; }
    public bool Sent { get; set; }
}

/// <summary>
/// A member's saved time zone, shared across every server (one row per user, not per-server).
/// Set via <c>/timezone</c>; read by <c>/remind</c> so users don't re-enter a UTC offset each
/// time. <see cref="TimeZone"/> is an IANA id (e.g. <c>America/New_York</c>) or a fixed-offset
/// token (e.g. <c>UTC-05:00</c>) — resolve it with <see cref="Helper.TimeZoneResolver"/>.
/// Table <c>UserTimezone</c> (added by <c>SQL/Database/postgres/003_UserTimezone.sql</c>).
/// </summary>
public sealed class UserTimezone
{
    public string UserId { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}
