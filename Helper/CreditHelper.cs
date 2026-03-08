namespace DiscordBot.Helper;

/// <summary>
/// Static helpers for the credits economy system.
/// Handles formatting, payout calculations, and earning rates.
/// </summary>
public static class CreditHelper
{

    public const string CurrencyEmoji = "⚡";
    public const string CurrencyName = "Credits";

    /// <summary>Formats a credit value with emoji, e.g. "⚡ 1,250"</summary>
    public static string Format(long amount) =>
        $"{CurrencyEmoji} **{amount:N0}**";

    /// <summary>Formats a signed delta, e.g. "+⚡ 500" or "-⚡ 200"</summary>
    public static string FormatDelta(long delta) =>
        delta >= 0 ? $"+{CurrencyEmoji} {delta:N0}" : $"-{CurrencyEmoji} {Math.Abs(delta):N0}";


    public const long DailyAmount = 5000;
    public const int DailyCooldownHours = 24;
    public const int WorkCooldownMinutes = 60;
    public const long WorkMin = 50;
    public const long WorkMax = 5000;
    public const long PassiveMessageAmount = 5;
    public const long PuzzleSolveAmount = 10000;

    /// <summary>Credits awarded on pet level-up: 50 × new level.</summary>
    public static long PetLevelUpAmount(int newLevel) => 50 * newLevel;


    public const long MinBet = 10;
    public const long MaxBet = 100000000_000;
    public const long DailyLossLimit = 100000000_000;  // max credits losable per 24h

    public static bool IsValidBet(long bet, long balance, out string error)
    {
        if (bet < MinBet) { error = $"Minimum bet is {Format(MinBet)}."; return false; }
        if (bet > MaxBet) { error = $"Maximum bet is {Format(MaxBet)}."; return false; }
        if (bet > balance) { error = $"You don't have enough credits! Balance: {Format(balance)}."; return false; }
        error = "";
        return true;
    }


    /// <summary>Random symbols for the spinning animation frames (not weighted — pure visual).</summary>
    public static readonly string[] SlotSpinSymbols = ["💎", "7️⃣", "🍀", "⭐", "🔔", "🍇", "🍊", "🍋", "🍒"];

    public static string SpinReelRandom() =>
        SlotSpinSymbols[Random.Shared.Next(SlotSpinSymbols.Length)];

    public static readonly (string symbol, string name, int weight, double multiplier)[] SlotSymbols =
    [
        ("💎", "Diamond",    1,  50.0),
        ("7️⃣",  "Lucky 7",   3,  20.0),
        ("🍀", "Clover",     5,  10.0),
        ("⭐", "Star",       8,   5.0),
        ("🔔", "Bell",      12,   3.0),
        ("🍇", "Grapes",    15,   2.0),
        ("🍊", "Orange",    18,   1.5),
        ("🍋", "Lemon",     22,   0.0),  // no payout — most common
        ("🍒", "Cherry",    16,   1.0),  // any cherry = small win
    ];

    public static string SpinReel()
    {
        int totalWeight = SlotSymbols.Sum(s => s.weight);
        int roll = Random.Shared.Next(totalWeight);
        int cumulative = 0;

        foreach (var (symbol, _, weight, _) in SlotSymbols)
        {
            cumulative += weight;
            if (roll < cumulative) return symbol;
        }

        return SlotSymbols[^1].symbol;
    }

    public static (long payout, string result) CalculateSlotPayout(
        string r1, string r2, string r3, long bet)
    {
        // Three of a kind
        if (r1 == r2 && r2 == r3)
        {
            var sym = SlotSymbols.FirstOrDefault(s => s.symbol == r1);
            long payout = (long)(bet * sym.multiplier);
            return (payout, $"🎰 **Three {sym.name}s!** {FormatDelta(payout)}");
        }

        // Two of a kind (partial win = 0.5× bet back)
        if (r1 == r2 || r2 == r3 || r1 == r3)
        {
            long payout = bet / 2;
            return (payout, $"Almost! Two matching. {FormatDelta(payout)}");
        }

        // Any cherry = small consolation
        if (r1 == "🍒" || r2 == "🍒" || r3 == "🍒")
        {
            long payout = bet / 4;
            return (payout, $"🍒 Cherry consolation prize! {FormatDelta(payout)}");
        }

        return (0, $"No match. {FormatDelta(-bet)}");
    }


    public static readonly string[] RedNumbers = ["1", "3", "5", "7", "9", "12", "14", "16", "18", "19", "21", "23", "25", "27", "30", "32", "34", "36"];
    public static readonly string[] BlackNumbers = ["2", "4", "6", "8", "10", "11", "13", "15", "17", "20", "22", "24", "26", "28", "29", "31", "33", "35"];

    public static int SpinRoulette() => Random.Shared.Next(0, 37); // 0–36

    public static (long payout, string result) CalculateRoulettePayout(
        int spin, string bet, long amount)
    {
        string spinStr = spin.ToString();
        bool isRed = RedNumbers.Contains(spinStr);
        bool isBlack = BlackNumbers.Contains(spinStr);
        bool isEven = spin > 0 && spin % 2 == 0;
        bool isOdd = spin > 0 && spin % 2 != 0;
        bool isLow = spin is >= 1 and <= 18;
        bool isHigh = spin is >= 19 and <= 36;
        string spinDisplay = spin == 0 ? "🟢 0" : isRed ? $"🔴 {spin}" : $"⚫ {spin}";

        return bet.ToLower() switch
        {
            "red" => isRed ? ((long)(amount * 1.9), $"{spinDisplay} — Red wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — Red loses. {FormatDelta(-amount)}"),
            "black" => isBlack ? ((long)(amount * 1.9), $"{spinDisplay} — Black wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — Black loses. {FormatDelta(-amount)}"),
            "even" => isEven ? ((long)(amount * 1.9), $"{spinDisplay} — Even wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — Even loses. {FormatDelta(-amount)}"),
            "odd" => isOdd ? ((long)(amount * 1.9), $"{spinDisplay} — Odd wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — Odd loses. {FormatDelta(-amount)}"),
            "low" => isLow ? ((long)(amount * 1.9), $"{spinDisplay} — 1-18 wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — 1-18 loses. {FormatDelta(-amount)}"),
            "high" => isHigh ? ((long)(amount * 1.9), $"{spinDisplay} — 19-36 wins! {FormatDelta((long)(amount * 1.9))}")
                               : (0, $"{spinDisplay} — 19-36 loses. {FormatDelta(-amount)}"),
            _ when int.TryParse(bet, out int num) && num == spin
                    => (amount * 35, $"{spinDisplay} — Exact number wins! {FormatDelta(amount * 35)}"),
            _ when int.TryParse(bet, out _)
                    => (0, $"{spinDisplay} — Wrong number. {FormatDelta(-amount)}"),
            _ => (0, $"{spinDisplay} — Invalid bet. {FormatDelta(-amount)}")
        };
    }


    public static readonly (string name, string emoji, int weight, double odds)[] Horses =
    [
        ("Thunderbolt",  "🐎", 30, 2.0),   // favourite
        ("Silver Wind",  "🏇", 25, 2.5),
        ("Crimson Dawn", "🦄", 20, 3.5),
        ("Dark Matter",  "🐴", 15, 5.0),
        ("Lucky Star",   "⭐", 10, 8.0),   // longshot
    ];

    public static int RunRace()
    {
        int total = Horses.Sum(h => h.weight);
        int roll = Random.Shared.Next(total);
        int cum = 0;
        for (int i = 0; i < Horses.Length; i++)
        {
            cum += Horses[i].weight;
            if (roll < cum) return i;
        }
        return Horses.Length - 1;
    }

    /// <summary>Returns a finishing-order array with the winner in position 0.</summary>
    public static int[] BuildRaceResult(int winnerIndex)
    {
        var positions = Enumerable.Range(0, Horses.Length)
                                  .OrderBy(_ => Random.Shared.Next())
                                  .ToArray();
        int wPos = Array.IndexOf(positions, winnerIndex);
        (positions[0], positions[wPos]) = (positions[wPos], positions[0]);
        return positions;
    }


    public const long ScratchCardCost = 50;

    public static readonly (string[] symbols, long multiplier, string label)[] ScratchPrizes =
    [
        (["💎","💎","💎"], 100, "JACKPOT"),
        (["7️⃣","7️⃣","7️⃣"],  50, "Triple 7s"),
        (["⭐","⭐","⭐"],  20, "Triple Stars"),
        (["🔔","🔔","🔔"],  10, "Triple Bells"),
        (["🍀","🍀","🍀"],   5, "Triple Clovers"),
        (["💰","💰","💰"],   3, "Triple Coins"),
        (["🎁","🎁","🎁"],   2, "Triple Gifts"),
    ];

    private static readonly string[] ScratchPool =
        ["💎", "7️⃣", "⭐", "🔔", "🍀", "💰", "🎁", "❌", "❌", "❌", "❌", "❌"];

    public static (string s1, string s2, string s3, long payout, string label) ScratchCard(long cost)
    {
        // Small chance to award a prize
        int roll = Random.Shared.Next(100);

        if (roll < 2) return (ScratchPrizes[0].symbols[0], ScratchPrizes[0].symbols[1], ScratchPrizes[0].symbols[2], cost * ScratchPrizes[0].multiplier, ScratchPrizes[0].label);
        if (roll < 5) return (ScratchPrizes[1].symbols[0], ScratchPrizes[1].symbols[1], ScratchPrizes[1].symbols[2], cost * ScratchPrizes[1].multiplier, ScratchPrizes[1].label);
        if (roll < 10) return (ScratchPrizes[2].symbols[0], ScratchPrizes[2].symbols[1], ScratchPrizes[2].symbols[2], cost * ScratchPrizes[2].multiplier, ScratchPrizes[2].label);
        if (roll < 18) return (ScratchPrizes[3].symbols[0], ScratchPrizes[3].symbols[1], ScratchPrizes[3].symbols[2], cost * ScratchPrizes[3].multiplier, ScratchPrizes[3].label);
        if (roll < 28) return (ScratchPrizes[4].symbols[0], ScratchPrizes[4].symbols[1], ScratchPrizes[4].symbols[2], cost * ScratchPrizes[4].multiplier, ScratchPrizes[4].label);
        if (roll < 40) return (ScratchPrizes[5].symbols[0], ScratchPrizes[5].symbols[1], ScratchPrizes[5].symbols[2], cost * ScratchPrizes[5].multiplier, ScratchPrizes[5].label);
        if (roll < 52) return (ScratchPrizes[6].symbols[0], ScratchPrizes[6].symbols[1], ScratchPrizes[6].symbols[2], cost * ScratchPrizes[6].multiplier, ScratchPrizes[6].label);

        // No win — random non-matching symbols from distinct pool
        var distinct = ScratchPool.Distinct().OrderBy(_ => Random.Shared.Next()).Take(3).ToArray();
        return (distinct[0], distinct[1], distinct[2], 0, "No match");
    }


    /// <summary>
    /// Payout for dice. pick values: "over","under","seven","doubles".
    /// </summary>
    public static long DicePayout(string pick, int d1, int d2, long bet)
    {
        int total = d1 + d2;
        bool won = pick switch
        {
            "over" => total > 7,
            "under" => total < 7,
            "seven" => total == 7,
            "doubles" => d1 == d2,
            _ => false
        };
        if (!won) return 0;
        return pick switch
        {
            "seven" => bet * 4,
            "doubles" => (long)(bet * 6.0),
            _ => (long)(bet * 1.8)
        };
    }


    public static readonly string[] CardRanks = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"];
    public static readonly string[] CardSuits = ["♠", "♥", "♦", "♣"];

    public static (string display, int value) DrawCard()
    {
        int rankIdx = Random.Shared.Next(CardRanks.Length);
        string suit = CardSuits[Random.Shared.Next(CardSuits.Length)];
        return ($"{CardRanks[rankIdx]}{suit}", rankIdx); // value = index (0=2 ... 12=A)
    }


    public static string WorkMessage(long earned) => Random.Shared.Next(10) switch
    {
        0 => $"You fixed a production bug at 2am. Compensation: {Format(earned)}",
        1 => $"You walked someone's dog and found a fiver in your pocket. {Format(earned)}",
        2 => $"You sold a cursed item on eBay. {Format(earned)}",
        3 => $"You completed a survey for twelve minutes. {Format(earned)}",
        4 => $"You helped a neighbour move a couch upstairs. {Format(earned)}",
        5 => $"You won a hotdog eating contest. {Format(earned)}",
        6 => $"You streamed for six hours to an audience of three. {Format(earned)}",
        7 => $"You found coins behind the sofa cushions. {Format(earned)}",
        8 => $"You did some freelance graphic design. {Format(earned)}",
        _ => $"You clocked in, drank coffee, and called it a day. {Format(earned)}"
    };


    public const int FishCooldownMinutes = 45;

    public static readonly (string name, string emoji, long min, long max, int weight, string flavour)[] FishTable =
    [
        // Junk
        ("Old Boot",        "👢",  0,    0,    10, "You reeled in an old boot. The lake is not impressed with you."),
        ("Seaweed",         "🌿",  0,    0,    9,  "A soggy clump of seaweed. The fish are laughing."),
        ("Tin Can",         "🥫",  0,    0,    6,  "Someone else's problem is now your problem."),
        // Common
        ("Minnow",          "🐟",  10,   40,   20, "A tiny minnow. Technically a fish."),
        ("Perch",           "🐠",  30,   70,   18, "A solid perch. Dinner is sorted."),
        ("Bass",            "🎣",  55,   110,  15, "A decent bass. The rod barely bent."),
        // Uncommon
        ("Trout",           "🐟",  90,   160,  10, "A plump trout. The river was generous today."),
        ("Salmon",          "🍣",  130,  220,  8,  "A beautiful salmon leapt straight into the net."),
        ("Carp",            "🐡",  110,  190,  8,  "A hefty carp. It put up a real fight."),
        // Rare
        ("Swordfish",       "⚔️",   220,  420,  5,  "A swordfish! Your arms are still trembling."),
        ("Giant Tuna",      "🐟",  320,  620,  3,  "A giant tuna! The rod nearly snapped clean in half."),
        ("Golden Koi",      "🏅",  550,  1050, 2,  "A golden koi! It practically glows in your hands."),
        // Legendary
        ("Legendary Carp",  "👑",  1100, 2600, 1,  "A LEGENDARY CARP. Witnesses gather. Someone starts clapping."),
    ];

    public static (string name, string emoji, long credits, string flavour) CastLine()
    {
        int total = FishTable.Sum(f => f.weight);
        int roll = Random.Shared.Next(total);
        int cum = 0;

        foreach (var (name, emoji, min, max, weight, flavour) in FishTable)
        {
            cum += weight;
            if (roll < cum)
            {
                long credits = max > 0 ? Random.Shared.NextInt64(min, max + 1) : 0;
                return (name, emoji, credits, flavour);
            }
        }

        var last = FishTable[^1];
        return (last.name, last.emoji, last.max, last.flavour);
    }


    public static readonly (string label, double multiplier, int weight, string emoji)[] WheelSegments =
    [
        ("BANKRUPT", 0.0, 9, "💀"),      // loss
        ("0.25×",    0.25, 18, "💸"),     // loss
        ("0.5×",     0.5, 21, "😬"),      // loss
        ("0.75×",    0.75, 17, "😕"),     // loss
        ("1×",       1.0, 25, "😐"),      // push
        ("1.5×",     1.5, 9, "🙂"),       // win
        ("2×",       2.0, 8, "😊"),       // win
        ("3×",       3.0, 6, "😁"),       // win
        ("5×",       5.0, 5, "🤩"),       // win
        ("10×",     10.0, 6, "🔥"),       // win
        ("25×",     25.0, 2, "💎"),       // win
        ("50×",     50.0, 2, "👑"),       // win
        ("100×",   100.0, 1, "🚀")        // win
    ];

    public static int SpinWheel()
    {
        int total = WheelSegments.Sum(s => s.weight);
        int roll = Random.Shared.Next(total);
        int cum = 0;
        for (int i = 0; i < WheelSegments.Length; i++)
        {
            cum += WheelSegments[i].weight;
            if (roll < cum) return i;
        }
        return WheelSegments.Length - 1;
    }

    /// <summary>
    /// Renders the Big Wheel as a horizontal wheel-of-fortune display.
    ///
    /// Layout (viewed from front — fixed pointer, segments scroll left→right):
    ///
    ///   top arc  : segments on the back of the wheel (half-rotation away)
    ///   ──────── : wheel rim
    ///   rim band : 3 context segments · ▶ SELECTED ◀ · 3 context segments
    ///   ──────── : wheel rim
    ///   bot arc  : more back-of-wheel segments (opposite side)
    ///
    /// Each frame the centreIndex advances, so all three rows shift together —
    /// giving the impression of a circular wheel rotating as a unit.
    /// </summary>
    public static string BuildWheelDisplay(int centreIndex)
    {
        int total = WheelSegments.Length;

        string EmojiAt(int offset)
        {
            int idx = ((centreIndex + offset) % total + total) % total;
            return WheelSegments[idx].emoji;
        }

        // Offset by ~half a rotation so they feel "opposite" to the pointer.
        string topArc =
            $"{EmojiAt(6)} {EmojiAt(7)} {EmojiAt(8)} {EmojiAt(9)} {EmojiAt(10)} {EmojiAt(0)} {EmojiAt(1)}";

        string leftCtx = $"{EmojiAt(-3)} {EmojiAt(-2)} {EmojiAt(-1)}";
        string rightCtx = $"{EmojiAt(1)} {EmojiAt(2)} {EmojiAt(3)}";
        var (label, _, _, winEmoji) = WheelSegments[centreIndex];
        string selected = $"▶ {winEmoji} {label} ◀";

        string botArc =
            $"{EmojiAt(5)} {EmojiAt(4)} {EmojiAt(3)} {EmojiAt(2)} {EmojiAt(1)} {EmojiAt(0)} {EmojiAt(10)}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"       {topArc}");
        sb.AppendLine("  ╔══════════════════════════════════╗");
        sb.AppendLine($"  ║  {leftCtx}  {selected,-14}  {rightCtx}  ║");
        sb.AppendLine("  ╚══════════════════════════════════╝");
        sb.AppendLine($"       {botArc}");
        sb.AppendLine("```");
        return sb.ToString();
    }


    public enum PokerHand
    {
        HighCard = 0, JacksOrBetter = 1, TwoPair = 2, ThreeOfAKind = 3,
        Straight = 4, Flush = 5, FullHouse = 6, FourOfAKind = 7,
        StraightFlush = 8, RoyalFlush = 9
    }

    public static readonly (PokerHand hand, long multiplier, string label)[] PokerPayouts =
    [
        (PokerHand.RoyalFlush,    800, "👑 ROYAL FLUSH"),
        (PokerHand.StraightFlush,  50, "🔥 Straight Flush"),
        (PokerHand.FourOfAKind,    25, "4️⃣  Four of a Kind"),
        (PokerHand.FullHouse,       9, "🏠 Full House"),
        (PokerHand.Flush,           6, "♠️  Flush"),
        (PokerHand.Straight,        4, "➡️  Straight"),
        (PokerHand.ThreeOfAKind,    3, "3️⃣  Three of a Kind"),
        (PokerHand.TwoPair,         2, "2️⃣  Two Pair"),
        (PokerHand.JacksOrBetter,   1, "🃏 Jacks or Better"),
        (PokerHand.HighCard,        0, "❌ No Win"),
    ];

    public static PokerHand EvaluatePokerHand(List<string> hand)
    {
        // Parse rank indices and suits from "rank|suit" format
        var ranks = hand.Select(c => Array.IndexOf(CardRanks, c.Split('|')[0])).OrderBy(r => r).ToArray();
        var suits = hand.Select(c => c.Split('|')[1]).ToArray();

        bool isFlush = suits.Distinct().Count() == 1;
        bool isStraight = ranks[4] - ranks[0] == 4 && ranks.Distinct().Count() == 5;
        // Ace-low straight: A-2-3-4-5 → ranks 12,0,1,2,3
        bool isAceLow = ranks.SequenceEqual(new[] { 0, 1, 2, 3, 12 });
        if (isAceLow) isStraight = true;

        var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
        int first = groups[0].Count();
        int second = groups.Count > 1 ? groups[1].Count() : 0;

        if (isFlush && isStraight)
        {
            // Royal = 10,J,Q,K,A (indices 8,9,10,11,12)
            bool isRoyal = ranks.SequenceEqual(new[] { 8, 9, 10, 11, 12 });
            return isRoyal ? PokerHand.RoyalFlush : PokerHand.StraightFlush;
        }
        if (first == 4) return PokerHand.FourOfAKind;
        if (first == 3 && second == 2) return PokerHand.FullHouse;
        if (isFlush) return PokerHand.Flush;
        if (isStraight) return PokerHand.Straight;
        if (first == 3) return PokerHand.ThreeOfAKind;
        if (first == 2 && second == 2) return PokerHand.TwoPair;
        // Jacks or Better: pair of J(9), Q(10), K(11), A(12)
        if (first == 2 && groups[0].Key >= 9) return PokerHand.JacksOrBetter;
        return PokerHand.HighCard;
    }

    public static long PokerPayout(PokerHand hand, long bet)
    {
        var entry = PokerPayouts.First(p => p.hand == hand);
        return bet * entry.multiplier;
    }

    public static string PokerHandLabel(PokerHand hand) =>
        PokerPayouts.First(p => p.hand == hand).label;

    /// <summary>Formats a poker hand card for display, e.g. "A♠".</summary>
    public static string FormatPokerCard(string card, bool held)
    {
        var parts = card.Split('|');
        string display = $"{parts[0]}{parts[1]}";
        return held ? $"[**{display}**]" : display;
    }

    /// <summary>Builds and shuffles a standard 52-card deck in "rank|suit" format.</summary>
    public static List<string> BuildPokerDeck()
    {
        var deck = (from suit in CardSuits
                    from rank in CardRanks
                    select $"{rank}|{suit}").ToList();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }


    /// <summary>Outcomes for /invest. Multiplier applied to the locked amount after 24h.</summary>
    public static readonly (decimal multiplier, int weight, string label)[] InvestOutcomes =
    [
        (0.20m,  3,  "📉 Market crash — lost most of it"),
        (0.50m,  8,  "📉 Poor return — took a significant loss"),
        (0.75m,  12, "📊 Below average — small loss"),
        (1.00m,  17, "➡️  Break even"),
        (1.10m,  20, "📈 Modest gain"),
        (1.25m,  15, "📈 Good return"),
        (1.50m,  12, "📈 Strong return"),
        (2.00m,  8,  "🚀 Great return!"),
        (3.00m,  4,  "🚀 Excellent return!"),
        (5.00m,  1,  "🌟 Jackpot investment!"),
    ];

    public static (decimal multiplier, string label) RollInvestment()
    {
        int total = InvestOutcomes.Sum(o => o.weight);
        int roll = Random.Shared.Next(total);
        int cum = 0;
        foreach (var (mult, weight, label) in InvestOutcomes)
        {
            cum += weight;
            if (roll < cum) return (mult, label);
        }
        return (1.0m, "➡️  Break even");
    }


    public const string PokerBotId = "BOT";

    /// <summary>Display a card without held brackets, e.g. "A♠".</summary>
    public static string ShowCard(string card) => FormatPokerCard(card, held: false);

    /// <summary>Display a hand of cards space-separated.</summary>
    public static string ShowHand(IEnumerable<string> cards) =>
        string.Join("  ", cards.Select(ShowCard));

    /// <summary>
    /// Best PokerHand achievable from 7 cards (2 hole + 5 community).
    /// Checks all C(7,5) = 21 five-card combinations.
    /// </summary>
    public static (PokerHand hand, string name) BestHandType(List<string> sevenCards)
    {
        PokerHand best = PokerHand.HighCard;
        for (int i = 0; i < 7; i++)
            for (int j = i + 1; j < 7; j++)
            {
                var five = sevenCards.Where((_, idx) => idx != i && idx != j).ToList();
                var h = EvaluatePokerHand(five);
                if (h > best) best = h;
            }
        return (best, PokerHandLabel(best));
    }

    /// <summary>
    /// Integer score for showdown comparison. Hand category × 1M + rank-index sum.
    /// Handles ties for casual bot use.
    /// </summary>
    public static int HandScore(List<string> sevenCards)
    {
        var (hand, _) = BestHandType(sevenCards);
        int rankSum = sevenCards
            .Select(c => Array.IndexOf(CardRanks, c.Split('|')[0]))
            .Sum();
        return (int)hand * 1_000_000 + rankSum;
    }
}
