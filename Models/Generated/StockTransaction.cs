using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class StockTransaction
{
    public int TxId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string Ticker { get; set; } = null!;

    public string TxType { get; set; } = null!;

    public int Shares { get; set; }

    public decimal PriceEach { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime TxTime { get; set; }
}
