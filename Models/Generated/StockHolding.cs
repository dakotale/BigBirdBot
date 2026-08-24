using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class StockHolding
{
    public int HoldingId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string Ticker { get; set; } = null!;

    public int Shares { get; set; }

    public decimal AvgBuyPrice { get; set; }
}
