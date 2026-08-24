using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Investment
{
    public int InvestmentId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal Multiplier { get; set; }

    public DateTime ReturnsAt { get; set; }

    public bool Claimed { get; set; }

    public DateTime CreatedAt { get; set; }
}
