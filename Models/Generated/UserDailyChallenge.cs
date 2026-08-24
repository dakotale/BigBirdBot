using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class UserDailyChallenge
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public DateOnly ChallengeDate { get; set; }

    public int Challenge1Id { get; set; }

    public int Challenge2Id { get; set; }

    public int Challenge3Id { get; set; }

    public int Progress1 { get; set; }

    public int Progress2 { get; set; }

    public int Progress3 { get; set; }

    public bool BonusClaimed { get; set; }
}
