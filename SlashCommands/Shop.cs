using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Shop system — browse items, purchase them, manage inventory, and apply effects.
///
/// Subcommands:
///   /shop browse [category]  — paginated item listing
///   /shop buy   &lt;item&gt;       — purchase an item
///   /shop inventory          — show owned items and active effects
///   /shop use   &lt;item&gt;       — consume an item from inventory
/// </summary>
[Group("shop", "Browse the shop, buy items, and use your inventory.")]
public class Shop : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();
    private readonly Economy _eco = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourShop = new(255, 179, 71);
    private static readonly Color ColourSuccess = new(87, 242, 135);
    private static readonly Color ColourError = new(237, 66, 69);
    private static readonly Color ColourInfo = new(88, 101, 242);
    private static readonly Color ColourGold = new(255, 215, 0);


    [SlashCommand("browse", "Browse shop items by category.")]
    [EnabledInDm(false)]
    public async Task HandleBrowseAsync(
        [Choice("All",              "all")]
        [Choice("Pet Consumables",  "PetConsumable")]
        [Choice("Pet Cosmetics",    "PetCosmetic")]
        [Choice("Boosters",         "Booster")]
        [Choice("Gambling Perks",   "GamblingPerk")]
        string category = "all")
    {
        await DeferAsync();

        var cat = category == "all"
            ? ShopHelper.ShopCategory.All
            : Enum.Parse<ShopHelper.ShopCategory>(category);

        var items = ShopHelper.ByCategory(cat).ToList();

        if (cat == ShopHelper.ShopCategory.All)
        {
            // Summary view — one field per category
            var embed = new EmbedBuilder()
                .WithTitle("🛒  The Shop")
                .WithColor(ColourShop)
                .WithDescription(
                    "Browse by category or use `/shop buy <item>` to purchase directly.\n" +
                    "Use `/shop inventory` to see what you own.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp();

            foreach (ShopHelper.ShopCategory c in Enum.GetValues<ShopHelper.ShopCategory>())
            {
                if (c == ShopHelper.ShopCategory.All) continue;

                var sb = new StringBuilder();
                foreach (var i in ShopHelper.ByCategory(c))
                    sb.AppendLine($"{i.Emoji} **{i.Name}** — {CreditHelper.Format(i.Price)}");

                embed.AddField(
                    $"{ShopHelper.CategoryEmoji(c)}  {ShopHelper.CategoryLabel(c)}",
                    sb.ToString(),
                    inline: false);
            }

            await FollowupAsync(embed: embed.Build());
            return;
        }

        // Category detail view
        var detailEmbed = new EmbedBuilder()
            .WithTitle($"{ShopHelper.CategoryEmoji(cat)}  {ShopHelper.CategoryLabel(cat)}")
            .WithColor(ColourShop)
            .WithDescription(ShopHelper.CategoryDescription(cat))
            .WithFooter($"Use /shop buy <item> to purchase • {Username}", AvatarUrl)
            .WithCurrentTimestamp();

        foreach (var item in items)
        {
            string effectLine = item.DurationMinutes.HasValue
                ? $"{item.Description} *(lasts {item.DurationMinutes} min)*"
                : item.StackCount > 1
                    ? $"{item.Description} *({item.StackCount} uses)*"
                    : item.Description;

            detailEmbed.AddField(
                $"{item.Emoji}  {item.Name}  —  {CreditHelper.Format(item.Price)}",
                effectLine,
                inline: false);
        }

        await FollowupAsync(embed: detailEmbed.Build());
    }


    [SlashCommand("buy", "Purchase an item from the shop.")]
    [EnabledInDm(false)]
    public async Task HandleBuyAsync(
        [Summary("item", "The item you want to buy.")]
        [Autocomplete(typeof(ShopBuyAutocompleteHandler))]
        string itemKey,
        [Summary("quantity", "How many to buy (default 1).")]
        [MinValue(1), MaxValue(25)] int quantity = 1)
    {
        await DeferAsync();

        var item = ShopHelper.Find(itemKey);
        if (item is null)
        {
            await ErrorAsync($"**{itemKey}** isn't a valid shop item. Use `/shop browse` to see what's available.");
            return;
        }

        long balance = _eco.GetBalance(UserId, ServerId);

        long totalCost = item.Price * quantity;

        if (balance < totalCost)
        {
            await ErrorAsync(
                $"You can't afford **{quantity}× {item.Name}**!\n\n" +
                $"Cost: {CreditHelper.Format(totalCost)}{(quantity > 1 ? $" ({quantity}× {CreditHelper.Format(item.Price)})" : "")}\n" +
                $"Your balance: {CreditHelper.Format(balance)}");
            return;
        }

        long newBalance = _eco.DeductCredits(UserId, ServerId, totalCost, "shop_purchase");

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddToInventory",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@ItemKey",  item.Key),
            new SqlParameter("@Quantity", quantity)
        ]);

        string qtyLabel = quantity > 1 ? $"{quantity}×  " : "";
        var embed = new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Purchased: {qtyLabel}{item.Name}")
            .WithColor(ColourSuccess)
            .WithDescription(item.Effect)
            .AddField("Quantity", $"×{quantity}", inline: true)
            .AddField("Cost", CreditHelper.Format(totalCost), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter($"Use /shop use {item.Key} to apply it • {Username}", AvatarUrl)
            .WithCurrentTimestamp();

        await FollowupAsync(embed: embed.Build());
    }


    [SlashCommand("inventory", "Show your owned items and active effects.")]
    [EnabledInDm(false)]
    public async Task HandleInventoryAsync()
    {
        await DeferAsync();

        var invDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetUserInventory",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        var effDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllActiveEffects",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        var embed = new EmbedBuilder()
            .WithTitle($"🎒  {Username}'s Inventory")
            .WithColor(ColourGold)
            .WithFooter($"Use /shop use <item> to use an item", AvatarUrl)
            .WithCurrentTimestamp();

        if (invDt.Rows.Count == 0 && effDt.Rows.Count == 0)
        {
            embed.WithDescription("Your inventory is empty. Use `/shop browse` to find something!");
            await FollowupAsync(embed: embed.Build());
            return;
        }

        // Owned items
        if (invDt.Rows.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (DataRow row in invDt.Rows)
            {
                string key = row["ItemKey"].ToString()!;
                int qty = int.Parse(row["Quantity"].ToString()!);
                var meta = ShopHelper.Find(key);
                string name = meta is not null ? $"{meta.Emoji} {meta.Name}" : key;
                sb.AppendLine($"{name} — **×{qty}**");
            }
            embed.AddField("🎒  Items", sb.ToString(), inline: false);
        }

        // Active effects
        if (effDt.Rows.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (DataRow row in effDt.Rows)
            {
                string key = row["EffectKey"].ToString()!;
                int stack = int.Parse(row["StackCount"].ToString()!);
                var meta = ShopHelper.Find(key);
                string name = meta is not null ? $"{meta.Emoji} {meta.Name}" : key;
                string expiry = "";

                if (row["ExpiresAt"] != DBNull.Value &&
                    DateTime.TryParse(row["ExpiresAt"].ToString(), out var exp))
                {
                    long unix = new DateTimeOffset(exp, TimeSpan.Zero).ToUnixTimeSeconds();
                    expiry = $" — expires <t:{unix}:R>";
                }
                else if (stack > 1)
                {
                    expiry = $" — **{stack} uses left**";
                }

                sb.AppendLine($"{name}{expiry}");
            }
            embed.AddField("⚡  Active Effects", sb.ToString(), inline: false);
        }

        await FollowupAsync(embed: embed.Build());
    }


    [SlashCommand("use", "Use an item from your inventory.")]
    [EnabledInDm(false)]
    public async Task HandleUseAsync(
        [Summary("item", "The item you want to use.")]
        [Autocomplete(typeof(ShopUseAutocompleteHandler))]
        string itemKey)
    {
        await DeferAsync();

        var item = ShopHelper.Find(itemKey);
        if (item is null)
        {
            await ErrorAsync($"**{itemKey}** isn't a valid item key.");
            return;
        }

        // Verify ownership before dispatching
        if (!ShopHelper.HasItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync($"You don't own **{item.Name}**. Buy it first with `/shop buy {item.Key}`.");
            return;
        }

        // Dispatch to the appropriate handler based on category / key
        if (item.IsCosmetic)
        {
            await UseCosmetic(item);
            return;
        }

        switch (item.Key)
        {
            case "kibble": await UsePetStat(item, hunger: 30); break;
            case "feast": await UsePetStat(item, hunger: 60, happiness: 15); break;
            case "treat": await UsePetStat(item, happiness: 25); break;
            case "luxury_toy": await UsePetStat(item, happiness: 40); break;
            case "energy_drink": await UsePetStat(item, energy: 50); break;
            case "grooming_kit": await UsePetStat(item, hygiene: 50); break;
            case "full_restore": await UseFullRestore(item); break;
            case "revive": await UseRevive(item); break;

            case "xp_boost":
                await UseActiveEffect(item, DateTime.UtcNow.AddMinutes(item.DurationMinutes!.Value), stackCount: 1);
                break;
            case "mega_bet":
                await UseActiveEffect(item, DateTime.UtcNow.AddMinutes(item.DurationMinutes!.Value), stackCount: 1);
                break;
            case "explore_boost":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "daily_boost":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "work_boost":
                await UseActiveEffect(item, expiresAt: null, stackCount: item.StackCount);
                break;

            case "bk_shield":
            case "insurance":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "cd_reset":
                await UseCooldownReset(item);
                break;

            default:
                await ErrorAsync($"**{item.Name}** doesn't have a use action yet.");
                break;
        }
    }

    // =========================================================================
    // Use Handlers
    // =========================================================================

    /// <summary>Applies stat deltas to the user's active pet.</summary>
    private async Task UsePetStat(ShopHelper.ShopItem item,
        int hunger = 0, int happiness = 0, int energy = 0, int hygiene = 0)
    {
        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item — it may have already been used.");
            return;
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int newHunger = Math.Min(100, int.Parse(row["Hunger"].ToString()!) + hunger);
        int newHappiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + happiness);
        int newEnergy = Math.Min(100, int.Parse(row["Energy"].ToString()!) + energy);
        int newHygiene = Math.Min(100, int.Parse(row["Hygiene"].ToString()!) + hygiene);
        int xp = int.Parse(row["XP"].ToString()!);
        string petName = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        newHunger),
            new SqlParameter("@Happiness",     newHappiness),
            new SqlParameter("@Energy",        newEnergy),
            new SqlParameter("@Hygiene",       newHygiene),
            new SqlParameter("@XP",            xp),
            new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(newHunger, newHappiness, newEnergy)),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        var sb = new StringBuilder();
        if (hunger != 0) sb.AppendLine($"🍽️ Hunger    {PetHelper.StatBar(newHunger)}    **{newHunger}/100**");
        if (happiness != 0) sb.AppendLine($"😊 Happiness {PetHelper.StatBar(newHappiness)} **{newHappiness}/100**");
        if (energy != 0) sb.AppendLine($"⚡ Energy    {PetHelper.StatBar(newEnergy)}    **{newEnergy}/100**");
        if (hygiene != 0) sb.AppendLine($"🧼 Hygiene   {PetHelper.StatBar(newHygiene)}   **{newHygiene}/100**");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Used {item.Name} on {petName}!")
            .WithColor(ColourSuccess)
            .WithDescription(sb.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Full Restore — maxes all stats on the active pet.</summary>
    private async Task UseFullRestore(ShopHelper.ShopItem item)
    {
        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int xp = int.Parse(row["XP"].ToString()!);
        string name = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        100),
            new SqlParameter("@Happiness",     100),
            new SqlParameter("@Energy",        100),
            new SqlParameter("@Hygiene",       100),
            new SqlParameter("@XP",            xp),
            new SqlParameter("@IsHibernating", false),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"💊  {name} is fully restored!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"All of **{name}**'s stats have been maxed out!\n\n" +
                $"🍽️ Hunger    {PetHelper.StatBar(100)} **100/100**\n" +
                $"😊 Happiness {PetHelper.StatBar(100)} **100/100**\n" +
                $"⚡ Energy    {PetHelper.StatBar(100)} **100/100**\n" +
                $"🧼 Hygiene   {PetHelper.StatBar(100)} **100/100**")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Revive — wakes a hibernating pet and restores all stats to 50.</summary>
    private async Task UseRevive(ShopHelper.ShopItem item)
    {
        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        bool hibernating = bool.TryParse(row["IsHibernating"].ToString(), out bool h) && h;
        if (!hibernating)
        {
            await ErrorAsync("Your pet isn't hibernating — a Revive Potion can only be used on a hibernating pet.");
            return;
        }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int xp = int.Parse(row["XP"].ToString()!);
        string name = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        50),
            new SqlParameter("@Happiness",     50),
            new SqlParameter("@Energy",        50),
            new SqlParameter("@Hygiene",       50),
            new SqlParameter("@XP",            xp),
            new SqlParameter("@IsHibernating", false),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"💫  {name} has been revived!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{name}** is awake and ready to go again!\n\n" +
                $"🍽️ Hunger    {PetHelper.StatBar(50)} **50/100**\n" +
                $"😊 Happiness {PetHelper.StatBar(50)} **50/100**\n" +
                $"⚡ Energy    {PetHelper.StatBar(50)} **50/100**\n" +
                $"🧼 Hygiene   {PetHelper.StatBar(50)} **50/100**")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Cosmetics — applies a title or aura to the user's active pet.</summary>
    private async Task UseCosmetic(ShopHelper.ShopItem item)
    {
        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        string petName = row["Name"].ToString()!;
        string cosType = item.CosmeticType!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "SetPetCosmetic",
        [
            new SqlParameter("@PetID",        petId),
            new SqlParameter("@CosmeticType", cosType),
            new SqlParameter("@CosmeticKey",  item.Key)
        ]);

        string slotLabel = cosType == "title" ? "Title" : "Aura";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Cosmetic Applied!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{petName}** now wears the **{item.Emoji} {item.Name}** {slotLabel.ToLower()}!\n\n" +
                $"It'll show up on `/petcard`.\n" +
                $"You can replace it anytime by using another {slotLabel.ToLower()} cosmetic.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>
    /// Writes an active effect to UserActiveEffects (and consumes the inventory item).
    /// Used for boosters and gambling perks that aren't instant.
    /// </summary>
    private async Task UseActiveEffect(ShopHelper.ShopItem item, DateTime? expiresAt, int stackCount)
    {
        // Check if already active — refuse stacking timed effects, allow stack increment for work_boost
        if (ShopHelper.HasActiveEffect(UserId, ServerId, item.Key))
        {
            if (item.Key == "work_boost")
            {
                // Stack on top — increment by StackCount
                int current = ShopHelper.GetEffectStack(UserId, ServerId, item.Key);
                if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
                {
                    await ErrorAsync("Could not consume item.");
                    return;
                }
                ShopHelper.SetActiveEffect(UserId, ServerId, item.Key, expiresAt, current + stackCount);
                await FollowupAsync(embed: EffectEmbed(item, $"Stacked! You now have **{current + stackCount}** work boost uses remaining.").Build());
                return;
            }

            string expNote = item.DurationMinutes.HasValue
                ? " — it will replace the current timer"
                : " — it will refresh it";
            if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
            {
                await ErrorAsync("Could not consume item.");
                return;
            }
            ShopHelper.SetActiveEffect(UserId, ServerId, item.Key, expiresAt, stackCount);
            await FollowupAsync(embed: EffectEmbed(item, $"Effect refreshed{expNote}.").Build());
            return;
        }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        ShopHelper.SetActiveEffect(UserId, ServerId, item.Key, expiresAt, stackCount);

        string durationNote = expiresAt.HasValue
            ? $"Active for **{item.DurationMinutes} minutes**."
            : stackCount > 1
                ? $"Lasts for **{stackCount} uses**."
                : "Active until triggered.";

        await FollowupAsync(embed: EffectEmbed(item, durationNote).Build());
    }

    /// <summary>Cooldown Eraser — clears all gambling cooldowns immediately.</summary>
    private async Task UseCooldownReset(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        Gambling.ClearUserCooldowns(UserId);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"⏩  Cooldowns Reset!")
            .WithColor(ColourSuccess)
            .WithDescription("All your gambling command cooldowns have been cleared. Go again!")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    private EmbedBuilder EffectEmbed(ShopHelper.ShopItem item, string note) =>
        new EmbedBuilder()
            .WithTitle($"{item.Emoji}  {item.Name} — Active!")
            .WithColor(ColourInfo)
            .WithDescription($"{item.Effect}\n\n{note}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Shop", message, Username).Build());


    private (DataRow? row, string? error) GetActivePet()
    {
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", UserId)]);

        if (dt.Rows.Count == 0)
            return (null, "You don't have an active pet. Use `/adopt` to get one or `/setactivepet` to switch.");

        return (dt.Rows[0], null);
    }
}


/// <summary>Suggests all shop items matching the current input.</summary>
public class ShopBuyAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        string current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var results = ShopHelper.Items
            .Where(i => i.Name.Contains(current, StringComparison.OrdinalIgnoreCase) ||
                        i.Key.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(i => new AutocompleteResult(
                $"{i.Emoji} {i.Name} — {CreditHelper.Format(i.Price)}", i.Key));

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}

/// <summary>Suggests only items the user currently owns.</summary>
public class ShopUseAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        string userId = autocompleteInteraction.User.Id.ToString();
        string serverId = autocompleteInteraction.GuildId?.ToString() ?? "DM";
        string current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        try
        {
            var dt = new StoredProcedure().Select(
                Constants.Constants.discordBotConnStr, "GetUserInventory",
            [
                new SqlParameter("@UserID",   userId),
                new SqlParameter("@ServerID", serverId)
            ]);

            var ownedKeys = dt.Rows.Cast<DataRow>()
                .Select(r => r["ItemKey"].ToString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var results = ShopHelper.Items
                .Where(i => ownedKeys.Contains(i.Key) &&
                            (i.Name.Contains(current, StringComparison.OrdinalIgnoreCase) ||
                             i.Key.Contains(current, StringComparison.OrdinalIgnoreCase)))
                .Take(25)
                .Select(i => new AutocompleteResult($"{i.Emoji} {i.Name}", i.Key));

            return Task.FromResult(AutocompletionResult.FromSuccess(results));
        }
        catch
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }
    }
}
