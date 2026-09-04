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
