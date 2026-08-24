using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Credit
{
    public int CreditId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public decimal Balance { get; set; }

    public decimal TotalEarned { get; set; }

    public decimal TotalSpent { get; set; }

    public DateTime? LastDaily { get; set; }

    public DateTime? LastWork { get; set; }

    public int DailyStreak { get; set; }

    public DateOnly? LastStreakDate { get; set; }

    public decimal LifetimeEarned { get; set; }
}
