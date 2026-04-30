using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Pet breeding system.
/// /breed   — breed two of your pets together to produce an egg (24h hatch timer).
/// /eggs    — view your pending eggs and time remaining.
/// /hatchegg — hatch a ready egg into a new pet.
/// Requirements: both pets must be level 10+, same species, not hibernating.
/// </summary>
public class Breeding : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourSuccess = EmbedColors.Green;
    private static readonly Color ColourWarn = EmbedColors.Amber;
    private static readonly Color ColourRed = EmbedColors.Red;
    private static readonly Color ColourGold = EmbedColors.Gold;

    private const int MinBreedLevel = 10;   // both pets must be at least this level
    private const int MaxEggs = 3;    // max unhatched eggs at once

    // ── /breed ────────────────────────────────────────────────────────────────

    [SlashCommand("breed", "Breed two of your pets to produce an egg!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBreedAsync(
        [Summary("pet1_id", "PetID of the first parent.")] int pet1Id,
        [Summary("pet2_id", "PetID of the second parent.")] int pet2Id)
    {
        await DeferAsync();

        if (pet1Id == pet2Id)
        {
            await ErrorAsync("A pet cannot breed with itself.");
            return;
        }

        // ── Load both pets ─────────────────────────────────────────────────────
        var p1Dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetByID",
        [
            new SqlParameter("@PetID",  pet1Id),
            new SqlParameter("@UserID", UserId)
        ]);
        var p2Dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetByID",
        [
            new SqlParameter("@PetID",  pet2Id),
            new SqlParameter("@UserID", UserId)
        ]);

        if (p1Dt.Rows.Count == 0) { await ErrorAsync($"Pet #{pet1Id} not found or doesn't belong to you."); return; }
        if (p2Dt.Rows.Count == 0) { await ErrorAsync($"Pet #{pet2Id} not found or doesn't belong to you."); return; }

        var p1 = p1Dt.Rows[0];
        var p2 = p2Dt.Rows[0];

        // ── Validation ─────────────────────────────────────────────────────────
        string species1 = p1["Species"].ToString()!;
        string species2 = p2["Species"].ToString()!;

        if (species1 != species2)
        {
            await ErrorAsync($"Both pets must be the same species to breed.\n**{p1["Name"]}** is a {species1}, **{p2["Name"]}** is a {species2}.");
            return;
        }

        int xp1 = int.Parse(p1["XP"].ToString()!);
        int xp2 = int.Parse(p2["XP"].ToString()!);
        int level1 = PetHelper.LevelFromXp(xp1);
        int level2 = PetHelper.LevelFromXp(xp2);

        if (level1 < MinBreedLevel)
        {
            await ErrorAsync($"**{p1["Name"]}** is level {level1}. Both pets must be at least level {MinBreedLevel} to breed.");
            return;
        }
        if (level2 < MinBreedLevel)
        {
            await ErrorAsync($"**{p2["Name"]}** is level {level2}. Both pets must be at least level {MinBreedLevel} to breed.");
            return;
        }

        bool hib1 = p1["IsHibernating"].ToString() is "1" or "True";
        bool hib2 = p2["IsHibernating"].ToString() is "1" or "True";

        if (hib1) { await ErrorAsync($"**{p1["Name"]}** is hibernating and can't breed right now."); return; }
        if (hib2) { await ErrorAsync($"**{p2["Name"]}** is hibernating and can't breed right now."); return; }

        // ── Check egg cap ──────────────────────────────────────────────────────
        var eggsDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPendingEggs",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (eggsDt.Rows.Count >= MaxEggs)
        {
            await ErrorAsync($"You already have {eggsDt.Rows.Count} unhatched egg{(eggsDt.Rows.Count == 1 ? "" : "s")}. Hatch them first with `/hatchegg`.");
            return;
        }

        // ── Compute inherited stats (average ± up to 10% variance) ────────────
        int InheritStat(string col)
        {
            int a = int.Parse(p1[col].ToString()!);
            int b = int.Parse(p2[col].ToString()!);
            int avg = (a + b) / 2;
            int vary = (int)(avg * 0.10);
            return Math.Clamp(avg + Random.Shared.Next(-vary, vary + 1), 0, 100);
        }

        int baseHunger = InheritStat("Hunger");
        int baseHappiness = InheritStat("Happiness");
        int baseEnergy = InheritStat("Energy");
        int baseHygiene = InheritStat("Hygiene");

        // Head-start XP: 10% of the lower parent's XP
        int baseXp = (int)(Math.Min(xp1, xp2) * 0.10);

        // Breed: pick one parent's breed at random
        string breed = Random.Shared.Next(2) == 0
            ? p1["Breed"].ToString()!
            : p2["Breed"].ToString()!;

        // ── Create the egg ─────────────────────────────────────────────────────
        var eggDt = _sp.Select(Constants.Constants.discordBotConnStr, "CreatePetEgg",
        [
            new SqlParameter("@UserID",        UserId),
            new SqlParameter("@ServerID",      ServerId),
            new SqlParameter("@Parent1ID",     pet1Id),
            new SqlParameter("@Parent2ID",     pet2Id),
            new SqlParameter("@Species",       species1),
            new SqlParameter("@Breed",         breed),
            new SqlParameter("@BaseHunger",    baseHunger),
            new SqlParameter("@BaseHappiness", baseHappiness),
            new SqlParameter("@BaseEnergy",    baseEnergy),
            new SqlParameter("@BaseHygiene",   baseHygiene),
            new SqlParameter("@BaseXP",        baseXp)
        ]);

        if (eggDt.Rows.Count == 0) { await ErrorAsync("Failed to create egg. Please try again."); return; }

        int eggId = int.Parse(eggDt.Rows[0]["EggID"].ToString()!);
        DateTime hatchAt = DateTime.Parse(eggDt.Rows[0]["HatchAt"].ToString()!);
        long hatchTs = new DateTimeOffset(hatchAt).ToUnixTimeSeconds();

        string emoji = PetHelper.PetEmoji(species1, 100, 100, false, false);
        string p1Name = p1["Name"].ToString()!;
        string p2Name = p2["Name"].ToString()!;

        var statLines = new StringBuilder();
        statLines.AppendLine($"🍖 Hunger:    **{baseHunger}**");
        statLines.AppendLine($"😊 Happiness: **{baseHappiness}**");
        statLines.AppendLine($"⚡ Energy:    **{baseEnergy}**");
        statLines.AppendLine($"🛁 Hygiene:   **{baseHygiene}**");
        statLines.AppendLine($"⭐ Head-start XP: **{baseXp:N0}** (Lv {PetHelper.LevelFromXp(baseXp)})");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🥚  An Egg Appears!")
            .WithColor(ColourGold)
            .WithDescription(
                $"**{p1Name}** and **{p2Name}** have produced an egg!\n\n" +
                $"🐾 Species: **{species1}** — Breed: **{breed}**\n" +
                $"🥚 Egg ID: **#{eggId}**\n\n" +
                $"The egg will hatch <t:{hatchTs}:R> (<t:{hatchTs}:f>).\n" +
                $"Use `/hatchegg {eggId}` when it's ready!")
            .AddField("Inherited Stats", statLines.ToString(), inline: false)
            .WithFooter($"{Username} • Check /eggs for all pending eggs", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /eggs ─────────────────────────────────────────────────────────────────

    [SlashCommand("eggs", "View your pending eggs and hatch timers.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleEggsAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPendingEggs",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🥚  No Pending Eggs")
                .WithColor(ColourWarn)
                .WithDescription("You don't have any eggs incubating right now.\nUse `/breed` to produce one!")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        var desc = new StringBuilder();

        foreach (DataRow row in dt.Rows)
        {
            int eggId = int.Parse(row["EggID"].ToString()!);
            string species = row["Species"].ToString()!;
            string breed = row["Breed"].ToString()!;
            int secsLeft = int.Parse(row["SecondsRemaining"].ToString()!);
            bool ready = secsLeft <= 0;
            long hatchTs = new DateTimeOffset(DateTime.Parse(row["HatchAt"].ToString()!)).ToUnixTimeSeconds();
            int headXp = int.Parse(row["BaseXP"].ToString()!);
            string emoji = PetHelper.PetEmoji(species, 100, 100, false, false);

            desc.AppendLine(ready
                ? $"🥚 **Egg #{eggId}** — {emoji} {species} ({breed}) ✅ **Ready to hatch!** Use `/hatchegg {eggId}`"
                : $"🥚 **Egg #{eggId}** — {emoji} {species} ({breed}) — hatches <t:{hatchTs}:R>");

            desc.AppendLine($"　⭐ Head-start XP: {headXp:N0} (Lv {PetHelper.LevelFromXp(headXp)})");
        }

        desc.AppendLine();
        desc.AppendLine($"-# You can hold up to {MaxEggs} unhatched eggs at once.");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🥚  Your Eggs ({dt.Rows.Count}/{MaxEggs})")
            .WithColor(ColourGold)
            .WithDescription(desc.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /hatchegg ─────────────────────────────────────────────────────────────

    [SlashCommand("hatchegg", "Hatch a ready egg into a new pet!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleHatchEggAsync(
        [Summary("egg_id", "The egg ID to hatch (from /eggs).")] int eggId,
        [Summary("name", "Name your new pet.")][MinLength(1), MaxLength(32)] string name)
    {
        await DeferAsync();

        name = name.Trim();

        // ── Load and validate egg ──────────────────────────────────────────────
        var eggDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetEggByID",
        [
            new SqlParameter("@EggID",  eggId),
            new SqlParameter("@UserID", UserId)
        ]);

        if (eggDt.Rows.Count == 0)
        {
            await ErrorAsync($"Egg #{eggId} not found or doesn't belong to you.");
            return;
        }

        var egg = eggDt.Rows[0];

        if (egg["IsHatched"].ToString() is "1" or "True")
        {
            await ErrorAsync($"Egg #{eggId} has already hatched.");
            return;
        }

        int secsLeft = int.Parse(egg["SecondsRemaining"].ToString()!);
        if (secsLeft > 0)
        {
            int h = secsLeft / 3600;
            int m = (secsLeft % 3600) / 60;
            int s = secsLeft % 60;
            await ErrorAsync($"Egg #{eggId} isn't ready yet — hatches in **{h}h {m}m {s}s**.");
            return;
        }

        // ── Check pet cap ──────────────────────────────────────────────────────
        var allPets = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetsByUser",
            [new SqlParameter("@UserID", UserId)]);

        if (allPets.Rows.Count >= 100)
        {
            await ErrorAsync("You already have 100 pets! Release one before hatching.");
            return;
        }

        // ── Hatch: create the pet ──────────────────────────────────────────────
        string species = egg["Species"].ToString()!;
        string breed = egg["Breed"].ToString()!;
        int baseHunger = int.Parse(egg["BaseHunger"].ToString()!);
        int baseHappiness = int.Parse(egg["BaseHappiness"].ToString()!);
        int baseEnergy = int.Parse(egg["BaseEnergy"].ToString()!);
        int baseHygiene = int.Parse(egg["BaseHygiene"].ToString()!);
        int baseXp = int.Parse(egg["BaseXP"].ToString()!);
        bool hasActive = allPets.Rows.Count == 0;

        // We need a custom AddPet call that sets initial stats and XP
        // Use the existing AddPet SP then a secondary UpdatePetXP call
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPet",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@Name",     name),
            new SqlParameter("@Species",  species),
            new SqlParameter("@Breed",    breed),
            new SqlParameter("@IsActive", hasActive)
        ]);

        // Get the newly created pet's ID so we can set XP and inherited stats
        var newPetDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetsByUser",
            [new SqlParameter("@UserID", UserId)]);

        // Find the new pet (most recently added — highest PetID among this user)
        int newPetId = 0;
        foreach (DataRow r in newPetDt.Rows)
        {
            int id = int.Parse(r["PetID"].ToString()!);
            if (id > newPetId) newPetId = id;
        }

        if (newPetId > 0)
        {
            // Apply inherited stats and head-start XP via ApplyEggStats SP
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ApplyEggStats",
            [
                new SqlParameter("@PetID",     newPetId),
                new SqlParameter("@UserID",    UserId),
                new SqlParameter("@Hunger",    baseHunger),
                new SqlParameter("@Happiness", baseHappiness),
                new SqlParameter("@Energy",    baseEnergy),
                new SqlParameter("@Hygiene",   baseHygiene),
                new SqlParameter("@XP",        baseXp)
            ]);

            // Mark egg as hatched
            _sp.Select(Constants.Constants.discordBotConnStr, "HatchEgg",
            [
                new SqlParameter("@EggID",        eggId),
                new SqlParameter("@UserID",        UserId),
                new SqlParameter("@HatchedPetID",  newPetId)
            ]);
        }

        // ── Build response ─────────────────────────────────────────────────────
        string emoji = PetHelper.PetEmoji(species, baseHappiness, baseHunger, false, false);
        int level = PetHelper.LevelFromXp(baseXp);
        int parent1 = int.Parse(egg["Parent1ID"].ToString()!);
        int parent2 = int.Parse(egg["Parent2ID"].ToString()!);

        var statLines = new StringBuilder();
        statLines.AppendLine($"🍖 Hunger:    **{baseHunger}**");
        statLines.AppendLine($"😊 Happiness: **{baseHappiness}**");
        statLines.AppendLine($"⚡ Energy:    **{baseEnergy}**");
        statLines.AppendLine($"🛁 Hygiene:   **{baseHygiene}**");
        statLines.AppendLine($"⭐ Starting XP: **{baseXp:N0}** (Level **{level}**)");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{emoji}  {name} has hatched!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"Your egg has hatched into a **{breed} {species}**!\n\n" +
                $"👨‍👩‍👧 Parents: Pet **#{parent1}** × Pet **#{parent2}**\n" +
                (hasActive
                    ? $"✅ **{name}** is now your active pet."
                    : $"Use `/setactive` to make **{name}** your active pet."))
            .AddField("Starting Stats", statLines.ToString(), inline: false)
            .WithFooter($"{Username} • Use /petcard to see your new pet!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Breeding", message, Username).Build());
}