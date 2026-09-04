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
    public static string Format(decimal amount) =>
        $"{CurrencyEmoji} **{amount:N0}**";

    /// <summary>Formats a signed delta, e.g. "+⚡ 500" or "-⚡ 200"</summary>
    public static string FormatDelta(decimal delta) =>
        delta >= 0 ? $"+{CurrencyEmoji} {delta:N0}" : $"-{CurrencyEmoji} {Math.Abs(delta):N0}";

    // ── Prestige ranks ────────────────────────────────────────────────────────
    private static readonly (decimal threshold, string rank)[] PrestigeTiers =
    [
        (0m,                  "🪨 Broke"),
        (1_000_000m,          "🥉 Bronze"),
        (10_000_000m,         "🥈 Silver"),
        (100_000_000m,        "🥇 Gold"),
        (1_000_000_000m,      "💎 Diamond"),
        (10_000_000_000m,     "👑 Elite"),
        (100_000_000_000m,    "🌟 Legend"),
        (1_000_000_000_000m,  "🚀 Mythic"),
    ];

    /// <summary>Returns the highest prestige rank whose threshold the user's lifetime earnings have reached.</summary>
    public static string PrestigeRank(decimal lifetimeEarned)
    {
        var tier = PrestigeTiers[0];
        foreach (var t in PrestigeTiers)
            if (lifetimeEarned >= t.threshold) tier = t;
        return tier.rank;
    }


    public const decimal DailyAmount = 100_000m;
    public const int DailyCooldownHours = 24;
    public const int WorkCooldownMinutes = 60;
    public const decimal WorkMin = 5_000m;
    public const decimal WorkMax = 75_000m;
    public const decimal PassiveMessageAmount = 25m;
    public const decimal PuzzleSolveAmount = 250_000m;

    // ── Daily streak multiplier table ─────────────────────────────────────────
    // Thresholds are inclusive lower bounds. The highest matching tier wins.
    private static readonly (int minDay, decimal multiplier, string label)[] StreakTiers =
    [
        (1,  1.00m, ""),
        (3,  1.25m, "🔥 3-day streak"),
        (5,  1.50m, "🔥 5-day streak"),
        (7,  2.00m, "⚡ Week streak!"),
        (14, 3.00m, "💎 2-week streak!"),
        (30, 5.00m, "👑 Monthly streak!"),
    ];

    /// <summary>
    /// Returns (multiplier, label) for the given consecutive day streak.
    /// multiplier=1 and label="" for streaks below 3 days.
    /// </summary>
    public static (decimal multiplier, string label) StreakMultiplier(int streak)
    {
        var tier = StreakTiers[0];
        foreach (var t in StreakTiers)
            if (streak >= t.minDay) tier = t;
        return (tier.multiplier, tier.label);
    }

    /// <summary>Credits awarded on pet level-up: 500 × new level.</summary>
    public static decimal PetLevelUpAmount(int newLevel) => 500m * newLevel;


    public const decimal MinBet = 10m;
    public const decimal MaxBet = 100_000_000_000m;
    public const decimal DailyLossLimit = 100_000_000_000m; // max credits losable per 24h

    /// <summary>Validates a bet against the min/max bet limits and the user's balance, returning an error message if invalid.</summary>
    public static bool IsValidBet(decimal bet, decimal balance, out string error)
    {
        if (bet < MinBet) { error = $"Minimum bet is {Format(MinBet)}."; return false; }
        if (bet > MaxBet) { error = $"Maximum bet is {Format(MaxBet)}."; return false; }
        if (bet > balance) { error = $"You don't have enough credits! Balance: {Format(balance)}."; return false; }
        error = "";
        return true;
    }


    /// <summary>Picks a random index into <paramref name="weights"/>, biased so each index's chance is proportional to its weight.</summary>
    private static int WeightedIndex(int[] weights)
    {
        int total = weights.Sum();
        int roll = Random.Shared.Next(total);
        int cum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cum += weights[i];
            if (roll < cum) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>Random symbols for the spinning animation frames (not weighted — pure visual).</summary>
    public static readonly string[] SlotSpinSymbols = ["💎", "7️⃣", "🍀", "⭐", "🔔", "🍇", "🍊", "🍋", "🍒"];

    /// <summary>Picks a purely random slot symbol for animation frames — not weighted, since it's never the final result.</summary>
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

    /// <summary>Spins one slot reel, weighted by each symbol's configured rarity.</summary>
    public static string SpinReel() =>
        SlotSymbols[WeightedIndex(SlotSymbols.Select(s => s.weight).ToArray())].symbol;

    /// <summary>Scores a completed 3-reel slot spin: three-of-a-kind, two-of-a-kind, cherry consolation, or no match.</summary>
    public static (decimal payout, string result) CalculateSlotPayout(
        string r1, string r2, string r3, decimal bet)
    {
        // Three of a kind
        if (r1 == r2 && r2 == r3)
        {
            var sym = SlotSymbols.FirstOrDefault(s => s.symbol == r1);
            decimal payout = bet * (decimal)sym.multiplier;
            return (payout, $"🎰 **Three {sym.name}s!** {FormatDelta(payout)}");
        }

        // Two of a kind (partial win = 0.5× bet back)
        if (r1 == r2 || r2 == r3 || r1 == r3)
        {
            decimal payout = bet / 2m;
            return (payout, $"Almost! Two matching. {FormatDelta(payout)}");
        }

        // Any cherry = small consolation
        if (r1 == "🍒" || r2 == "🍒" || r3 == "🍒")
        {
            decimal payout = bet / 4m;
            return (payout, $"🍒 Cherry consolation prize! {FormatDelta(payout)}");
        }

        return (0m, $"No match. {FormatDelta(-bet)}");
    }


    public static readonly string[] RedNumbers = ["1", "3", "5", "7", "9", "12", "14", "16", "18", "19", "21", "23", "25", "27", "30", "32", "34", "36"];
    public static readonly string[] BlackNumbers = ["2", "4", "6", "8", "10", "11", "13", "15", "17", "20", "22", "24", "26", "28", "29", "31", "33", "35"];

    /// <summary>Spins the roulette wheel, uniformly at random across 0–36.</summary>
    public static int SpinRoulette() => Random.Shared.Next(0, 37); // 0–36

    /// <summary>Scores a roulette bet (red/black/even/odd/low/high/exact number) against the spun result.</summary>
    public static (decimal payout, string result) CalculateRoulettePayout(
        int spin, string bet, decimal amount)
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
            "red" => isRed ? (amount * 1.9m, $"{spinDisplay} — Red wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — Red loses. {FormatDelta(-amount)}"),
            "black" => isBlack ? (amount * 1.9m, $"{spinDisplay} — Black wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — Black loses. {FormatDelta(-amount)}"),
            "even" => isEven ? (amount * 1.9m, $"{spinDisplay} — Even wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — Even loses. {FormatDelta(-amount)}"),
            "odd" => isOdd ? (amount * 1.9m, $"{spinDisplay} — Odd wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — Odd loses. {FormatDelta(-amount)}"),
            "low" => isLow ? (amount * 1.9m, $"{spinDisplay} — 1-18 wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — 1-18 loses. {FormatDelta(-amount)}"),
            "high" => isHigh ? (amount * 1.9m, $"{spinDisplay} — 19-36 wins! {FormatDelta(amount * 1.9m)}")
                               : (0m, $"{spinDisplay} — 19-36 loses. {FormatDelta(-amount)}"),
            _ when int.TryParse(bet, out int num) && num == spin
                    => (amount * 35m, $"{spinDisplay} — Exact number wins! {FormatDelta(amount * 35m)}"),
            _ when int.TryParse(bet, out _)
                    => (0, $"{spinDisplay} — Wrong number. {FormatDelta(-amount)}"),
            _ => (0, $"{spinDisplay} — Invalid bet. {FormatDelta(-amount)}")
        };
    }


    public static readonly (string name, string emoji, int weight, double odds)[] Horses =
    [
        ("Thunderbolt",  "🐎", 28, 2.0),   // favourite
        ("Silver Wind",  "🏇", 22, 2.5),
        ("Crimson Dawn", "🦄", 17, 3.5),
        ("Iron Fist",    "🐴", 13, 5.0),
        ("Dark Matter",  "🐎", 9,  7.0),
        ("Lucky Star",   "⭐", 6,  12.0),
        ("Ghost Rider",  "💀", 3,  25.0),
        ("Miracle Run",  "✨", 2,  50.0),
    ];

    /// <summary>Picks the winning horse, weighted by each horse's configured odds (favourites win more often).</summary>
    public static int RunRace() =>
        WeightedIndex(Horses.Select(h => h.weight).ToArray());

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


    public const decimal ScratchCardCost = 2_000m;

    public static readonly (string[] symbols, decimal multiplier, string label)[] ScratchPrizes =
    [
        (["💎","💎","💎"], 100m, "JACKPOT"),
        (["7️⃣","7️⃣","7️⃣"],  50m, "Triple 7s"),
        (["⭐","⭐","⭐"],  20m, "Triple Stars"),
        (["🔔","🔔","🔔"],  10m, "Triple Bells"),
        (["🍀","🍀","🍀"],   5m, "Triple Clovers"),
        (["💰","💰","💰"],   3m, "Triple Coins"),
        (["🎁","🎁","🎁"],   2m, "Triple Gifts"),
    ];

    private static readonly string[] ScratchPool =
        ["💎", "7️⃣", "⭐", "🔔", "🍀", "💰", "🎁", "❌", "❌", "❌", "❌", "❌"];

    /// <summary>Scratches a standard card: rolls for one of the fixed prize tiers, or three non-matching symbols on a miss.</summary>
    public static (string s1, string s2, string s3, decimal payout, string label) ScratchCard(decimal cost)
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
        return (distinct[0], distinct[1], distinct[2], 0m, "No match");
    }

    /// <summary>
    /// Chaos Card variant — shuffles the prize table and picks from a
    /// randomized order, then applies a random multiplier tweak (0.5×–3×)
    /// to whatever prize is hit. Win odds and payouts are completely unpredictable.
    /// </summary>
    public static (string s1, string s2, string s3, decimal payout, string label) ScratchCardChaos(decimal cost)
    {
        // Shuffle prize table
        var shuffled = ScratchPrizes.OrderBy(_ => Random.Shared.Next()).ToArray();

        // Random win roll — same total range but prizes in chaos order
        int roll = Random.Shared.Next(100);
        int cursor = 0;
        int[] thresholds = [2, 5, 10, 18, 28, 40, 52];

        for (int i = 0; i < shuffled.Length && i < thresholds.Length; i++)
        {
            if (roll < thresholds[i])
            {
                var prize = shuffled[i];
                // Random multiplier twist: 0.5×, 1×, 1.5×, 2×, or 3× the normal payout
                decimal[] twists = [0.5m, 1m, 1.5m, 2m, 3m];
                decimal twist = twists[Random.Shared.Next(twists.Length)];
                decimal payout = cost * prize.multiplier * twist;
                string label = twist != 1m
                    ? $"🃏 {prize.label} ({twist}× chaos)"
                    : $"🃏 {prize.label}";
                return (prize.symbols[0], prize.symbols[1], prize.symbols[2], payout, label);
            }
            cursor = thresholds[i];
        }

        // No win — chaos symbols (may look like they should match but don't)
        var pool = ScratchPool.OrderBy(_ => Random.Shared.Next()).Take(3).ToArray();
        return (pool[0], pool[1], pool[2], 0m, "🃏 No match");
    }


    /// <summary>
    /// Payout for dice. pick values: "over","under","seven","doubles".
    /// </summary>
    public static decimal DicePayout(string pick, int d1, int d2, decimal bet)
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
        if (!won) return 0m;
        return pick switch
        {
            "seven" => bet * 4m,
            "doubles" => bet * 6.0m,
            _ => bet * 1.8m
        };
    }


    public static readonly string[] CardRanks = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"];
    public static readonly string[] CardSuits = ["♠", "♥", "♦", "♣"];

    /// <summary>Draws a uniformly random playing card for High-Low; value is the rank index (0=2 ... 12=A) for comparison.</summary>
    public static (string display, int value) DrawCard()
    {
        int rankIdx = Random.Shared.Next(CardRanks.Length);
        string suit = CardSuits[Random.Shared.Next(CardSuits.Length)];
        return ($"{CardRanks[rankIdx]}{suit}", rankIdx); // value = index (0=2 ... 12=A)
    }


    /// <summary>Returns a random flavour-text line for the /work command's payout, purely cosmetic.</summary>
    public static string WorkMessage(decimal earned) => Random.Shared.Next(10) switch
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

    public static readonly (string name, string emoji, decimal min, decimal max, int weight, string flavour)[] FishTable =
    [
        // Junk
        ("Old Boot",        "👢",  0,       0,       10, "You reeled in an old boot. The lake is not impressed with you."),
        ("Seaweed",         "🌿",  0,       0,       9,  "A soggy clump of seaweed. The fish are laughing."),
        ("Tin Can",         "🥫",  0,       0,       6,  "Someone else's problem is now your problem."),
        // Common
        ("Minnow",          "🐟",  1_000,   4_000,   20, "A tiny minnow. Technically a fish."),
        ("Perch",           "🐠",  3_000,   7_000,   18, "A solid perch. Dinner is sorted."),
        ("Bass",            "🎣",  5_500,   11_000,  15, "A decent bass. The rod barely bent."),
        // Uncommon
        ("Trout",           "🐟",  9_000,   16_000,  10, "A plump trout. The river was generous today."),
        ("Salmon",          "🍣",  13_000,  22_000,  8,  "A beautiful salmon leapt straight into the net."),
        ("Carp",            "🐡",  11_000,  19_000,  8,  "A hefty carp. It put up a real fight."),
        // Rare
        ("Swordfish",       "⚔️",  22_000,  42_000,  5,  "A swordfish! Your arms are still trembling."),
        ("Giant Tuna",      "🐟",  40_000,  70_000,  3,  "A giant tuna! The rod nearly snapped clean in half."),
        ("Golden Koi",      "🏅",  60_000,  110_000, 2,  "A golden koi! It practically glows in your hands."),
        // Legendary
        ("Legendary Carp",  "👑",  120_000, 250_000, 1,  "A LEGENDARY CARP. Witnesses gather. Someone starts clapping."),
    ];

    /// <summary>Rolls a fishing catch, weighted by each fish's rarity, with a random reward within that fish's credit range.</summary>
    public static (string name, string emoji, decimal credits, string flavour) CastLine()
    {
        var (name, emoji, min, max, _, flavour) = FishTable[WeightedIndex(FishTable.Select(f => f.weight).ToArray())];
        decimal credits = max > 0 ? Math.Floor(min + (decimal)Random.Shared.NextDouble() * (max - min + 1m)) : 0m;
        return (name, emoji, credits, flavour);
    }


    public static readonly (string label, double multiplier, int weight, string emoji)[] WheelSegments =
    [
        ("BANKRUPT", 0.0,    5, "💀"),
        ("0.125×",   0.125,  7, "🪦"),
        ("0.25×",    0.25,  10, "💸"),
        ("0.375×",   0.375,  9, "😰"),
        ("0.5×",     0.5,   11, "😬"),
        ("0.75×",    0.75,  12, "😕"),
        ("1×",       1.0,    6, "😐"),
        ("1.5×",     1.5,    9, "🙂"),
        ("2×",       2.0,    8, "😊"),
        ("3×",       3.0,    6, "😁"),
        ("5×",       5.0,    5, "🤩"),
        ("10×",     10.0,    5, "🔥"),
        ("25×",     25.0,    2, "💎"),
        ("50×",     50.0,    3, "👑"),
        ("100×",   100.0,    2, "🚀"),
    ];

    /// <summary>Spins the Big Wheel, weighted by each segment's configured rarity.</summary>
    public static int SpinWheel() =>
        WeightedIndex(WheelSegments.Select(s => s.weight).ToArray());

    /// <summary>
    /// Chaos Card variant — randomizes each segment's weight before spinning.
    /// Any outcome is possible at any probability, including extreme jackpots or
    /// repeated BANKRUPTs. Weights are re-rolled per-segment from 1–30.
    /// </summary>
    public static int SpinWheelChaos() =>
        WeightedIndex(WheelSegments.Select(_ => Random.Shared.Next(1, 31)).ToArray());

    /// <summary>
    /// Renders the Big Wheel as a horizontal wheel-of-fortune display.
    ///
    /// Layout (viewed from front — fixed pointer, segments scroll left-right):
    ///
    ///   top arc  : segments on the back of the wheel (half-rotation away)
    ///   -------- : wheel rim
    ///   rim band : 3 context segments . SELECTED . 3 context segments
    ///   -------- : wheel rim
    ///   bot arc  : more back-of-wheel segments (opposite side)
    ///
    /// Each frame the centreIndex advances, so all three rows shift together --
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


    public static readonly (decimal multiplier, int weight, string label)[] InvestOutcomes =
    [
        (0.20m,  3,  "Market crash"),
        (0.50m,  8,  "Poor return"),
        (0.75m,  12, "Below average"),
        (1.00m,  17, "Break even"),
        (1.10m,  20, "Modest gain"),
        (1.25m,  15, "Good return"),
        (1.50m,  12, "Strong return"),
        (2.00m,  8,  "Great return!"),
        (3.00m,  4,  "Excellent return!"),
        (5.00m,  1,  "Jackpot investment!"),
    ];

    /// <summary>Rolls a 24h investment outcome, weighted by each tier's configured rarity.</summary>
    public static (decimal multiplier, string label) RollInvestment()
    {
        var o = InvestOutcomes[WeightedIndex(InvestOutcomes.Select(o => o.weight).ToArray())];
        return (o.multiplier, o.label);
    }
}