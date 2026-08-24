using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class StockHistory
{
    public int HistoryId { get; set; }

    public string Ticker { get; set; } = null!;

    public decimal Price { get; set; }

    public DateTime RecordedAt { get; set; }
}
