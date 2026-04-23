using DiscordBot.Helper;

namespace DiscordBot.Tests.Unit;

public class ShopHelperTests
{
    // ── EasterEggPrice constant ───────────────────────────────────────────────

    [Fact]
    public void EasterEggPrice_IsDecimalMaxValue()
    {
        Assert.Equal(decimal.MaxValue, ShopHelper.EasterEggPrice);
    }

    // ── Items catalog: structural integrity ───────────────────────────────────

    [Fact]
    public void Items_HasAtLeastOneEntry()
    {
        Assert.NotEmpty(ShopHelper.Items);
    }

    [Fact]
    public void Items_AllKeysAreUnique()
    {
        var keys = ShopHelper.Items.Select(i => i.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Items_AllKeysAndNamesNonEmpty()
    {
        foreach (var item in ShopHelper.Items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Key),         $"Item has empty Key");
            Assert.False(string.IsNullOrWhiteSpace(item.Name),        $"Key={item.Key}: empty Name");
            Assert.False(string.IsNullOrWhiteSpace(item.Emoji),       $"Key={item.Key}: empty Emoji");
            Assert.False(string.IsNullOrWhiteSpace(item.Description), $"Key={item.Key}: empty Description");
            Assert.False(string.IsNullOrWhiteSpace(item.Effect),      $"Key={item.Key}: empty Effect");
        }
    }

    [Fact]
    public void Items_AllNonEasterEggItemsHavePositivePrice()
    {
        foreach (var item in ShopHelper.Items.Where(i => i.Price != ShopHelper.EasterEggPrice))
            Assert.True(item.Price > 0, $"Key={item.Key}: non-positive price {item.Price}");
    }

    [Fact]
    public void Items_EasterEggItemsHaveMaxPrice()
    {
        var easterEggs = ShopHelper.Items.Where(i => i.Price == ShopHelper.EasterEggPrice).ToArray();
        Assert.NotEmpty(easterEggs);
        foreach (var item in easterEggs)
            Assert.Equal(decimal.MaxValue, item.Price);
    }

    [Fact]
    public void Items_CosmeticsHaveCosmeticType()
    {
        foreach (var item in ShopHelper.Items.Where(i => i.IsCosmetic))
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CosmeticType),
                $"Cosmetic key={item.Key} has null/empty CosmeticType");
            Assert.True(item.CosmeticType is "title" or "aura",
                $"Key={item.Key}: unexpected CosmeticType '{item.CosmeticType}'");
        }
    }

    [Fact]
    public void Items_NonCosmeticsHaveNullCosmeticType()
    {
        foreach (var item in ShopHelper.Items.Where(i => !i.IsCosmetic))
            Assert.Null(item.CosmeticType);
    }

    [Fact]
    public void Items_TimedEffectsHavePositiveDuration()
    {
        foreach (var item in ShopHelper.Items.Where(i => i.DurationMinutes.HasValue))
            Assert.True(item.DurationMinutes!.Value > 0,
                $"Key={item.Key}: non-positive DurationMinutes");
    }

    [Fact]
    public void Items_WorkBoostHasStackCount3()
    {
        var item = ShopHelper.Find("work_boost");
        Assert.NotNull(item);
        Assert.Equal(3, item!.StackCount);
    }

    [Fact]
    public void Items_TaxEvasionHasStackCount10()
    {
        var item = ShopHelper.Find("tax_evasion");
        Assert.NotNull(item);
        Assert.Equal(10, item!.StackCount);
    }

    [Fact]
    public void Items_DefaultStackCountIsOne()
    {
        // Items without explicit StackCount override should default to 1
        var kibble = ShopHelper.Find("kibble");
        Assert.NotNull(kibble);
        Assert.Equal(1, kibble!.StackCount);
    }

    [Fact]
    public void Items_PetCosmeticCategory_AllAreCosmetic()
    {
        var cosmeticItems = ShopHelper.ByCategory(ShopHelper.ShopCategory.PetCosmetic).ToArray();
        Assert.NotEmpty(cosmeticItems);
        Assert.All(cosmeticItems, i => Assert.True(i.IsCosmetic));
    }

    [Fact]
    public void Items_PetConsumableCategory_NoneAreCosmetic()
    {
        var consumables = ShopHelper.ByCategory(ShopHelper.ShopCategory.PetConsumable).ToArray();
        Assert.NotEmpty(consumables);
        Assert.All(consumables, i => Assert.False(i.IsCosmetic));
    }

    [Fact]
    public void Items_ContainsExpectedKeys()
    {
        string[] requiredKeys =
        [
            "kibble", "feast", "treat", "energy_drink", "grooming_kit",
            "full_restore", "revive", "title_dragon", "title_star",
            "aura_sparkle", "aura_golden", "aura_flame",
            "explore_boost", "xp_boost", "daily_boost", "work_boost",
            "chaos_card", "comeback_chip", "hot_streak",
            "interest_boost", "market_crash", "economy_nuke",
        ];
        foreach (var key in requiredKeys)
            Assert.NotNull(ShopHelper.Find(key));
    }

    // ── Specific item properties ──────────────────────────────────────────────

    [Fact]
    public void Items_XpBoost_HasDuration60Minutes()
    {
        var item = ShopHelper.Find("xp_boost");
        Assert.NotNull(item);
        Assert.Equal(60, item!.DurationMinutes);
    }

    [Fact]
    public void Items_GoldenTicket_HasDuration120Minutes()
    {
        var item = ShopHelper.Find("golden_ticket");
        Assert.NotNull(item);
        Assert.Equal(120, item!.DurationMinutes);
    }

    [Fact]
    public void Items_GoldenTicketII_HasDuration360Minutes()
    {
        var item = ShopHelper.Find("golden_ticket_ii");
        Assert.NotNull(item);
        Assert.Equal(360, item!.DurationMinutes);
    }

    [Fact]
    public void Items_Revive_IsInPetConsumableCategory()
    {
        var item = ShopHelper.Find("revive");
        Assert.NotNull(item);
        Assert.Equal(ShopHelper.ShopCategory.PetConsumable, item!.Category);
    }

    [Fact]
    public void Items_TitleDragon_IsCosmeticWithTitleType()
    {
        var item = ShopHelper.Find("title_dragon");
        Assert.NotNull(item);
        Assert.True(item!.IsCosmetic);
        Assert.Equal("title", item.CosmeticType);
    }

    [Fact]
    public void Items_AuraSparkle_IsCosmeticWithAuraType()
    {
        var item = ShopHelper.Find("aura_sparkle");
        Assert.NotNull(item);
        Assert.True(item!.IsCosmetic);
        Assert.Equal("aura", item.CosmeticType);
    }

    [Fact]
    public void Items_ServerReset_IsMostExpensiveNonEasterEgg()
    {
        var item = ShopHelper.Find("server_reset");
        Assert.NotNull(item);
        decimal maxPrice = ShopHelper.Items
            .Where(i => i.Price != ShopHelper.EasterEggPrice)
            .Max(i => i.Price);
        Assert.Equal(maxPrice, item!.Price);
    }

    [Fact]
    public void Items_Treat_IsCheapestPetConsumable()
    {
        var consumables = ShopHelper.ByCategory(ShopHelper.ShopCategory.PetConsumable)
            .Where(i => i.Price != ShopHelper.EasterEggPrice)
            .OrderBy(i => i.Price)
            .ToArray();
        Assert.NotEmpty(consumables);
        Assert.Equal("treat", consumables[0].Key);
    }

    // ── Find ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Find_ExistingKey_ReturnsItem()
    {
        var item = ShopHelper.Find("kibble");
        Assert.NotNull(item);
        Assert.Equal("kibble", item!.Key);
    }

    [Fact]
    public void Find_CaseInsensitive_ReturnsItem()
    {
        Assert.NotNull(ShopHelper.Find("KIBBLE"));
        Assert.NotNull(ShopHelper.Find("Kibble"));
        Assert.NotNull(ShopHelper.Find("kIbBlE"));
    }

    [Fact]
    public void Find_NonExistentKey_ReturnsNull()
    {
        Assert.Null(ShopHelper.Find("nonexistent_item_xyz"));
        Assert.Null(ShopHelper.Find(""));
    }

    [Fact]
    public void Find_AllItemsCanBeFoundByTheirOwnKey()
    {
        foreach (var item in ShopHelper.Items)
        {
            var found = ShopHelper.Find(item.Key);
            Assert.NotNull(found);
            Assert.Equal(item.Key, found!.Key);
        }
    }

    // ── ByCategory ────────────────────────────────────────────────────────────

    [Fact]
    public void ByCategory_All_ReturnsAllItems()
    {
        var all = ShopHelper.ByCategory(ShopHelper.ShopCategory.All).ToArray();
        Assert.Equal(ShopHelper.Items.Length, all.Length);
    }

    [Fact]
    public void ByCategory_PetConsumable_ReturnsOnlyPetConsumables()
    {
        var items = ShopHelper.ByCategory(ShopHelper.ShopCategory.PetConsumable).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(ShopHelper.ShopCategory.PetConsumable, i.Category));
    }

    [Fact]
    public void ByCategory_Booster_ReturnsOnlyBoosters()
    {
        var items = ShopHelper.ByCategory(ShopHelper.ShopCategory.Booster).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(ShopHelper.ShopCategory.Booster, i.Category));
    }

    [Fact]
    public void ByCategory_GamblingPerk_ReturnsOnlyGamblingPerks()
    {
        var items = ShopHelper.ByCategory(ShopHelper.ShopCategory.GamblingPerk).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(ShopHelper.ShopCategory.GamblingPerk, i.Category));
    }

    [Fact]
    public void ByCategory_Luxury_ReturnsOnlyLuxury()
    {
        var items = ShopHelper.ByCategory(ShopHelper.ShopCategory.Luxury).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(ShopHelper.ShopCategory.Luxury, i.Category));
    }

    [Fact]
    public void ByCategory_CategoryCounts_SumToTotalItems()
    {
        int total =
            ShopHelper.ByCategory(ShopHelper.ShopCategory.PetConsumable).Count() +
            ShopHelper.ByCategory(ShopHelper.ShopCategory.PetCosmetic).Count()   +
            ShopHelper.ByCategory(ShopHelper.ShopCategory.Booster).Count()        +
            ShopHelper.ByCategory(ShopHelper.ShopCategory.GamblingPerk).Count()   +
            ShopHelper.ByCategory(ShopHelper.ShopCategory.Luxury).Count();
        Assert.Equal(ShopHelper.Items.Length, total);
    }

    // ── CategoryEmoji ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ShopHelper.ShopCategory.PetConsumable, "🐾")]
    [InlineData(ShopHelper.ShopCategory.PetCosmetic,   "✨")]
    [InlineData(ShopHelper.ShopCategory.Booster,       "📈")]
    [InlineData(ShopHelper.ShopCategory.GamblingPerk,  "🎲")]
    [InlineData(ShopHelper.ShopCategory.Luxury,        "💎")]
    [InlineData(ShopHelper.ShopCategory.All,           "🛒")]
    public void CategoryEmoji_ReturnsCorrectEmoji(ShopHelper.ShopCategory cat, string expected)
    {
        Assert.Equal(expected, ShopHelper.CategoryEmoji(cat));
    }

    // ── CategoryLabel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ShopHelper.ShopCategory.PetConsumable, "Pet Consumables")]
    [InlineData(ShopHelper.ShopCategory.PetCosmetic,   "Pet Cosmetics")]
    [InlineData(ShopHelper.ShopCategory.Booster,       "Boosters")]
    [InlineData(ShopHelper.ShopCategory.GamblingPerk,  "Gambling Perks")]
    [InlineData(ShopHelper.ShopCategory.Luxury,        "Luxury")]
    [InlineData(ShopHelper.ShopCategory.All,           "All Items")]
    public void CategoryLabel_ReturnsCorrectLabel(ShopHelper.ShopCategory cat, string expected)
    {
        Assert.Equal(expected, ShopHelper.CategoryLabel(cat));
    }

    // ── CategoryDescription ───────────────────────────────────────────────────

    [Theory]
    [InlineData(ShopHelper.ShopCategory.PetConsumable)]
    [InlineData(ShopHelper.ShopCategory.PetCosmetic)]
    [InlineData(ShopHelper.ShopCategory.Booster)]
    [InlineData(ShopHelper.ShopCategory.GamblingPerk)]
    [InlineData(ShopHelper.ShopCategory.Luxury)]
    [InlineData(ShopHelper.ShopCategory.All)]
    public void CategoryDescription_ReturnsNonEmptyString(ShopHelper.ShopCategory cat)
    {
        Assert.False(string.IsNullOrWhiteSpace(ShopHelper.CategoryDescription(cat)));
    }

    // ── CosmeticDisplay ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("title_dragon", "🐉 Dragon Tamer")]
    [InlineData("title_star",   "⭐ Star Collector")]
    [InlineData("title_shadow", "🌑 Shadow Walker")]
    [InlineData("aura_sparkle", "✨ Sparkle Aura")]
    [InlineData("aura_golden",  "🌟 Golden Aura")]
    [InlineData("aura_flame",   "🔥 Flame Aura")]
    public void CosmeticDisplay_KnownKey_ReturnsFormattedString(string key, string expected)
    {
        Assert.Equal(expected, ShopHelper.CosmeticDisplay(key));
    }

    [Fact]
    public void CosmeticDisplay_UnknownKey_ReturnsKeyItself()
    {
        Assert.Equal("nonexistent_xyz", ShopHelper.CosmeticDisplay("nonexistent_xyz"));
    }

    [Fact]
    public void CosmeticDisplay_Format_IsEmojiSpaceName()
    {
        // All cosmetic displays should be "{emoji} {name}"
        foreach (var item in ShopHelper.Items.Where(i => i.IsCosmetic))
        {
            string display = ShopHelper.CosmeticDisplay(item.Key);
            Assert.Equal($"{item.Emoji} {item.Name}", display);
        }
    }

    // ── Price ordering sanity ─────────────────────────────────────────────────

    [Fact]
    public void Items_FullRestore_MoreExpensiveThanKibble()
    {
        var kibble      = ShopHelper.Find("kibble")!;
        var fullRestore = ShopHelper.Find("full_restore")!;
        Assert.True(fullRestore.Price > kibble.Price);
    }

    [Fact]
    public void Items_LuxuryItems_MoreExpensiveThanBoosters()
    {
        decimal maxBooster = ShopHelper.ByCategory(ShopHelper.ShopCategory.Booster)
            .Max(i => i.Price);
        decimal minLuxury = ShopHelper.ByCategory(ShopHelper.ShopCategory.Luxury)
            .Where(i => i.Price != ShopHelper.EasterEggPrice)
            .Min(i => i.Price);
        Assert.True(minLuxury > maxBooster,
            $"Cheapest luxury ({minLuxury}) should exceed most expensive booster ({maxBooster})");
    }
}
