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

    // ── /shop browse ──────────────────────────────────────────────────────────

    [SlashCommand("browse", "Browse shop items by category.")]
    [EnabledInDm(false)]
    public async Task HandleBrowseAsync(
        [Choice("All",              "all")]
        [Choice("Pet Consumables",  "PetConsumable")]
        [Choice("Pet Cosmetics",    "PetCosmetic")]
        [Choice("Boosters",         "Booster")]
        [Choice("Gambling Perks",   "GamblingPerk")]
        [Choice("Luxury",           "Luxury")]
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

    // ── /shop buy ─────────────────────────────────────────────────────────────

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

        decimal balance = _eco.GetBalance(UserId, ServerId);

        decimal totalCost = (decimal)item.Price * quantity;

        if (balance < totalCost)
        {
            await ErrorAsync(
                $"You can't afford **{quantity}× {item.Name}**!\n\n" +
                $"Cost: {CreditHelper.Format(totalCost)}{(quantity > 1 ? $" ({quantity}× {CreditHelper.Format(item.Price)})" : "")}\n" +
                $"Your balance: {CreditHelper.Format(balance)}");
            return;
        }

        decimal newBalance = _eco.DeductCredits(UserId, ServerId, totalCost, "shop_purchase");

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

    // ── /shop inventory ───────────────────────────────────────────────────────

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

    // ── /shop use ─────────────────────────────────────────────────────────────

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
            // ── Pet consumables ───────────────────────────────────────────────
            case "kibble": await UsePetStat(item, hunger: 30); break;
            case "feast": await UsePetStat(item, hunger: 60, happiness: 15); break;
            case "treat": await UsePetStat(item, happiness: 25); break;
            case "luxury_toy": await UsePetStat(item, happiness: 40); break;
            case "energy_drink": await UsePetStat(item, energy: 50); break;
            case "grooming_kit": await UsePetStat(item, hygiene: 50); break;
            case "full_restore": await UseFullRestore(item); break;
            case "revive": await UseRevive(item); break;

            // ── Boosters — timed / stack active effects ────────────────────
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

            // ── Gambling perks ────────────────────────────────────────────────
            case "bk_shield":
            case "insurance":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "comeback_chip":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "hot_streak":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;
            case "cd_reset":
                await UseCooldownReset(item);
                break;
            case "impregnate_bot_owner":
                await UseImpregnator(item);
                break;
            case "destroy_bot_owner_baby":
                await RemoveImpregnator(item);
                break;

            // ── Gambling perks — new shop items ───────────────────────────────
            case "chaos_card":
                await UseActiveEffect(item, expiresAt: null, stackCount: 1);
                break;

            // ── Luxury — timed active effects ─────────────────────────────────
            case "golden_ticket":
            case "golden_ticket_ii":
                await UseGoldenTicket(item);
                break;
            case "tax_evasion":
                await UseActiveEffect(item, expiresAt: null, stackCount: item.StackCount);
                break;

            // ── Luxury — immediate / one-shot ─────────────────────────────────
            case "interest_boost":
                await UseInterestBoost(item);
                break;
            case "bank_heist":
                await UseBankHeist(item);
                break;
            case "market_crash":
                await UseMarketCrash(item);
                break;
            case "jackpot_seed":
                await UseJackpotSeed(item);
                break;
            case "prestige_reset":
                await UsePrestigeReset(item);
                break;
            case "wealth_flex":
                await UseWealthFlex(item);
                break;
            case "balance_transfer":
                await UseBalanceTransfer(item);
                break;

            // ── Luxury — server-wide nukes (confirmation required) ────────────
            case "economy_nuke":
                await UseEconomyNuke(item);
                break;
            case "server_reset":
                await UseServerReset(item);
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

    // ── Impregnator ───────────────────────────────────────────────────────────

    private const ulong BotOwnerId = 171369791486033920UL;

    /// <summary>
    /// Impregnate Bot Owner easter egg.
    /// Immediately: DMs both parties, posts in chat, writes event row.
    /// After 9 months: BotHost fires birth DMs, reimburses 1T, starts daily 1B child support.
    /// </summary>
    private async Task UseImpregnator(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        DateTime birthAt = DateTime.UtcNow.AddMonths(9);

        // Persist event
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "CreatePregnancy",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@BirthAt",  birthAt)
        ]);

        long birthUnix = new DateTimeOffset(birthAt, TimeSpan.Zero).ToUnixTimeSeconds();

        // 1. DM bot owner — they have been impregnated
        try
        {
            var owner = await Context.Client.GetUserAsync(BotOwnerId);
            if (owner is not null)
            {
                var ownerDm = await owner.CreateDMChannelAsync();
                await ownerDm.SendMessageAsync(
                    $"🍼  **Oh no…**\n\n" +
                    $"**{Username}** has impregnated you! You are now expecting a baby.\n" +
                    $"Due date: <t:{birthUnix}:F> (<t:{birthUnix}:R>).\n\n" +
                    $"Congratulations? 🐣");
            }
        }
        catch { /* DMs closed */ }

        // 2. DM the buyer — they succeeded
        try
        {
            var buyerDm = await Context.User.CreateDMChannelAsync();
            await buyerDm.SendMessageAsync(
                $"🤰  **Mission accomplished.**\n\n" +
                $"You have successfully impregnated the bot owner!\n" +
                $"The baby is due <t:{birthUnix}:R>.\n\n" +
                $"Keep your DMs open — you'll receive a very special delivery in 9 months. 👶\n" +
                $"*(Child support will also be discussed at that time.)*");
        }
        catch { /* DMs closed */ }

        // 3. Public chat message
        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🤖🍼  PREGANTE!")
            .WithColor(new Color(255, 105, 180))
            .WithDescription(
                $"**{Context.User.Mention}** has impregnated <@{BotOwnerId}>!\n\n" +
                $"A baby is on the way. Due date: <t:{birthUnix}:F>\n" +
                $"Both parties have been notified. 🐣")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    private async Task RemoveImpregnator(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }
        DataTable dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePregnancy",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await ErrorAsync("You don't have an active pregnancy to clear.");
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ClearPregnancy",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🍼  Baby Destroyed")
            .WithColor(ColourError)
            .WithDescription(
                $"You have used the **{item.Name}** and destroyed the bot owner's baby.\n" +
                $"The bot owner has been notified. 😢\n" +
                "Enjoy the consequences of your actions!")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Embed helpers ─────────────────────────────────────────────────────────

    private EmbedBuilder EffectEmbed(ShopHelper.ShopItem item, string note) =>
        new EmbedBuilder()
            .WithTitle($"{item.Emoji}  {item.Name} — Active!")
            .WithColor(ColourInfo)
            .WithDescription($"{item.Effect}\n\n{note}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Shop", message, Username).Build());

    // ── Luxury handlers ───────────────────────────────────────────────────────

    /// <summary>Golden Ticket / Golden Ticket II — timed income multiplier.</summary>
    private async Task UseGoldenTicket(ShopHelper.ShopItem item)
    {
        // Don't allow stacking GT and GT-II
        if (ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket") ||
            ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket_ii"))
        {
            await ErrorAsync("A Golden Ticket effect is already active. Wait for it to expire before using another.");
            return;
        }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(item.DurationMinutes!.Value);
        ShopHelper.SetActiveEffect(UserId, ServerId, item.Key, expiresAt, 1);

        string multi = item.Key == "golden_ticket_ii" ? "3×" : "2×";
        int hours = item.DurationMinutes!.Value / 60;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  {item.Name} — Active!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"All credit income is now **{multi}** for the next **{hours} hour{(hours == 1 ? "" : "s")}**.\n\n" +
                $"Applies to: `/daily`, `/work`, gambling payouts, and fishing rewards.\n\n" +
                $"-# Expires <t:{new DateTimeOffset(expiresAt).ToUnixTimeSeconds()}:R>")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Interest Boost — flat 250M credit grant.</summary>
    private async Task UseInterestBoost(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        decimal payout = 250_000_000m;
        decimal newBalance = _eco.AddCredits(UserId, ServerId, payout, "interest_boost");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Interest Paid!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{CreditHelper.Format(payout)}** has been deposited into your account.\n\n" +
                $"Balance: {CreditHelper.Format(newBalance)}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Bank Heist — steal 1–5% of a random other user's balance.</summary>
    private async Task UseBankHeist(ShopHelper.ShopItem item)
    {
        // 48-hour cooldown check
        string cooldownKey = $"bank_heist:{UserId}:{ServerId}";
        if (ShopHelper.HasActiveEffect(UserId, ServerId, cooldownKey))
        {
            await ErrorAsync("You're on a 48-hour cooldown from your last heist. Lay low for a while.");
            return;
        }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        // 30% fail chance
        if (Random.Shared.NextDouble() < 0.30)
        {
            ShopHelper.SetActiveEffect(UserId, ServerId, cooldownKey,
                DateTime.UtcNow.AddHours(48), 1);
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"{item.Emoji}  Heist Failed!")
                .WithColor(ColourError)
                .WithDescription("The security was too tight. You got away clean but walked out empty-handed.\n\n-# 48-hour cooldown applied.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        // Pick a random other user from the leaderboard
        var lbDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCreditLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        var targets = lbDt.Rows.Cast<System.Data.DataRow>()
            .Where(r => r["UserID"]?.ToString() != UserId)
            .ToList();

        if (targets.Count == 0)
        {
            await ErrorAsync("No other users found to heist. The server is too empty.");
            return;
        }

        var target = targets[Random.Shared.Next(targets.Count)];
        string tId = target["UserID"].ToString()!;
        string tName = target["Username"].ToString()!;
        decimal tBal = decimal.Parse(target["Balance"].ToString()!);

        double pct = 0.01 + Random.Shared.NextDouble() * 0.04; // 1–5%
        decimal stolen = Math.Floor(tBal * (decimal)pct);

        if (stolen <= 0)
        {
            await ErrorAsync("Target has nothing worth stealing.");
            return;
        }

        _eco.DeductCredits(tId, ServerId, stolen, "bank_heist_victim");
        decimal newBalance = _eco.AddCredits(UserId, ServerId, stolen, "bank_heist_win");

        ShopHelper.SetActiveEffect(UserId, ServerId, cooldownKey,
            DateTime.UtcNow.AddHours(48), 1);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Heist Successful!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"You robbed **{tName}** of **{CreditHelper.Format(stolen)}** ({pct * 100:F1}% of their balance).\n\n" +
                $"Balance: {CreditHelper.Format(newBalance)}\n\n" +
                $"-# 48-hour cooldown applied.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Market Crash — drops all stock prices 20–40%.</summary>
    private async Task UseMarketCrash(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllStocks", []);
        int count = 0;

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string ticker = row["Ticker"].ToString()!;
            decimal price = decimal.Parse(row["Price"].ToString()!);
            double dropPct = 0.20 + Random.Shared.NextDouble() * 0.20; // 20–40%
            decimal newPrice = Math.Max(1m, Math.Floor(price * (decimal)(1.0 - dropPct)));

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ApplyStockTick",
            [
                new SqlParameter("@Ticker",   ticker),
                new SqlParameter("@NewPrice", newPrice)
            ]);
            count++;
        }

        // Announce in default channel
        try
        {
            var guild = Context.Guild;
            if (guild?.DefaultChannel is not null)
                await guild.DefaultChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("📉  Market Crash!")
                    .WithColor(ColourError)
                    .WithDescription(
                        $"{Context.User.Mention} triggered a **Market Crash**!\n\n" +
                        $"All {count} stocks have dropped **20–40%**. Check `/stock market` for current prices.")
                    .WithCurrentTimestamp()
                    .Build());
        }
        catch { }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Market Crash Triggered!")
            .WithColor(ColourSuccess)
            .WithDescription($"**{count}** stocks have been crashed by 20–40%. The announcement has been posted.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Jackpot Seed — injects 100B into the passive jackpot pool.</summary>
    private async Task UseJackpotSeed(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        decimal seed = 100_000_000_000m;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "FeedPassiveJackpot",
        [
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@Amount",   seed)
        ]);

        var potDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPassiveJackpot",
            [new SqlParameter("@ServerID", ServerId)]);
        decimal newPool = potDt.Rows.Count > 0
            ? decimal.Parse(potDt.Rows[0]["Pool"].ToString()!)
            : seed;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Jackpot Seeded!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{CreditHelper.Format(seed)}** has been injected into the server passive jackpot.\n\n" +
                $"New pool total: **{CreditHelper.Format(newPool)}**\n\n" +
                $"*Win it on slots or scratch card (0.5% chance per play).*")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Prestige Reset — zeros LifetimeEarned and refunds 100B.</summary>
    private async Task UsePrestigeReset(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ResetLifetimeEarned",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        decimal refund = 100_000_000_000m;
        decimal newBalance = _eco.AddCredits(UserId, ServerId, refund, "prestige_reset_refund");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Prestige Reset!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"Your **LifetimeEarned** has been reset to 0. You're back to 🪨 Broke.\n\n" +
                $"Refund of **{CreditHelper.Format(refund)}** applied.\n" +
                $"Balance: {CreditHelper.Format(newBalance)}\n\n" +
                $"*Use `/prestige` to start climbing again.*")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Wealth Flex — burns 1T, posts server-wide announcement.</summary>
    private async Task UseWealthFlex(ShopHelper.ShopItem item)
    {
        decimal burnAmount = 1_000_000_000_000m;
        decimal balance = _eco.GetBalance(UserId, ServerId);

        if (balance < burnAmount)
        {
            await ErrorAsync($"You need at least {CreditHelper.Format(burnAmount)} to use Wealth Flex. Current balance: {CreditHelper.Format(balance)}.");
            return;
        }

        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        decimal newBalance = _eco.DeductCredits(UserId, ServerId, burnAmount, "wealth_flex_burn");

        try
        {
            var guild = Context.Guild;
            if (guild?.DefaultChannel is not null)
                await guild.DefaultChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("💸  Wealth Flex!")
                    .WithColor(new Color(255, 215, 0))
                    .WithDescription(
                        $"{Context.User.Mention} just **burned {CreditHelper.Format(burnAmount)}** for absolutely no reason.\n\n" +
                        $"*The ultimate flex. Absolutely nothing was gained.*")
                    .WithCurrentTimestamp()
                    .Build());
        }
        catch { }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Wealth Flex!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{CreditHelper.Format(burnAmount)}** has been permanently destroyed.\n" +
                $"Balance: {CreditHelper.Format(newBalance)}\n\n" +
                $"The server has been notified of your sacrifice.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Balance Transfer — moves up to 10% of balance to any user.</summary>
    private async Task UseBalanceTransfer(ShopHelper.ShopItem item)
    {
        await ErrorAsync("To use Balance Transfer, run: `/shop use balance_transfer @user`\n\n*(This item requires a target — re-use the command and mention a user.)*");
    }

    /// <summary>Economy Nuke — halves every user's balance in the server.</summary>
    private async Task UseEconomyNuke(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "HalveAllBalances",
            [new SqlParameter("@ServerID", ServerId)]);

        try
        {
            var guild = Context.Guild;
            if (guild?.DefaultChannel is not null)
                await guild.DefaultChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("☢️  Economy Nuke!")
                    .WithColor(ColourError)
                    .WithDescription(
                        $"{Context.User.Mention} detonated an **Economy Nuke**!\n\n" +
                        $"**Every user's balance has been halved.** Check `/balance` to see the damage.")
                    .WithCurrentTimestamp()
                    .Build());
        }
        catch { }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Economy Nuke Detonated!")
            .WithColor(ColourSuccess)
            .WithDescription("Every user's balance in this server has been halved. The announcement has been posted.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>Server Reset — zeros every user's balance.</summary>
    private async Task UseServerReset(ShopHelper.ShopItem item)
    {
        if (!ShopHelper.ConsumeItem(UserId, ServerId, item.Key))
        {
            await ErrorAsync("Could not consume item.");
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ZeroAllBalances",
            [new SqlParameter("@ServerID", ServerId)]);

        try
        {
            var guild = Context.Guild;
            if (guild?.DefaultChannel is not null)
                await guild.DefaultChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("💥  Server Economy Reset!")
                    .WithColor(ColourError)
                    .WithDescription(
                        $"{Context.User.Mention} used a **Server Economy Reset**.\n\n" +
                        $"**Every user's balance has been set to 0.** Prestige ranks are preserved.\n" +
                        $"*Time to start over.*")
                    .WithCurrentTimestamp()
                    .Build());
        }
        catch { }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{item.Emoji}  Server Economy Reset!")
            .WithColor(ColourSuccess)
            .WithDescription("Every balance in this server has been zeroed. Prestige ranks are intact.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Pet helper (mirrors Pet.cs pattern) ──────────────────────────────────

    private (DataRow? row, string? error) GetActivePet()
    {
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", UserId)]);

        if (dt.Rows.Count == 0)
            return (null, "You don't have an active pet. Use `/adopt` to get one or `/setactivepet` to switch.");

        return (dt.Rows[0], null);
    }
}

// ── Autocomplete Handlers ─────────────────────────────────────────────────────

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

        var all = ShopHelper.Items
            .Where(i => i.Price != 9223372036854775807); // exclude unobtainable easter eggs

        // If no input, return first 25 across all categories (sorted by price asc)
        // If input given, match against name/key/category name
        var filtered = string.IsNullOrWhiteSpace(current)
            ? all.OrderBy(i => (int)i.Category).ThenBy(i => i.Price)
            : all.Where(i =>
                i.Name.Contains(current, StringComparison.OrdinalIgnoreCase) ||
                i.Key.Contains(current, StringComparison.OrdinalIgnoreCase) ||
                i.Category.ToString().Contains(current, StringComparison.OrdinalIgnoreCase));

        var results = filtered
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