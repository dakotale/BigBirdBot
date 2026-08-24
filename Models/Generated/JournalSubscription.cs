using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class JournalSubscription
{
    public string UserId { get; set; } = null!;

    public TimeOnly DailyTimeUtc { get; set; }

    public string DailyTimeDisplay { get; set; } = null!;

    public DateTime SubscribedAt { get; set; }

    public DateTime? LastReminderSentAt { get; set; }
}
