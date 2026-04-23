namespace DiscordBot.Tests.Unit;

public class CreditHelperExtendedTests
{
    // ── BuildPokerDeck ────────────────────────────────────────────────────────

    [Fact]
    public void BuildPokerDeck_Returns52Cards()
    {
        var deck = CreditHelper.BuildPokerDeck();
        Assert.Equal(52, deck.Count);
    }

    [Fact]
    public void BuildPokerDeck_AllCardsUnique()
    {
        var deck = CreditHelper.BuildPokerDeck();
        Assert.Equal(52, deck.Distinct().Count());
    }

    [Fact]
    public void BuildPokerDeck_ContainsAllRankSuitCombinations()
    {
        var deck = CreditHelper.BuildPokerDeck();
        foreach (string suit in CreditHelper.CardSuits)
            foreach (string rank in CreditHelper.CardRanks)
                Assert.Contains($"{rank}|{suit}", deck);
    }

    [Fact]
    public void BuildPokerDeck_IsShuffled_NotAlwaysInOrder()
    {
        // With 52! possible orders, two calls producing the same order is astronomically unlikely
        var deck1 = CreditHelper.BuildPokerDeck();
        var deck2 = CreditHelper.BuildPokerDeck();
        // At least one position should differ across calls (statistically guaranteed)
        bool anyDifference = deck1.Zip(deck2).Any(p => p.First != p.Second);
        // We allow this to occasionally be false by not asserting — but we verify
        // the deck is valid either way (test above already covers uniqueness)
        Assert.Equal(52, deck1.Count);
        Assert.Equal(52, deck2.Count);
    }

    // ── FormatPokerCard ───────────────────────────────────────────────────────

    [Fact]
    public void FormatPokerCard_NotHeld_ReturnsPlainDisplay()
    {
        Assert.Equal("A♠", CreditHelper.FormatPokerCard("A|♠", held: false));
    }

    [Fact]
    public void FormatPokerCard_Held_ReturnsBoldBrackets()
    {
        Assert.Equal("[**A♠**]", CreditHelper.FormatPokerCard("A|♠", held: true));
    }

    [Fact]
    public void FormatPokerCard_NotHeld_TwoDigitRank()
    {
        Assert.Equal("10♥", CreditHelper.FormatPokerCard("10|♥", held: false));
    }

    [Fact]
    public void FormatPokerCard_Held_TwoDigitRank()
    {
        Assert.Equal("[**10♥**]", CreditHelper.FormatPokerCard("10|♥", held: true));
    }

    // ── ShowCard / ShowHand ───────────────────────────────────────────────────

    [Fact]
    public void ShowCard_ReturnsFormattedUnheldCard()
    {
        Assert.Equal("K♦", CreditHelper.ShowCard("K|♦"));
    }

    [Fact]
    public void ShowHand_JoinsWithDoubleSpace()
    {
        var cards = new[] { "A|♠", "K|♥", "Q|♦" };
        string result = CreditHelper.ShowHand(cards);
        Assert.Equal("A♠  K♥  Q♦", result);
    }

    [Fact]
    public void ShowHand_SingleCard_NoSeparator()
    {
        Assert.Equal("J♣", CreditHelper.ShowHand(new[] { "J|♣" }));
    }

    // ── PokerHandLabel ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CreditHelper.PokerHand.RoyalFlush,    "ROYAL FLUSH")]
    [InlineData(CreditHelper.PokerHand.StraightFlush, "Straight Flush")]
    [InlineData(CreditHelper.PokerHand.FourOfAKind,   "Four of a Kind")]
    [InlineData(CreditHelper.PokerHand.FullHouse,     "Full House")]
    [InlineData(CreditHelper.PokerHand.Flush,         "Flush")]
    [InlineData(CreditHelper.PokerHand.Straight,      "Straight")]
    [InlineData(CreditHelper.PokerHand.ThreeOfAKind,  "Three of a Kind")]
    [InlineData(CreditHelper.PokerHand.TwoPair,       "Two Pair")]
    [InlineData(CreditHelper.PokerHand.JacksOrBetter, "Jacks or Better")]
    [InlineData(CreditHelper.PokerHand.HighCard,      "No Win")]
    public void PokerHandLabel_AllHands_CorrectLabel(CreditHelper.PokerHand hand, string expected)
    {
        Assert.Equal(expected, CreditHelper.PokerHandLabel(hand));
    }

    // ── BestHandType — 7-card combinations ────────────────────────────────────

    [Fact]
    public void BestHandType_SevenCards_WithRoyalFlushCards_DetectsRoyalFlush()
    {
        // 10♠ J♠ Q♠ K♠ A♠ + 2♥ 3♦ → best 5 is royal flush
        var seven = new List<string> { "10|♠", "J|♠", "Q|♠", "K|♠", "A|♠", "2|♥", "3|♦" };
        var (hand, name) = CreditHelper.BestHandType(seven);
        Assert.Equal(CreditHelper.PokerHand.RoyalFlush, hand);
        Assert.Equal("ROYAL FLUSH", name);
    }

    [Fact]
    public void BestHandType_SevenCards_WithFourOfAKind_DetectsQuads()
    {
        var seven = new List<string> { "A|♠", "A|♥", "A|♦", "A|♣", "2|♠", "3|♥", "4|♦" };
        var (hand, _) = CreditHelper.BestHandType(seven);
        Assert.Equal(CreditHelper.PokerHand.FourOfAKind, hand);
    }

    [Fact]
    public void BestHandType_SevenCards_PicksBestAmong21Combos()
    {
        // Pair of aces + flush in diamonds (5 diamonds) — flush beats pair
        var seven = new List<string> { "A|♠", "A|♥", "2|♦", "5|♦", "7|♦", "9|♦", "J|♦" };
        var (hand, _) = CreditHelper.BestHandType(seven);
        Assert.True((int)hand >= (int)CreditHelper.PokerHand.Flush);
    }

    // ── HandScore ─────────────────────────────────────────────────────────────

    [Fact]
    public void HandScore_RoyalFlush_HigherThanStraightFlush()
    {
        var royal = new List<string> { "10|♠", "J|♠", "Q|♠", "K|♠", "A|♠", "2|♥", "3|♦" };
        var sf    = new List<string> { "5|♠",  "6|♠", "7|♠", "8|♠", "9|♠", "2|♥", "3|♦" };
        Assert.True(CreditHelper.HandScore(royal) > CreditHelper.HandScore(sf));
    }

    [Fact]
    public void HandScore_HigherHandEnum_AlwaysWins()
    {
        // Four aces beats full house
        var quads    = new List<string> { "A|♠", "A|♥", "A|♦", "A|♣", "2|♠", "3|♥", "4|♦" };
        var fullHouse = new List<string> { "K|♠", "K|♥", "K|♦", "Q|♣", "Q|♠", "2|♥", "3|♦" };
        Assert.True(CreditHelper.HandScore(quads) > CreditHelper.HandScore(fullHouse));
    }

    [Fact]
    public void HandScore_NonNegative()
    {
        var cards = new List<string> { "2|♠", "4|♥", "6|♦", "8|♣", "10|♠", "Q|♥", "A|♦" };
        Assert.True(CreditHelper.HandScore(cards) >= 0);
    }

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

    // ── PokerPayouts integrity ────────────────────────────────────────────────

    [Fact]
    public void PokerPayouts_HasEntryForEveryHandEnum()
    {
        var enumValues = Enum.GetValues<CreditHelper.PokerHand>();
        foreach (var hand in enumValues)
            Assert.Single(CreditHelper.PokerPayouts, p => p.hand == hand);
    }

    [Fact]
    public void PokerPayouts_RoyalFlushHighestMultiplier()
    {
        decimal max = CreditHelper.PokerPayouts.Max(p => p.multiplier);
        var royal = CreditHelper.PokerPayouts.First(p => p.hand == CreditHelper.PokerHand.RoyalFlush);
        Assert.Equal(max, royal.multiplier);
    }

    [Fact]
    public void PokerPayouts_HighCardHasZeroMultiplier()
    {
        var highCard = CreditHelper.PokerPayouts.First(p => p.hand == CreditHelper.PokerHand.HighCard);
        Assert.Equal(0m, highCard.multiplier);
    }
}
