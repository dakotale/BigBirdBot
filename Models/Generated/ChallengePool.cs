using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ChallengePool
{
    public int ChallengeId { get; set; }

    public string Key { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string GameType { get; set; } = null!;

    public int TargetCount { get; set; }

    public decimal RewardAmount { get; set; }

    public short Difficulty { get; set; }
}
