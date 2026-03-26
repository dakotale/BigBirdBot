namespace DiscordBot.Helper;

/// <summary>
/// Static helpers for the stock market system.
/// Handles price tick simulation, sparklines, and formatting.
/// </summary>
public static class StockHelper
{

    /// <summary>How often prices update (minutes).</summary>
    public const int TickIntervalMinutes = 15;

    /// <summary>Hard ceiling for any stock price. Prevents runaway inflation from
    /// exceeding the DECIMAL(18,2) column and keeps the game sensible.</summary>
    public const decimal MaxPrice = 9_999_999.99m;


    /// <summary>
    /// Calculates a new price using a biased random walk.
    /// Volatility is the stock's std dev; trend is a slight directional nudge.
    /// Price floors at $1.00 and is capped at <see cref="MaxPrice"/>.
    /// </summary>
    public static decimal NextPrice(decimal current, double volatility, double trend)
    {
        // If the stored price is already above the cap (legacy inflation),
        // bleed it back down toward the cap instead of compounding further.
        if (current > MaxPrice)
            current = MaxPrice;

        // Box-Muller transform for a normally distributed random variable
        double u1 = 1.0 - Random.Shared.NextDouble();
        double u2 = 1.0 - Random.Shared.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        double pctChange = trend + volatility * z;

        // Hard clamp: max ±30% in a single tick
        pctChange = Math.Clamp(pctChange, -0.30, 0.30);

        decimal next = current * (decimal)(1.0 + pctChange);
        return Math.Clamp(Math.Round(next, 2), 1.00m, MaxPrice);
    }


    private static readonly char[] SparkChars = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    /// <summary>
    /// Builds a Unicode sparkline from a list of prices (oldest first).
    /// Returns an empty string if fewer than 2 data points.
    /// </summary>
    public static string Sparkline(IEnumerable<decimal> prices)
    {
        var pts = prices.ToArray();
        if (pts.Length < 2) return string.Empty;

        decimal min = pts.Min();
        decimal max = pts.Max();
        decimal rng = max - min;

        if (rng == 0) return new string(SparkChars[3], pts.Length);

        return string.Concat(pts.Select(p =>
        {
            int idx = (int)Math.Round((double)(p - min) / (double)rng * (SparkChars.Length - 1));
            return SparkChars[Math.Clamp(idx, 0, SparkChars.Length - 1)];
        }));
    }


    /// <summary>Formats a stock price with two decimal places.</summary>
    public static string FormatPrice(decimal price) => $"${price:N2}";

    /// <summary>Returns a coloured change string e.g. "+2.45 (+1.72%)".</summary>
    public static string FormatChange(decimal current, decimal prev)
    {
        decimal change = current - prev;
        decimal pct = prev == 0 ? 0 : change / prev * 100;
        string sign = change >= 0 ? "+" : "";
        return $"{sign}{change:N2} ({sign}{pct:N2}%)";
    }

    /// <summary>Arrow + colour indicator based on price movement.</summary>
    public static string TrendArrow(decimal current, decimal prev) =>
        current > prev ? "📈" : current < prev ? "📉" : "➡️";

    /// <summary>Sector emoji for embed flavour.</summary>
    public static string SectorEmoji(string sector) => sector.ToLower() switch
    {
        "tech" => "💻",
        "energy" => "⚡",
        "healthcare" => "🏥",
        "media" => "📺",
        "finance" => "🏦",
        "consumer" => "🛍️",
        "materials" => "⛏️",
        "aerospace" => "🚀",
        "industrial" => "🏭",
        _ => "📊"
    };

    /// <summary>P&amp;L colour: green if profit, red if loss, grey if flat.</summary>
    public static Discord.Color PnLColour(decimal pnl) =>
        pnl > 0 ? new Discord.Color(87, 242, 135) :
        pnl < 0 ? new Discord.Color(237, 66, 69) :
                   new Discord.Color(153, 170, 181);

    /// <summary>Formats unrealized P&amp;L with sign and emoji.</summary>
    public static string FormatPnL(decimal pnl)
    {
        string sign = pnl >= 0 ? "+" : "";
        string emoji = pnl > 0 ? "🟢" : pnl < 0 ? "🔴" : "⚪";
        return $"{emoji} {sign}{pnl:N2}";
    }

    /// <summary>
    /// Compacts a large number to K/M/B suffix for use in fixed-width table columns.
    /// e.g. 10_000_000 → "10M", 50_000 → "50K", 474 → "474"
    /// </summary>
    public static string CompactShares(long n)
    {
        if (n >= 1_000_000_000) return $"{n / 1_000_000_000.0:0.##}B";
        if (n >= 1_000_000) return $"{n / 1_000_000.0:0.##}M";
        if (n >= 1_000) return $"{n / 1_000.0:0.##}K";
        return n.ToString();
    }

    /// <summary>
    /// Compacts a P&amp;L value to K/M/B suffix with sign.
    /// e.g. +7_152_500 → "+$7.15M", -1_433_400 → "-$1.43M", +474.30 → "+$474"
    /// </summary>
    public static string CompactPnL(decimal pnl)
    {
        string sign = pnl >= 0 ? "+" : "-";
        decimal abs = Math.Abs(pnl);
        string val = abs switch
        {
            >= 1_000_000_000m => $"${abs / 1_000_000_000m:0.##}B",
            >= 1_000_000m => $"${abs / 1_000_000m:0.##}M",
            >= 1_000m => $"${abs / 1_000m:0.##}K",
            _ => $"${abs:N0}"
        };
        return sign + val;
    }
}
