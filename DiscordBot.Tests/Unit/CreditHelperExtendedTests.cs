namespace DiscordBot.Tests.Unit;

public class CreditHelperExtendedTests
{
    // ── WorkMessage ───────────────────────────────────────────────────────────

    [Fact]
    public void WorkMessage_ContainsFormattedAmount()
    {
        decimal earned = 42_000m;
        string result = CreditHelper.WorkMessage(earned);
        Assert.Contains(CreditHelper.Format(earned), result);
    }

    [Fact]
    public void WorkMessage_ReturnsNonEmptyString()
    {
        Assert.False(string.IsNullOrWhiteSpace(CreditHelper.WorkMessage(5_000m)));
    }

    [Fact]
    public void WorkMessage_VariousAmounts_AlwaysContainsEmoji()
    {
        foreach (decimal amount in new[] { 5_000m, 25_000m, 75_000m })
            Assert.Contains(CreditHelper.CurrencyEmoji, CreditHelper.WorkMessage(amount));
    }

    // ── CastLine ─────────────────────────────────────────────────────────────

    [Fact]
    public void CastLine_ReturnsNonEmptyName()
    {
        var (name, _, _, _) = CreditHelper.CastLine();
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void CastLine_ReturnsNonEmptyEmoji()
    {
        var (_, emoji, _, _) = CreditHelper.CastLine();
        Assert.False(string.IsNullOrWhiteSpace(emoji));
    }

    [Fact]
    public void CastLine_CreditsNonNegative()
    {
        for (int i = 0; i < 200; i++)
        {
            var (_, _, credits, _) = CreditHelper.CastLine();
            Assert.True(credits >= 0, $"Negative credits: {credits}");
        }
    }

    [Fact]
    public void CastLine_ReturnsNameFromFishTable()
    {
        var validNames = CreditHelper.FishTable.Select(f => f.name).ToHashSet();
        for (int i = 0; i < 100; i++)
        {
            var (name, _, _, _) = CreditHelper.CastLine();
            Assert.Contains(name, validNames);
        }
    }

    // ── FishTable integrity ───────────────────────────────────────────────────

    [Fact]
    public void FishTable_AllEntriesHaveNonEmptyName()
    {
        Assert.All(CreditHelper.FishTable, f => Assert.False(string.IsNullOrWhiteSpace(f.name)));
    }

    [Fact]
    public void FishTable_AllEntriesHaveNonEmptyEmoji()
    {
        Assert.All(CreditHelper.FishTable, f => Assert.False(string.IsNullOrWhiteSpace(f.emoji)));
    }

    [Fact]
    public void FishTable_AllEntriesHavePositiveWeight()
    {
        Assert.All(CreditHelper.FishTable, f => Assert.True(f.weight > 0));
    }

    [Fact]
    public void FishTable_JunkItems_HaveZeroMaxCredits()
    {
        // Old Boot, Seaweed, Tin Can are junk: max = 0
        var junk = CreditHelper.FishTable.Where(f => f.max == 0).ToArray();
        Assert.NotEmpty(junk);
        Assert.All(junk, f => Assert.Equal(0, f.min));
    }

    [Fact]
    public void FishTable_NonJunkItems_HavePositiveMin()
    {
        Assert.All(
            CreditHelper.FishTable.Where(f => f.max > 0),
            f => Assert.True(f.min > 0));
    }

    [Fact]
    public void FishTable_LegendaryCarp_HasHighestMaxCredits()
    {
        decimal globalMax = CreditHelper.FishTable.Max(f => f.max);
        var legendary = CreditHelper.FishTable.First(f => f.name == "Legendary Carp");
        Assert.Equal(globalMax, legendary.max);
    }

    // ── RollInvestment ────────────────────────────────────────────────────────

    [Fact]
    public void RollInvestment_MultiplierIsPositive()
    {
        for (int i = 0; i < 200; i++)
        {
            var (mult, _) = CreditHelper.RollInvestment();
            Assert.True(mult > 0, $"Non-positive multiplier: {mult}");
        }
    }

    [Fact]
    public void RollInvestment_LabelIsNonEmpty()
    {
        for (int i = 0; i < 50; i++)
        {
            var (_, label) = CreditHelper.RollInvestment();
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    [Fact]
    public void RollInvestment_MultiplierComesFromInvestOutcomes()
    {
        var validMultipliers = CreditHelper.InvestOutcomes.Select(o => o.multiplier).ToHashSet();
        for (int i = 0; i < 200; i++)
        {
            var (mult, _) = CreditHelper.RollInvestment();
            Assert.Contains(mult, validMultipliers);
        }
    }

    // ── InvestOutcomes integrity ──────────────────────────────────────────────

    [Fact]
    public void InvestOutcomes_AllLabelsNonEmpty()
    {
        Assert.All(CreditHelper.InvestOutcomes, o => Assert.False(string.IsNullOrWhiteSpace(o.label)));
    }

    [Fact]
    public void InvestOutcomes_AllWeightsPositive()
    {
        Assert.All(CreditHelper.InvestOutcomes, o => Assert.True(o.weight > 0));
    }

    [Fact]
    public void InvestOutcomes_AllMultipliersPositive()
    {
        Assert.All(CreditHelper.InvestOutcomes, o => Assert.True(o.multiplier > 0));
    }

    [Fact]
    public void InvestOutcomes_HasBreakEvenEntry()
    {
        Assert.Single(CreditHelper.InvestOutcomes, o => o.multiplier == 1.00m);
    }

    [Fact]
    public void InvestOutcomes_HasJackpotEntry()
    {
        Assert.Single(CreditHelper.InvestOutcomes, o => o.label == "Jackpot investment!");
    }

    // ── SpinWheel / SpinWheelChaos ────────────────────────────────────────────

    [Fact]
    public void SpinWheel_ReturnsValidIndex()
    {
        for (int i = 0; i < 200; i++)
        {
            int idx = CreditHelper.SpinWheel();
            Assert.InRange(idx, 0, CreditHelper.WheelSegments.Length - 1);
        }
    }

    [Fact]
    public void SpinWheelChaos_ReturnsValidIndex()
    {
        for (int i = 0; i < 200; i++)
        {
            int idx = CreditHelper.SpinWheelChaos();
            Assert.InRange(idx, 0, CreditHelper.WheelSegments.Length - 1);
        }
    }

    // ── BuildWheelDisplay ─────────────────────────────────────────────────────

    [Fact]
    public void BuildWheelDisplay_ContainsCodeBlock()
    {
        string display = CreditHelper.BuildWheelDisplay(0);
        Assert.Contains("```", display);
    }

    [Fact]
    public void BuildWheelDisplay_ContainsSelectedLabel()
    {
        for (int i = 0; i < CreditHelper.WheelSegments.Length; i++)
        {
            string display = CreditHelper.BuildWheelDisplay(i);
            Assert.Contains(CreditHelper.WheelSegments[i].label, display);
        }
    }

    [Fact]
    public void BuildWheelDisplay_ContainsPointerSymbols()
    {
        string display = CreditHelper.BuildWheelDisplay(0);
        Assert.Contains("▶", display);
        Assert.Contains("◀", display);
    }

    [Fact]
    public void BuildWheelDisplay_NonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(CreditHelper.BuildWheelDisplay(0)));
    }

    // ── WheelSegments integrity ───────────────────────────────────────────────

    [Fact]
    public void WheelSegments_AllLabelsNonEmpty()
    {
        Assert.All(CreditHelper.WheelSegments, s => Assert.False(string.IsNullOrWhiteSpace(s.label)));
    }

    [Fact]
    public void WheelSegments_AllEmojisNonEmpty()
    {
        Assert.All(CreditHelper.WheelSegments, s => Assert.False(string.IsNullOrWhiteSpace(s.emoji)));
    }

    [Fact]
    public void WheelSegments_AllWeightsPositive()
    {
        Assert.All(CreditHelper.WheelSegments, s => Assert.True(s.weight > 0));
    }

    [Fact]
    public void WheelSegments_BankruptMultiplierIsZero()
    {
        var bankrupt = CreditHelper.WheelSegments.First(s => s.label == "BANKRUPT");
        Assert.Equal(0.0, bankrupt.multiplier);
    }

    [Fact]
    public void WheelSegments_HighestMultiplierIs100x()
    {
        double max = CreditHelper.WheelSegments.Max(s => s.multiplier);
        Assert.Equal(100.0, max);
    }

    // ── ScratchCard ───────────────────────────────────────────────────────────

    [Fact]
    public void ScratchCard_PayoutNonNegative()
    {
        for (int i = 0; i < 200; i++)
        {
            var (_, _, _, payout, _) = CreditHelper.ScratchCard(CreditHelper.ScratchCardCost);
            Assert.True(payout >= 0, $"Negative payout: {payout}");
        }
    }

    [Fact]
    public void ScratchCard_LabelNonEmpty()
    {
        for (int i = 0; i < 50; i++)
        {
            var (_, _, _, _, label) = CreditHelper.ScratchCard(CreditHelper.ScratchCardCost);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    [Fact]
    public void ScratchCard_SymbolsNonEmpty()
    {
        var (s1, s2, s3, _, _) = CreditHelper.ScratchCard(CreditHelper.ScratchCardCost);
        Assert.False(string.IsNullOrWhiteSpace(s1));
        Assert.False(string.IsNullOrWhiteSpace(s2));
        Assert.False(string.IsNullOrWhiteSpace(s3));
    }

    // ── ScratchCardChaos ──────────────────────────────────────────────────────

    [Fact]
    public void ScratchCardChaos_PayoutNonNegative()
    {
        for (int i = 0; i < 200; i++)
        {
            var (_, _, _, payout, _) = CreditHelper.ScratchCardChaos(CreditHelper.ScratchCardCost);
            Assert.True(payout >= 0, $"Negative payout: {payout}");
        }
    }

    [Fact]
    public void ScratchCardChaos_LabelNonEmpty()
    {
        for (int i = 0; i < 50; i++)
        {
            var (_, _, _, _, label) = CreditHelper.ScratchCardChaos(CreditHelper.ScratchCardCost);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    // ── ScratchPrizes integrity ───────────────────────────────────────────────

    [Fact]
    public void ScratchPrizes_AllHaveThreeSymbols()
    {
        Assert.All(CreditHelper.ScratchPrizes, p => Assert.Equal(3, p.symbols.Length));
    }

    [Fact]
    public void ScratchPrizes_AllLabelsNonEmpty()
    {
        Assert.All(CreditHelper.ScratchPrizes, p => Assert.False(string.IsNullOrWhiteSpace(p.label)));
    }

    [Fact]
    public void ScratchPrizes_AllMultipliersPositive()
    {
        Assert.All(CreditHelper.ScratchPrizes, p => Assert.True(p.multiplier > 0));
    }

    [Fact]
    public void ScratchPrizes_JackpotIsHighestMultiplier()
    {
        decimal max = CreditHelper.ScratchPrizes.Max(p => p.multiplier);
        var jackpot = CreditHelper.ScratchPrizes.First(p => p.label == "JACKPOT");
        Assert.Equal(max, jackpot.multiplier);
    }

    // ── SlotSymbols integrity ─────────────────────────────────────────────────

    [Fact]
    public void SlotSymbols_AllSymbolsNonEmpty()
    {
        Assert.All(CreditHelper.SlotSymbols, s => Assert.False(string.IsNullOrWhiteSpace(s.symbol)));
    }

    [Fact]
    public void SlotSymbols_AllNamesNonEmpty()
    {
        Assert.All(CreditHelper.SlotSymbols, s => Assert.False(string.IsNullOrWhiteSpace(s.name)));
    }

    [Fact]
    public void SlotSymbols_AllWeightsPositive()
    {
        Assert.All(CreditHelper.SlotSymbols, s => Assert.True(s.weight > 0));
    }

    [Fact]
    public void SlotSymbols_DiamondHasHighestMultiplier()
    {
        double max = CreditHelper.SlotSymbols.Max(s => s.multiplier);
        var diamond = CreditHelper.SlotSymbols.First(s => s.name == "Diamond");
        Assert.Equal(max, diamond.multiplier);
    }

    [Fact]
    public void SlotSymbols_LemonHasZeroMultiplier()
    {
        var lemon = CreditHelper.SlotSymbols.First(s => s.name == "Lemon");
        Assert.Equal(0.0, lemon.multiplier);
    }

    // ── SpinReel / SpinReelRandom ─────────────────────────────────────────────

    [Fact]
    public void SpinReel_ReturnsValidSymbol()
    {
        var validSymbols = CreditHelper.SlotSymbols.Select(s => s.symbol).ToHashSet();
        for (int i = 0; i < 200; i++)
            Assert.Contains(CreditHelper.SpinReel(), validSymbols);
    }

    [Fact]
    public void SpinReelRandom_ReturnsValidSymbol()
    {
        var validSymbols = CreditHelper.SlotSpinSymbols.ToHashSet();
        for (int i = 0; i < 200; i++)
            Assert.Contains(CreditHelper.SpinReelRandom(), validSymbols);
    }

    // ── Horses integrity ──────────────────────────────────────────────────────

    [Fact]
    public void Horses_HasEightEntries()
    {
        Assert.Equal(8, CreditHelper.Horses.Length);
    }

    [Fact]
    public void Horses_AllNamesNonEmpty()
    {
        Assert.All(CreditHelper.Horses, h => Assert.False(string.IsNullOrWhiteSpace(h.name)));
    }

    [Fact]
    public void Horses_AllEmojisNonEmpty()
    {
        Assert.All(CreditHelper.Horses, h => Assert.False(string.IsNullOrWhiteSpace(h.emoji)));
    }

    [Fact]
    public void Horses_AllWeightsPositive()
    {
        Assert.All(CreditHelper.Horses, h => Assert.True(h.weight > 0));
    }

    [Fact]
    public void Horses_OddsDescendingFromFavourite()
    {
        // Favourite (index 0) has lowest odds (easiest win), underdog has highest
        Assert.True(CreditHelper.Horses[0].odds < CreditHelper.Horses[^1].odds);
    }

    [Fact]
    public void Horses_MiracleRun_HasHighestOdds()
    {
        double maxOdds = CreditHelper.Horses.Max(h => h.odds);
        var miracle = CreditHelper.Horses.First(h => h.name == "Miracle Run");
        Assert.Equal(maxOdds, miracle.odds);
    }

    // ── CardRanks / CardSuits integrity ───────────────────────────────────────

    [Fact]
    public void CardRanks_Has13Entries()
    {
        Assert.Equal(13, CreditHelper.CardRanks.Length);
    }

    [Fact]
    public void CardSuits_Has4Entries()
    {
        Assert.Equal(4, CreditHelper.CardSuits.Length);
    }

    [Fact]
    public void CardRanks_ContainsAce()
    {
        Assert.Contains("A", CreditHelper.CardRanks);
    }

    [Fact]
    public void CardRanks_ContainsFaceCards()
    {
        Assert.Contains("J", CreditHelper.CardRanks);
        Assert.Contains("Q", CreditHelper.CardRanks);
        Assert.Contains("K", CreditHelper.CardRanks);
    }

    [Fact]
    public void CardSuits_ContainsAllFourSuits()
    {
        Assert.Contains("♠", CreditHelper.CardSuits);
        Assert.Contains("♥", CreditHelper.CardSuits);
        Assert.Contains("♦", CreditHelper.CardSuits);
        Assert.Contains("♣", CreditHelper.CardSuits);
    }

}
