using System.Data;
using Microsoft.Data.SqlClient;
using DiscordBot.Constants;

namespace DiscordBot.Helper;

/// <summary>
/// Static catalog of all shop items and lightweight DB helper methods
/// used by Shop.cs, Gambling.cs, Economy.cs, and Pet.cs to check and
/// consume active effects without taking a direct dependency on Shop.cs.
/// </summary>
public static class ShopHelper
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    public enum ShopCategory { All, PetConsumable, PetCosmetic, Booster, GamblingPerk, Luxury }

    // ── Item definition ───────────────────────────────────────────────────────

    /// <param name="Key">Unique identifier used in DB and autocomplete.</param>
    /// <param name="Name">Display name shown in embeds.</param>
    /// <param name="Emoji">Emoji prefix for the item.</param>
    /// <param name="Description">Short description shown in /shop browse.</param>
    /// <param name="Effect">Longer effect description shown in /shop buy confirmation.</param>
    /// <param name="Price">Cost in credits.</param>
    /// <param name="Category">Used for browse filtering.</param>
    /// <param name="IsCosmetic">True → applies to active pet via PetCosmetics table; False → consumable/booster.</param>
    /// <param name="CosmeticType">Non-null for cosmetics: 'title' or 'aura'.</param>
    /// <param name="DurationMinutes">Non-null for timed effects (stored in UserActiveEffects with ExpiresAt).</param>
    /// <param name="StackCount">For multi-use effects like work_boost (default 1).</param>
    public sealed record ShopItem(
        string Key,
        string Name,
        string Emoji,
        string Description,
        string Effect,
        decimal Price,
        ShopCategory Category,
        bool IsCosmetic = false,
        string? CosmeticType = null,
        int? DurationMinutes = null,
        int StackCount = 1);

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Sentinel price for easter-egg items that are not purchasable via autocomplete.</summary>
    public const decimal EasterEggPrice = decimal.MaxValue;

    // ── Item Catalog ──────────────────────────────────────────────────────────

    public static readonly ShopItem[] Items =
    [
        // ── Pet Consumables ───────────────────────────────────────────────────
        // Priced relative to /daily (50K). Minor stat items: 0.3–0.5× daily.
        // Premium items (full restore / revive): 2–3.5× daily.
        new("kibble",       "Premium Kibble",    "🍖",
            "+30 Hunger to your active pet.",
            "Restores 30 Hunger on your active pet immediately.",
            20_000,  ShopCategory.PetConsumable),

        new("feast",        "Gourmet Feast",     "🍱",
            "+60 Hunger and +15 Happiness.",
            "Restores 60 Hunger and 15 Happiness on your active pet.",
            65_000,  ShopCategory.PetConsumable),

        new("treat",        "Tasty Treat",       "🍬",
            "+25 Happiness to your active pet.",
            "Boosts your active pet's Happiness by 25.",
            15_000,  ShopCategory.PetConsumable),

        new("luxury_toy",   "Luxury Toy",        "🧸",
            "+40 Happiness to your active pet.",
            "A premium toy that boosts your active pet's Happiness by 40.",
            45_000,  ShopCategory.PetConsumable),

        new("energy_drink", "Energy Drink",      "⚡",
            "+50 Energy to your active pet.",
            "Restores 50 Energy on your active pet.",
            30_000,  ShopCategory.PetConsumable),

        new("grooming_kit", "Grooming Kit",      "🛁",
            "+50 Hygiene to your active pet.",
            "Brings your active pet's Hygiene up by 50.",
            20_000,  ShopCategory.PetConsumable),

        new("full_restore", "Full Restore",      "💊",
            "Max all stats on your active pet.",
            "Instantly maxes Hunger, Happiness, Energy, and Hygiene on your active pet.",
            175_000, ShopCategory.PetConsumable),

        new("revive",       "Revive Potion",     "💫",
            "Wake a hibernating pet and restore all stats to 50.",
            "Wakes your active pet from hibernation and sets all stats to 50. Requires the pet to be hibernating.",
            100_000, ShopCategory.PetConsumable),

        // ── Pet Cosmetics ─────────────────────────────────────────────────────
        // Standard titles/auras: 4–5× daily. Legendary: 20× daily.
        new("title_dragon", "Dragon Tamer",      "🐉",
            "Badge displayed on /petcard.",
            "Equips the **Dragon Tamer** 🐉 title on your active pet — visible on /petcard.",
            250_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_star",   "Star Collector",    "⭐",
            "Badge displayed on /petcard.",
            "Equips the **Star Collector** ⭐ title on your active pet — visible on /petcard.",
            250_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_shadow", "Shadow Walker",     "🌑",
            "Badge displayed on /petcard.",
            "Equips the **Shadow Walker** 🌑 title on your active pet — visible on /petcard.",
            250_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_legend", "Legendary Tamer",   "🏆",
            "Prestige badge displayed on /petcard.",
            "Equips the **Legendary Tamer** 🏆 prestige title on your active pet — visible on /petcard.",
            1_000_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("aura_sparkle", "Sparkle Aura",      "✨",
            "Decorative aura shown on /petcard.",
            "Applies the **✨ Sparkle Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        new("aura_golden",  "Golden Aura",       "🌟",
            "Decorative aura shown on /petcard.",
            "Applies the **🌟 Golden Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        new("aura_flame",   "Flame Aura",        "🔥",
            "Decorative aura shown on /petcard.",
            "Applies the **🔥 Flame Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        // ── Boosters ──────────────────────────────────────────────────────────
        // Priced so the net value of the boost is roughly break-even or slight positive
        // over time, making them worth buying but not trivially free money.
        new("explore_boost", "Explore Boost",    "🗺️",
            "Guarantees a rare+ reward on your next /explore.",
            "Your pet's next adventure is guaranteed to return a Rare, Epic, or Legendary reward.",
            100_000, ShopCategory.Booster),

        new("xp_boost",     "XP Boost",          "📈",
            "2× pet XP for 60 minutes.",
            "Doubles all XP your active pet earns for the next 60 minutes (explore, battles, care actions).",
            150_000, ShopCategory.Booster, DurationMinutes: 60),

        new("daily_boost",  "Daily Boost",       "🎁",
            "Doubles your next /daily credit claim.",
            "Your next /daily payout will be doubled. Consumed automatically on use.",
            75_000,  ShopCategory.Booster),

        new("work_boost",   "Work Boost",        "💼",
            "2× credits for your next 3 /work sessions.",
            "The next 3 times you use /work, you'll earn double credits. Stacks automatically.",
            60_000,  ShopCategory.Booster, StackCount: 3),

        // ── Gambling Perks ────────────────────────────────────────────────────
        new("chaos_card",     "Chaos Card",        "🃏",
            "Randomizes the payout table for your next spin/wheel/scratch.",
            "Your next slots, big wheel, or scratch card roll uses a completely randomized payout table. Could be incredible, could be catastrophic. Consumed on use.",
            120_000, ShopCategory.GamblingPerk),

        new("comeback_chip",  "Comeback Chip",     "📈",
            "After 3 losses in a row, your next bet pays 1.5× guaranteed.",
            "Tracks your loss streak. On your 4th consecutive loss, your bet is automatically paid out at 1.5× regardless of the actual result. Consumed on trigger.",
            90_000,  ShopCategory.GamblingPerk),

        new("hot_streak",     "Hot Streak",        "🔥",
            "After 3 wins in a row, your next bet is free.",
            "Tracks your win streak. On your 4th consecutive win, your bet cost is refunded win or lose. Consumed on trigger.",
            110_000, ShopCategory.GamblingPerk),

        new("cd_reset",     "Cooldown Eraser",   "⏩",
            "Instantly resets all gambling cooldowns.",
            "Clears the cooldown on all gambling commands for you right now. Effect is instant — no storage needed.",
            35_000,  ShopCategory.GamblingPerk),

        new("mega_bet",     "Bet Limit Booster", "💰",
            $"Raises your max bet to unlimited for 60 minutes.",
            $"Temporarily removes the maximum bet cap (normally ⚡ {CreditHelper.Format(CreditHelper.MaxBet)}) for 60 minutes. Bet as much as you own.",
            5_000_000, ShopCategory.GamblingPerk, DurationMinutes: 60),

        new("impregnate_bot_owner", "Impregnate Bot Owner", "🤖",
            "Fills the bot owner with eggs. (Easter Egg)",
            "An unusual item that, when used, immediately impregnates the bot owner.",
            EasterEggPrice, ShopCategory.GamblingPerk),
        new("destroy_bot_owner_baby", "Destroy Bot Owner's Baby", "🍼",
            "Destroys one of the bot owner's babies. (Easter Egg)",
            "An unusual item that, when use, destroys one of the bot owner's babies if they have any for the person who buys this.",
            EasterEggPrice, ShopCategory.GamblingPerk),

        // ── Luxury — Mid-game (100M–5B) ───────────────────────────────────────
        // Meaningful for players in the 200M–20B range (ranks 3–6 on leaderboard).

        new("interest_boost",   "Interest Boost",      "📊",
            "Earns a flat 250M credits as a one-time interest payment.",
            "Instantly grants you 250,000,000 credits. Flat rate — not percentage based.",
            1_000_000_000, ShopCategory.Luxury),

        new("tax_evasion",      "Tax Evasion",         "🕵️",
            "Your next 10 gambling wins don't feed the passive jackpot pool.",
            "For your next 10 gambling wins, winnings do not feed the passive jackpot pool.",
            2_000_000_000, ShopCategory.Luxury, StackCount: 10),

        new("bank_heist",       "Bank Heist",          "🏦",
            "Steal 1%–5% of another user's balance. 48-hour cooldown.",
            "Attempt a heist on any user. Transfers a random 1–5% of their balance to you. Fails 30% of the time.",
            3_000_000_000, ShopCategory.Luxury),

        new("golden_ticket",    "Golden Ticket",       "🎫",
            "Doubles all credit earnings for 2 hours.",
            "For 2 hours all income is doubled: /daily, /work, gambling payouts, and fishing rewards.",
            5_000_000_000, ShopCategory.Luxury, DurationMinutes: 120),

        // ── Luxury — Prestige cosmetics (10B–50B) ─────────────────────────────

        new("aura_void",        "Void Aura",           "🌌",
            "Exclusive aura shown on /pet card. Rarest aura in the shop.",
            "Applies the 🌌 Void Aura to your active pet — the rarest aura available.",
            10_000_000_000, ShopCategory.Luxury, IsCosmetic: true, CosmeticType: "aura"),

        new("title_sovereign",  "Sovereign",           "👑",
            "Ultra-rare prestige title for your pet.",
            "Equips the 👑 Sovereign title on your active pet — visible on /pet card.",
            25_000_000_000, ShopCategory.Luxury, IsCosmetic: true, CosmeticType: "title"),

        new("aura_celestial",   "Celestial Aura",      "🌠",
            "The rarest aura in existence. Reserved for the ultra-wealthy.",
            "Applies the 🌠 Celestial Aura to your active pet. Fewer than a handful can ever own this.",
            50_000_000_000, ShopCategory.Luxury, IsCosmetic: true, CosmeticType: "aura"),

        // ── Luxury — High-end economy tools (100B–500B) ───────────────────────
        // Targeted at players in the 1T–100T range.

        new("market_crash",     "Market Crash",        "📉",
            "Crashes all stock prices by 20–40% server-wide.",
            "Triggers an immediate server-wide stock price crash. All prices drop by a random 20–40%.",
            100_000_000_000, ShopCategory.Luxury),

        new("jackpot_seed",     "Jackpot Seed",        "💣",
            "Seeds the passive jackpot with 100B credits instantly.",
            "Injects 100,000,000,000 credits into the server passive jackpot pool.",
            200_000_000_000, ShopCategory.Luxury),

        new("prestige_reset",   "Prestige Reset",      "🔄",
            "Resets your LifetimeEarned to 0 and refunds 50% of the price.",
            "Resets your prestige rank to Broke and refunds 100B credits. For those who want to climb again.",
            200_000_000_000, ShopCategory.Luxury),

        new("economy_nuke",     "Economy Nuke",        "☢️",
            "Halves every user's balance in the server. Irreversible.",
            "Immediately halves the credit balance of every user in this server. Cannot be undone.",
            500_000_000_000, ShopCategory.Luxury),

        // ── Ultra-Luxury (1T+) ────────────────────────────────────────────────
        // Meaningful credit sinks for the top 1–2 players on the leaderboard.

        new("title_eternal",    "Eternal",             "♾️",
            "The single most exclusive title. Only the richest can buy this.",
            "Equips the ♾️ Eternal title on your active pet. No one else on the server can see this without knowing what it cost.",
            1_000_000_000_000, ShopCategory.Luxury, IsCosmetic: true, CosmeticType: "title"),

        new("wealth_flex",      "Wealth Flex",         "💸",
            "Permanently burns 1T credits. Earns a server announcement and a unique badge.",
            "Destroys 1,000,000,000,000 credits from your balance with no functional return. A pure status symbol — the bot will announce your sacrifice server-wide.",
            1_000_000_000_000, ShopCategory.Luxury),

        new("golden_ticket_ii", "Golden Ticket II",    "🏅",
            "Triples all credit earnings for 6 hours.",
            "For 6 hours all income is tripled: /daily, /work, gambling payouts, and fishing. Does not stack with Golden Ticket.",
            2_000_000_000_000, ShopCategory.Luxury, DurationMinutes: 360),

        new("server_reset",     "Server Economy Reset","💥",
            "Resets every user's balance to 0. Nuclear option. Irreversible.",
            "Sets the balance of every user in the server to 0. LifetimeEarned is preserved for prestige. This is permanent and cannot be undone.",
            10_000_000_000_000, ShopCategory.Luxury),
    ];

    // ── Lookup helpers ────────────────────────────────────────────────────────

    public static ShopItem? Find(string key) =>
        Items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ShopItem> ByCategory(ShopCategory cat) =>
        cat == ShopCategory.All ? Items : Items.Where(i => i.Category == cat);

    // ── Category display ──────────────────────────────────────────────────────

    public static string CategoryEmoji(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "🐾",
        ShopCategory.PetCosmetic => "✨",
        ShopCategory.Booster => "📈",
        ShopCategory.GamblingPerk => "🎲",
        ShopCategory.Luxury => "💎",
        _ => "🛒"
    };

    public static string CategoryLabel(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "Pet Consumables",
        ShopCategory.PetCosmetic => "Pet Cosmetics",
        ShopCategory.Booster => "Boosters",
        ShopCategory.GamblingPerk => "Gambling Perks",
        ShopCategory.Luxury => "Luxury",
        _ => "All Items"
    };

    public static string CategoryDescription(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "Restore and boost your pet's stats.",
        ShopCategory.PetCosmetic => "Titles and auras shown on /petcard. Applied to your active pet.",
        ShopCategory.Booster => "Temporary multipliers and guaranteed upgrades.",
        ShopCategory.GamblingPerk => "Protection and enhancements for gambling commands.",
        ShopCategory.Luxury => "High-ticket items across four tiers: Mid-game (500M–5B), Prestige cosmetics (10B–50B), High-end tools (100B–500B), and Ultra-Luxury (1T+).",
        _ => "Browse all available items."
    };

    // ── Cosmetic display ──────────────────────────────────────────────────────

    /// <summary>Returns the display string for a cosmetic key (e.g. "🐉 Dragon Tamer").</summary>
    public static string CosmeticDisplay(string cosmeticKey)
    {
        var item = Find(cosmeticKey);
        return item is not null ? $"{item.Emoji} {item.Name}" : cosmeticKey;
    }

    // ── DB helpers ────────────────────────────────────────────────────────────
    //
    //  These are called from Gambling.cs, Economy.cs, and Pet.cs to check and
    //  consume effects without depending on Shop.cs.

    /// <summary>Returns true if the user owns at least 1 of this item in their inventory.</summary>
    public static bool HasItem(string userId, string serverId, string itemKey)
    {
        try
        {
            var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "GetInventoryItem",
            [
                new SqlParameter("@UserID",   userId),
                new SqlParameter("@ServerID", serverId),
                new SqlParameter("@ItemKey",  itemKey)
            ]);
            return dt.Rows.Count > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Decrements inventory by 1.
    /// Returns <c>true</c> if the item was present and consumed.
    /// </summary>
    public static bool ConsumeItem(string userId, string serverId, string itemKey)
    {
        try
        {
            var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "DeductFromInventory",
            [
                new SqlParameter("@UserID",   userId),
                new SqlParameter("@ServerID", serverId),
                new SqlParameter("@ItemKey",  itemKey)
            ]);
            return dt.Rows.Count > 0 && int.Parse(dt.Rows[0]["Success"].ToString()!) == 1;
        }
        catch { return false; }
    }

    /// <summary>
    /// Returns true if the user has an active (non-expired) effect with this key.
    /// </summary>
    public static bool HasActiveEffect(string userId, string serverId, string effectKey)
    {
        try
        {
            var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "GetActiveEffect",
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerID",  serverId),
                new SqlParameter("@EffectKey", effectKey)
            ]);
            return dt.Rows.Count > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Decrements StackCount on the active effect; removes it when exhausted.
    /// Returns <c>true</c> if the effect existed and was consumed.
    /// </summary>
    public static bool ConsumeActiveEffect(string userId, string serverId, string effectKey)
    {
        try
        {
            var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "ConsumeActiveEffect",
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerID",  serverId),
                new SqlParameter("@EffectKey", effectKey)
            ]);
            return dt.Rows.Count > 0 && int.Parse(dt.Rows[0]["Success"].ToString()!) == 1;
        }
        catch { return false; }
    }

    /// <summary>Writes (or overwrites) an active effect to UserActiveEffects.</summary>
    public static void SetActiveEffect(
        string userId, string serverId, string effectKey,
        DateTime? expiresAt = null, int stackCount = 1)
    {
        try
        {
            new StoredProcedure().UpdateCreate(Constants.Constants.discordBotConnStr, "AddActiveEffect",
            [
                new SqlParameter("@UserID",     userId),
                new SqlParameter("@ServerID",   serverId),
                new SqlParameter("@EffectKey",  effectKey),
                new SqlParameter("@ExpiresAt",  (object?)expiresAt ?? DBNull.Value),
                new SqlParameter("@StackCount", stackCount)
            ]);
        }
        catch { /* non-fatal */ }
    }

    /// <summary>Returns the remaining StackCount of an active effect (0 if not present / expired).</summary>
    public static int GetEffectStack(string userId, string serverId, string effectKey)
    {
        try
        {
            var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "GetActiveEffect",
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerID",  serverId),
                new SqlParameter("@EffectKey", effectKey)
            ]);
            return dt.Rows.Count > 0 ? int.Parse(dt.Rows[0]["StackCount"].ToString()!) : 0;
        }
        catch { return 0; }
    }
}