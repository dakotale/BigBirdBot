using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class GambleLog
{
    public int LogId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string Game { get; set; } = null!;

    public decimal Bet { get; set; }

    public decimal Payout { get; set; }

    public decimal Net { get; set; }

    public DateTime CreatedAt { get; set; }
}
