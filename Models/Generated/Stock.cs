using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Stock
{
    public string Ticker { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string Sector { get; set; } = null!;

    public decimal Price { get; set; }

    public decimal PrevPrice { get; set; }

    public decimal High24h { get; set; }

    public decimal Low24h { get; set; }

    public decimal Volatility { get; set; }

    public decimal Trend { get; set; }

    public DateTime LastUpdated { get; set; }
}
