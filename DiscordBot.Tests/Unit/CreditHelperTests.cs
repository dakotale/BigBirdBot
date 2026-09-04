namespace DiscordBot.Tests.Unit;

public class CreditHelperTests
{
    // ── Format ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,        "⚡ **0**")]
    [InlineData(1_250,    "⚡ **1,250**")]
    [InlineData(1_000_000,"⚡ **1,000,000**")]
    public void Format_ReturnsEmojiAndFormattedNumber(decimal amount, string expected)
    {
        Assert.Equal(expected, CreditHelper.Format(amount));
    }

    [Theory]
    [InlineData(500,  "+⚡ 500")]
    [InlineData(0,    "+⚡ 0")]
    [InlineData(-200, "-⚡ 200")]
    public void FormatDelta_ReturnsSignedString(decimal delta, string expected)
    {
        Assert.Equal(expected, CreditHelper.FormatDelta(delta));
    }

    // ── Prestige ranks ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,                    "🪨 Broke")]
    [InlineData(999_999,              "🪨 Broke")]
    [InlineData(1_000_000,            "🥉 Bronze")]
    [InlineData(9_999_999,            "🥉 Bronze")]
    [InlineData(10_000_000,           "🥈 Silver")]
    [InlineData(100_000_000,          "🥇 Gold")]
    [InlineData(1_000_000_000,        "💎 Diamond")]
    [InlineData(10_000_000_000,       "👑 Elite")]
    [InlineData(100_000_000_000,      "🌟 Legend")]
    [InlineData(1_000_000_000_000,    "🚀 Mythic")]
    [InlineData(999_999_999_999_999,  "🚀 Mythic")]
    public void PrestigeRank_ReturnsCorrectTier(decimal lifetimeEarned, string expected)
    {
        Assert.Equal(expected, CreditHelper.PrestigeRank(lifetimeEarned));
    }

    // ── Streak multiplier ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  1.00, "")]
    [InlineData(2,  1.00, "")]
    [InlineData(3,  1.25, "🔥 3-day streak")]
    [InlineData(4,  1.25, "🔥 3-day streak")]
    [InlineData(5,  1.50, "🔥 5-day streak")]
    [InlineData(7,  2.00, "⚡ Week streak!")]
    [InlineData(13, 2.00, "⚡ Week streak!")]
    [InlineData(14, 3.00, "💎 2-week streak!")]
    [InlineData(29, 3.00, "💎 2-week streak!")]
    [InlineData(30, 5.00, "👑 Monthly streak!")]
    [InlineData(60, 5.00, "👑 Monthly streak!")]
    public void StreakMultiplier_ReturnsCorrectTier(int streak, decimal multiplier, string label)
    {
        var (mult, lbl) = CreditHelper.StreakMultiplier(streak);
        Assert.Equal(multiplier, mult);
        Assert.Equal(label, lbl);
    }

    // ── Pet level-up amount ───────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  500)]
    [InlineData(10, 5_000)]
    [InlineData(50, 25_000)]
    public void PetLevelUpAmount_Returns500TimesLevel(int level, decimal expected)
    {
        Assert.Equal(expected, CreditHelper.PetLevelUpAmount(level));
    }

    // ── IsValidBet ────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidBet_BelowMinimum_ReturnsFalse()
    {
        bool result = CreditHelper.IsValidBet(5m, 1_000m, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void IsValidBet_ExactlyMinimum_ReturnsTrue()
    {
        bool result = CreditHelper.IsValidBet(CreditHelper.MinBet, 1_000m, out string error);
        Assert.True(result);
        Assert.Empty(error);
    }

    [Fact]
    public void IsValidBet_AboveMaximum_ReturnsFalse()
    {
        bool result = CreditHelper.IsValidBet(CreditHelper.MaxBet + 1m, CreditHelper.MaxBet + 2m, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void IsValidBet_ExactlyMaximum_ReturnsTrue()
    {
        bool result = CreditHelper.IsValidBet(CreditHelper.MaxBet, CreditHelper.MaxBet, out string error);
        Assert.True(result);
        Assert.Empty(error);
    }

    [Fact]
    public void IsValidBet_ExceedsBalance_ReturnsFalse()
    {
        bool result = CreditHelper.IsValidBet(500m, 100m, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void IsValidBet_ExactlyBalance_ReturnsTrue()
    {
        bool result = CreditHelper.IsValidBet(100m, 100m, out string error);
        Assert.True(result);
        Assert.Empty(error);
    }

    // ── Slot payouts ─────────────────────────────────────────────────────────

    [Fact]
    public void CalculateSlotPayout_ThreeOfAKind_Diamond_Returns50x()
    {
        var (payout, result) = CreditHelper.CalculateSlotPayout("💎", "💎", "💎", 1_000m);
        Assert.Equal(50_000m, payout);
        Assert.Contains("Three", result);
    }

    [Fact]
    public void CalculateSlotPayout_ThreeOfAKind_Lemon_ReturnsZero()
    {
        // Lemon has multiplier 0.0
        var (payout, _) = CreditHelper.CalculateSlotPayout("🍋", "🍋", "🍋", 1_000m);
        Assert.Equal(0m, payout);
    }

    [Fact]
    public void CalculateSlotPayout_TwoOfAKind_ReturnsHalfBet()
    {
        var (payout, _) = CreditHelper.CalculateSlotPayout("💎", "💎", "🍊", 1_000m);
        Assert.Equal(500m, payout);
    }

    [Fact]
    public void CalculateSlotPayout_CherryConsolation_ReturnsQuarterBet()
    {
        var (payout, result) = CreditHelper.CalculateSlotPayout("🍒", "🍊", "🔔", 1_000m);
        Assert.Equal(250m, payout);
        Assert.Contains("Cherry", result);
    }

    [Fact]
    public void CalculateSlotPayout_NoMatch_ReturnsZero()
    {
        var (payout, _) = CreditHelper.CalculateSlotPayout("🍋", "🍊", "🔔", 1_000m);
        Assert.Equal(0m, payout);
    }

    // ── Roulette payouts ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  "red",   1_000, 1_900)]   // 1 is red
    [InlineData(2,  "black", 1_000, 1_900)]   // 2 is black
    [InlineData(4,  "even",  1_000, 1_900)]   // 4 is even
    [InlineData(7,  "odd",   1_000, 1_900)]   // 7 is odd
    [InlineData(5,  "low",   1_000, 1_900)]   // 5 is 1-18
    [InlineData(20, "high",  1_000, 1_900)]   // 20 is 19-36
    public void CalculateRoulettePayout_WinningBet_Returns1_9x(int spin, string bet, decimal amount, decimal expected)
    {
        var (payout, _) = CreditHelper.CalculateRoulettePayout(spin, bet, amount);
        Assert.Equal(expected, payout);
    }

    [Fact]
    public void CalculateRoulettePayout_ExactNumber_Win_Returns35x()
    {
        var (payout, _) = CreditHelper.CalculateRoulettePayout(7, "7", 1_000m);
        Assert.Equal(35_000m, payout);
    }

    [Fact]
    public void CalculateRoulettePayout_Green_RedBet_ReturnsZero()
    {
        var (payout, _) = CreditHelper.CalculateRoulettePayout(0, "red", 1_000m);
        Assert.Equal(0m, payout);
    }

    [Fact]
    public void CalculateRoulettePayout_WrongNumber_ReturnsZero()
    {
        var (payout, _) = CreditHelper.CalculateRoulettePayout(7, "5", 1_000m);
        Assert.Equal(0m, payout);
    }

    // ── Dice payouts ──────────────────────────────────────────────────────────

    [Fact]
    public void DicePayout_Over_Win_Returns1_8x()
    {
        decimal payout = CreditHelper.DicePayout("over", 4, 5, 1_000m); // total 9 > 7
        Assert.Equal(1_800m, payout);
    }

    [Fact]
    public void DicePayout_Under_Win_Returns1_8x()
    {
        decimal payout = CreditHelper.DicePayout("under", 3, 2, 1_000m); // total 5 < 7
        Assert.Equal(1_800m, payout);
    }

    [Fact]
    public void DicePayout_Seven_Win_Returns4x()
    {
        decimal payout = CreditHelper.DicePayout("seven", 3, 4, 1_000m);
        Assert.Equal(4_000m, payout);
    }

    [Fact]
    public void DicePayout_Doubles_Win_Returns6x()
    {
        decimal payout = CreditHelper.DicePayout("doubles", 3, 3, 1_000m);
        Assert.Equal(6_000m, payout);
    }

    [Fact]
    public void DicePayout_Over_Lose_ReturnsZero()
    {
        decimal payout = CreditHelper.DicePayout("over", 3, 3, 1_000m); // total 6 not > 7
        Assert.Equal(0m, payout);
    }

    [Fact]
    public void DicePayout_Seven_Lose_ReturnsZero()
    {
        decimal payout = CreditHelper.DicePayout("seven", 4, 5, 1_000m); // total 9 ≠ 7
        Assert.Equal(0m, payout);
    }

    // ── Race results ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void BuildRaceResult_WinnerIsAtPositionZero(int winnerIndex)
    {
        int[] positions = CreditHelper.BuildRaceResult(winnerIndex);
        Assert.Equal(winnerIndex, positions[0]);
    }

    [Fact]
    public void BuildRaceResult_ContainsAllHorses()
    {
        int[] positions = CreditHelper.BuildRaceResult(0);
        Assert.Equal(CreditHelper.Horses.Length, positions.Length);
        Assert.Equal(CreditHelper.Horses.Length, positions.Distinct().Count());
        Assert.All(positions, p => Assert.InRange(p, 0, CreditHelper.Horses.Length - 1));
    }
}
