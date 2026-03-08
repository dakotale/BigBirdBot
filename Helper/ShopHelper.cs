using System.Data;
using System.Data.SqlClient;
using DiscordBot.Constants;

namespace DiscordBot.Helper;

/// <summary>
/// Static catalog of all shop items and lightweight DB helper methods
/// used by Shop.cs, Gambling.cs, Economy.cs, and Pet.cs to check and
/// consume active effects without taking a direct dependency on Shop.cs.
/// </summary>
public static class ShopHelper
{

    public enum ShopCategory { All, PetConsumable, PetCosmetic, Booster, GamblingPerk }


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
        long Price,
        ShopCategory Category,
        bool IsCosmetic = false,
        string? CosmeticType = null,
        int? DurationMinutes = null,
        int StackCount = 1);


    public static readonly ShopItem[] Items =
    [
        new("kibble",       "Premium Kibble",    "🍖",
            "+30 Hunger to your active pet.",
            "Restores 30 Hunger on your active pet immediately.",
            50_000,  ShopCategory.PetConsumable),

        new("feast",        "Gourmet Feast",     "🍱",
            "+60 Hunger and +15 Happiness.",
            "Restores 60 Hunger and 15 Happiness on your active pet.",
            120_000, ShopCategory.PetConsumable),

        new("treat",        "Tasty Treat",       "🍬",
            "+25 Happiness to your active pet.",
            "Boosts your active pet's Happiness by 25.",
            30_000,  ShopCategory.PetConsumable),

        new("luxury_toy",   "Luxury Toy",        "🧸",
            "+40 Happiness to your active pet.",
            "A premium toy that boosts your active pet's Happiness by 40.",
            80_000,  ShopCategory.PetConsumable),

        new("energy_drink", "Energy Drink",      "⚡",
            "+50 Energy to your active pet.",
            "Restores 50 Energy on your active pet.",
            60_000,  ShopCategory.PetConsumable),

        new("grooming_kit", "Grooming Kit",      "🛁",
            "+50 Hygiene to your active pet.",
            "Brings your active pet's Hygiene up by 50.",
            40_000,  ShopCategory.PetConsumable),

        new("full_restore", "Full Restore",      "💊",
            "Max all stats on your active pet.",
            "Instantly maxes Hunger, Happiness, Energy, and Hygiene on your active pet.",
            250_000, ShopCategory.PetConsumable),

        new("revive",       "Revive Potion",     "💫",
            "Wake a hibernating pet and restore all stats to 50.",
            "Wakes your active pet from hibernation and sets all stats to 50. Requires the pet to be hibernating.",
            150_000, ShopCategory.PetConsumable),

        new("title_dragon", "Dragon Tamer",      "🐉",
            "Badge displayed on /petcard.",
            "Equips the **Dragon Tamer** 🐉 title on your active pet — visible on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_star",   "Star Collector",    "⭐",
            "Badge displayed on /petcard.",
            "Equips the **Star Collector** ⭐ title on your active pet — visible on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_shadow", "Shadow Walker",     "🌑",
            "Badge displayed on /petcard.",
            "Equips the **Shadow Walker** 🌑 title on your active pet — visible on /petcard.",
            200_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("title_legend", "Legendary Tamer",   "🏆",
            "Prestige badge displayed on /petcard.",
            "Equips the **Legendary Tamer** 🏆 prestige title on your active pet — visible on /petcard.",
            500_0000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "title"),

        new("aura_sparkle", "Sparkle Aura",      "✨",
            "Decorative aura shown on /petcard.",
            "Applies the **✨ Sparkle Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            300_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        new("aura_golden",  "Golden Aura",       "🌟",
            "Decorative aura shown on /petcard.",
            "Applies the **🌟 Golden Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            300_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        new("aura_flame",   "Flame Aura",        "🔥",
            "Decorative aura shown on /petcard.",
            "Applies the **🔥 Flame Aura** to your active pet — shown in the Cosmetics field on /petcard.",
            300_000, ShopCategory.PetCosmetic, IsCosmetic: true, CosmeticType: "aura"),

        new("explore_boost", "Explore Boost",    "🗺️",
            "Guarantees a rare+ reward on your next /explore.",
            "Your pet's next adventure is guaranteed to return a Rare, Epic, or Legendary reward.",
            150_000, ShopCategory.Booster),

        new("xp_boost",     "XP Boost",          "📈",
            "2× pet XP for 60 minutes.",
            "Doubles all XP your active pet earns for the next 60 minutes (explore, battles, care actions).",
            200_000, ShopCategory.Booster, DurationMinutes: 60),

        new("daily_boost",  "Daily Boost",       "🎁",
            "Doubles your next /daily credit claim.",
            "Your next /daily payout will be doubled. Consumed automatically on use.",
            200_000, ShopCategory.Booster),

        new("work_boost",   "Work Boost",        "💼",
            "2× credits for your next 3 /work sessions.",
            "The next 3 times you use /work, you'll earn double credits. Stacks automatically.",
            150_000, ShopCategory.Booster, StackCount: 3),

        new("bk_shield",    "Bankrupt Shield",   "🛡️",
            "Blocks the next BANKRUPT result on /bigwheel.",
            "If the Big Wheel lands on BANKRUPT, the result is ignored and you keep your bet. Consumed on trigger.",
            80_000,  ShopCategory.GamblingPerk),

        new("insurance",    "Gamble Insurance",  "📋",
            "Refunds 50% of your bet on your next /bigwheel loss.",
            "If you lose your next /bigwheel spin (payout < bet), 50% of your original bet is refunded. Consumed on trigger.",
            120_000, ShopCategory.GamblingPerk),

        new("cd_reset",     "Cooldown Eraser",   "⏩",
            "Instantly resets all gambling cooldowns.",
            "Clears the cooldown on all gambling commands for you right now. Effect is instant — no storage needed.",
            50_000,  ShopCategory.GamblingPerk),

        new("mega_bet",     "Bet Limit Booster", "💰",
            "Raises your max bet to 1,000,000,000,000 for 60 minutes.",
            $"Temporarily raises the maximum bet cap from ⚡ {CreditHelper.MaxBet} to ⚡ {CreditHelper.MaxBet + 1000000000000} for 60 minutes.",
            300_0000, ShopCategory.GamblingPerk, DurationMinutes: 60),
    ];


    public static ShopItem? Find(string key) =>
        Items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ShopItem> ByCategory(ShopCategory cat) =>
        cat == ShopCategory.All ? Items : Items.Where(i => i.Category == cat);


    public static string CategoryEmoji(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "🐾",
        ShopCategory.PetCosmetic => "✨",
        ShopCategory.Booster => "📈",
        ShopCategory.GamblingPerk => "🎲",
        _ => "🛒"
    };

    public static string CategoryLabel(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "Pet Consumables",
        ShopCategory.PetCosmetic => "Pet Cosmetics",
        ShopCategory.Booster => "Boosters",
        ShopCategory.GamblingPerk => "Gambling Perks",
        _ => "All Items"
    };

    public static string CategoryDescription(ShopCategory cat) => cat switch
    {
        ShopCategory.PetConsumable => "Restore and boost your pet's stats.",
        ShopCategory.PetCosmetic => "Titles and auras shown on /petcard. Applied to your active pet.",
        ShopCategory.Booster => "Temporary multipliers and guaranteed upgrades.",
        ShopCategory.GamblingPerk => "Protection and enhancements for gambling commands.",
        _ => "Browse all available items."
    };


    /// <summary>Returns the display string for a cosmetic key (e.g. "🐉 Dragon Tamer").</summary>
    public static string CosmeticDisplay(string cosmeticKey)
    {
        var item = Find(cosmeticKey);
        return item is not null ? $"{item.Emoji} {item.Name}" : cosmeticKey;
    }

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
