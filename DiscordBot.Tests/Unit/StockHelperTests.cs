namespace DiscordBot.Tests.Unit;

public class StockHelperTests
{
    // ── NextPrice bounds ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(100,      5.0, 1.0)]
    [InlineData(1.00,     5.0, 1.0)]   // at floor
    [InlineData(9_999.99, 5.0, 1.0)]   // at ceiling
    [InlineData(500,     10.0, 3.0)]   // high volatility
    public void NextPrice_AlwaysWithinBounds(decimal current, double volatility, double trend)
    {
        for (int i = 0; i < 500; i++)
        {
            decimal next = StockHelper.NextPrice(current, volatility, trend);
            Assert.InRange(next, 1.00m, StockHelper.MaxPrice);
        }
    }

    [Fact]
    public void NextPrice_AboveMaxPrice_ClampsBeforeCalculating()
    {
        // Any price above MaxPrice should be clamped to MaxPrice first,
        // then produce a value still within [1, MaxPrice].
        decimal overCap = StockHelper.MaxPrice + 5_000m;
        for (int i = 0; i < 100; i++)
        {
            decimal next = StockHelper.NextPrice(overCap, 5.0, 1.0);
            Assert.InRange(next, 1.00m, StockHelper.MaxPrice);
        }
    }

    // ── Sparkline ─────────────────────────────────────────────────────────────

    [Fact]
    public void Sparkline_EmptySequence_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, StockHelper.Sparkline(Array.Empty<decimal>()));
    }

    [Fact]
    public void Sparkline_SinglePoint_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, StockHelper.Sparkline(new[] { 100m }));
    }

    [Fact]
    public void Sparkline_AllEqualPrices_ReturnsMidChars()
    {
        string spark = StockHelper.Sparkline(new[] { 100m, 100m, 100m });
        // When range == 0, every character is SparkChars[3] = '▄'
        Assert.Equal(3, spark.Length);
        Assert.All(spark.ToCharArray(), c => Assert.Equal('▄', c));
    }

    [Fact]
    public void Sparkline_AscendingPrices_StartsLowEndsHigh()
    {
        string spark = StockHelper.Sparkline(new[] { 100m, 200m });
        Assert.Equal(2, spark.Length);
        Assert.True(spark[0] < spark[1], "First char should be lower block than last char for ascending prices");
    }

    [Fact]
    public void Sparkline_DescendingPrices_StartsHighEndsLow()
    {
        string spark = StockHelper.Sparkline(new[] { 200m, 100m });
        Assert.Equal(2, spark.Length);
        Assert.True(spark[0] > spark[1], "First char should be higher block than last char for descending prices");
    }

    [Fact]
    public void Sparkline_LengthMatchesInputCount()
    {
        var prices = new[] { 100m, 110m, 105m, 120m, 95m };
        Assert.Equal(prices.Length, StockHelper.Sparkline(prices).Length);
    }

    // ── FormatPrice ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100.5,    "$100.50")]
    [InlineData(9_999.99, "$9,999.99")]
    [InlineData(1.00,     "$1.00")]
    public void FormatPrice_ReturnsFormattedDollarAmount(decimal price, string expected)
    {
        Assert.Equal(expected, StockHelper.FormatPrice(price));
    }

    // ── FormatChange ──────────────────────────────────────────────────────────

    [Fact]
    public void FormatChange_Gain_HasPositiveSign()
    {
        string result = StockHelper.FormatChange(110m, 100m);
        Assert.StartsWith("+", result);
        Assert.Contains("10.00", result);
        Assert.Contains("10.00%", result);
    }

    [Fact]
    public void FormatChange_Loss_HasNegativeSign()
    {
        string result = StockHelper.FormatChange(90m, 100m);
        Assert.StartsWith("-", result);
    }

    [Fact]
    public void FormatChange_ZeroPrev_DoesNotDivideByZero()
    {
        string result = StockHelper.FormatChange(100m, 0m);
        Assert.Contains("0.00%", result);
    }

    [Fact]
    public void FormatChange_NoChange_ShowsZeroDelta()
    {
        string result = StockHelper.FormatChange(100m, 100m);
        Assert.StartsWith("+", result);
        Assert.Contains("0.00", result);
    }

    // ── TrendArrow ────────────────────────────────────────────────────────────

    [Fact]
    public void TrendArrow_Higher_ReturnsUpArrow()
    {
        Assert.Equal("📈", StockHelper.TrendArrow(110m, 100m));
    }

    [Fact]
    public void TrendArrow_Lower_ReturnsDownArrow()
    {
        Assert.Equal("📉", StockHelper.TrendArrow(90m, 100m));
    }

    [Fact]
    public void TrendArrow_Equal_ReturnsFlatArrow()
    {
        Assert.Equal("➡️", StockHelper.TrendArrow(100m, 100m));
    }

    // ── SectorEmoji ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tech",       "💻")]
    [InlineData("TECH",       "💻")]   // case-insensitive
    [InlineData("energy",     "⚡")]
    [InlineData("healthcare", "🏥")]
    [InlineData("media",      "📺")]
    [InlineData("finance",    "🏦")]
    [InlineData("consumer",   "🛍️")]
    [InlineData("materials",  "⛏️")]
    [InlineData("aerospace",  "🚀")]
    [InlineData("industrial", "🏭")]
    [InlineData("unknown",    "📊")]
    [InlineData("",           "📊")]
    public void SectorEmoji_ReturnsCorrectEmoji(string sector, string expected)
    {
        Assert.Equal(expected, StockHelper.SectorEmoji(sector));
    }

    // ── CompactShares ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,               "0")]
    [InlineData(999,             "999")]
    [InlineData(1_000,           "1K")]
    [InlineData(1_500,           "1.5K")]
    [InlineData(1_000_000,       "1M")]
    [InlineData(1_500_000,       "1.5M")]
    [InlineData(1_000_000_000L,  "1B")]
    [InlineData(2_000_000_000L,  "2B")]
    public void CompactShares_ReturnsCorrectSuffix(long shares, string expected)
    {
        Assert.Equal(expected, StockHelper.CompactShares(shares));
    }

    // ── CompactPnL ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(474.30,        "+$474")]
    [InlineData(-474.30,       "-$474")]
    [InlineData(50_000,        "+$50K")]
    [InlineData(-50_000,       "-$50K")]
    [InlineData(7_152_500,     "+$7.15M")]
    [InlineData(-1_433_400,    "-$1.43M")]
    [InlineData(2_000_000_000, "+$2B")]
    [InlineData(-2_000_000_000,"-$2B")]
    public void CompactPnL_ReturnsCorrectFormat(decimal pnl, string expected)
    {
        Assert.Equal(expected, StockHelper.CompactPnL(pnl));
    }
}
