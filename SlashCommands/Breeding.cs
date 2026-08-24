using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;
using System.Text;
using PetEntity = DiscordBot.Models.Generated.Pet;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Pet breeding system.
/// /breed   — breed two of your pets together to produce an egg (24h hatch timer).
/// /eggs    — view your pending eggs and time remaining.
/// /hatchegg — hatch a ready egg into a new pet.
/// Requirements: both pets must be level 10+, same species, not hibernating.
/// </summary>
public class Breeding(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
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

    /// <summary>
    /// Validates both parent pets (same species, level 10+, not hibernating, egg cap not
    /// reached), then creates an egg with stats averaged from both parents (±10% variance)
    /// and a 24h hatch timer.
    /// </summary>
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
        var p1 = await db.Pets.FirstOrDefaultAsync(x => x.PetId == pet1Id && x.UserId == UserId);
        var p2 = await db.Pets.FirstOrDefaultAsync(x => x.PetId == pet2Id && x.UserId == UserId);

        if (p1 is null) { await ErrorAsync($"Pet #{pet1Id} not found or doesn't belong to you."); return; }
        if (p2 is null) { await ErrorAsync($"Pet #{pet2Id} not found or doesn't belong to you."); return; }

        // ── Validation ─────────────────────────────────────────────────────────
        string species1 = p1.Species;
        string species2 = p2.Species;

        if (species1 != species2)
        {
            await ErrorAsync($"Both pets must be the same species to breed.\n**{p1.Name}** is a {species1}, **{p2.Name}** is a {species2}.");
            return;
        }

        int xp1 = p1.Xp;
        int xp2 = p2.Xp;
        int level1 = PetHelper.LevelFromXp(xp1);
        int level2 = PetHelper.LevelFromXp(xp2);

        if (level1 < MinBreedLevel)
        {
            await ErrorAsync($"**{p1.Name}** is level {level1}. Both pets must be at least level {MinBreedLevel} to breed.");
            return;
        }
        if (level2 < MinBreedLevel)
        {
            await ErrorAsync($"**{p2.Name}** is level {level2}. Both pets must be at least level {MinBreedLevel} to breed.");
            return;
        }

        if (p1.IsHibernating) { await ErrorAsync($"**{p1.Name}** is hibernating and can't breed right now."); return; }
        if (p2.IsHibernating) { await ErrorAsync($"**{p2.Name}** is hibernating and can't breed right now."); return; }

        // ── Check egg cap ──────────────────────────────────────────────────────
        int pendingEggCount = await db.PetEggs.CountAsync(e => e.UserId == UserId && e.ServerId == ServerId && !e.IsHatched);

        if (pendingEggCount >= MaxEggs)
        {
            await ErrorAsync($"You already have {pendingEggCount} unhatched egg{(pendingEggCount == 1 ? "" : "s")}. Hatch them first with `/hatchegg`.");
            return;
        }

        // ── Compute inherited stats (average ± up to 10% variance) ────────────
        int InheritStat(int a, int b)
        {
            int avg = (a + b) / 2;
            int vary = (int)(avg * 0.10);
            return Math.Clamp(avg + Random.Shared.Next(-vary, vary + 1), 0, 100);
        }

        int baseHunger = InheritStat(p1.Hunger, p2.Hunger);
        int baseHappiness = InheritStat(p1.Happiness, p2.Happiness);
        int baseEnergy = InheritStat(p1.Energy, p2.Energy);
        int baseHygiene = InheritStat(p1.Hygiene, p2.Hygiene);

        // Head-start XP: 10% of the lower parent's XP
        int baseXp = (int)(Math.Min(xp1, xp2) * 0.10);

        // Breed: pick one parent's breed at random
        string breed = Random.Shared.Next(2) == 0 ? p1.Breed : p2.Breed;

        // ── Create the egg ─────────────────────────────────────────────────────
        // Source (CreatePetEgg) computes HatchAt server-side as GETUTCDATE() + 24h.
        DateTime hatchAt = DateTime.UtcNow.AddHours(24);
        var egg = new PetEgg
        {
            UserId = UserId, ServerId = ServerId, Parent1Id = pet1Id, Parent2Id = pet2Id,
            Species = species1, Breed = breed,
            BaseHunger = baseHunger, BaseHappiness = baseHappiness, BaseEnergy = baseEnergy,
            BaseHygiene = baseHygiene, BaseXp = baseXp, HatchAt = hatchAt
        };
        db.PetEggs.Add(egg);
        await db.SaveChangesAsync();

        int eggId = egg.EggId;
        long hatchTs = new DateTimeOffset(hatchAt).ToUnixTimeSeconds();

        string emoji = PetHelper.PetEmoji(species1, 100, 100, false, false);
        string p1Name = p1.Name;
        string p2Name = p2.Name;

        var statLines = new StringBuilder();
        statLines.AppendLine($"🍖 Hunger:    **{baseHunger}**");
        statLines.AppendLine($"😊 Happiness: **{baseHappiness}**");
        statLines.AppendLine($"⚡ Energy:    **{baseEnergy}**");
        statLines.AppendLine($"🛁 Hygiene:   **{baseHygiene}**");
        statLines.AppendLine($"⭐ Head-start XP: **{baseXp:N0}** (Lv {PetHelper.LevelFromXp(baseXp)})");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🥚  An Egg Appears!",
            $"**{p1Name}** and **{p2Name}** have produced an egg!\n\n" +
            $"🐾 Species: **{species1}** — Breed: **{breed}**\n" +
            $"🥚 Egg ID: **#{eggId}**\n\n" +
            $"The egg will hatch <t:{hatchTs}:R> (<t:{hatchTs}:f>).\n" +
            $"Use `/hatchegg {eggId}` when it's ready!",
            ColourGold, footer: $"{Username} • Check /eggs for all pending eggs", footerIconUrl: AvatarUrl,
            fields: [("Inherited Stats", statLines.ToString(), false)])
            .Build());
    }

    // ── /eggs ─────────────────────────────────────────────────────────────────

    /// <summary>Lists every pending egg with its hatch countdown (or "ready to hatch" once the timer elapses).</summary>
    [SlashCommand("eggs", "View your pending eggs and hatch timers.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleEggsAsync()
    {
        await DeferAsync();

        var eggs = await db.PetEggs.AsNoTracking()
            .Where(e => e.UserId == UserId && e.ServerId == ServerId && !e.IsHatched)
            .OrderBy(e => e.HatchAt).ToListAsync();

        if (eggs.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "🥚  No Pending Eggs", "You don't have any eggs incubating right now.\nUse `/breed` to produce one!",
                ColourWarn, footer: Username, footerIconUrl: AvatarUrl).Build());
            return;
        }

        var desc = new StringBuilder();
        var now = DateTime.UtcNow;

        foreach (var row in eggs)
        {
            int eggId = row.EggId;
            string species = row.Species;
            string breed = row.Breed;
            int secsLeft = (int)(row.HatchAt - now).TotalSeconds;
            bool ready = secsLeft <= 0;
            long hatchTs = new DateTimeOffset(row.HatchAt).ToUnixTimeSeconds();
            int headXp = row.BaseXp;
            string emoji = PetHelper.PetEmoji(species, 100, 100, false, false);

            desc.AppendLine(ready
                ? $"🥚 **Egg #{eggId}** — {emoji} {species} ({breed}) ✅ **Ready to hatch!** Use `/hatchegg {eggId}`"
                : $"🥚 **Egg #{eggId}** — {emoji} {species} ({breed}) — hatches <t:{hatchTs}:R>");

            desc.AppendLine($"　⭐ Head-start XP: {headXp:N0} (Lv {PetHelper.LevelFromXp(headXp)})");
        }

        desc.AppendLine();
        desc.AppendLine($"-# You can hold up to {MaxEggs} unhatched eggs at once.");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🥚  Your Eggs ({eggs.Count}/{MaxEggs})", desc.ToString(),
            ColourGold, footer: Username, footerIconUrl: AvatarUrl).Build());
    }

    // ── /hatchegg ─────────────────────────────────────────────────────────────

    /// <summary>Hatches a ready egg into a new pet, applying its inherited stats and head-start XP, and auto-activating it if it's the user's first pet.</summary>
    [SlashCommand("hatchegg", "Hatch a ready egg into a new pet!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleHatchEggAsync(
        [Summary("egg_id", "The egg ID to hatch (from /eggs).")] int eggId,
        [Summary("name", "Name your new pet.")][MinLength(1), MaxLength(32)] string name)
    {
        await DeferAsync();

        name = name.Trim();

        // ── Load and validate egg ──────────────────────────────────────────────
        var egg = await db.PetEggs.FirstOrDefaultAsync(e => e.EggId == eggId && e.UserId == UserId);

        if (egg is null)
        {
            await ErrorAsync($"Egg #{eggId} not found or doesn't belong to you.");
            return;
        }

        if (egg.IsHatched)
        {
            await ErrorAsync($"Egg #{eggId} has already hatched.");
            return;
        }

        int secsLeft = (int)(egg.HatchAt - DateTime.UtcNow).TotalSeconds;
        if (secsLeft > 0)
        {
            int h = secsLeft / 3600;
            int m = (secsLeft % 3600) / 60;
            int s = secsLeft % 60;
            await ErrorAsync($"Egg #{eggId} isn't ready yet — hatches in **{h}h {m}m {s}s**.");
            return;
        }

        // ── Check pet cap ──────────────────────────────────────────────────────
        int existingPetCount = await db.Pets.CountAsync(p => p.UserId == UserId);

        if (existingPetCount >= 100)
        {
            await ErrorAsync("You already have 100 pets! Release one before hatching.");
            return;
        }

        // ── Hatch: create the pet ──────────────────────────────────────────────
        string species = egg.Species;
        string breed = egg.Breed;
        int baseHunger = egg.BaseHunger;
        int baseHappiness = egg.BaseHappiness;
        int baseEnergy = egg.BaseEnergy;
        int baseHygiene = egg.BaseHygiene;
        int baseXp = egg.BaseXp;
        bool hasActive = existingPetCount == 0;

        // Source made a plain AddPet call, then re-fetched all the user's pets and guessed
        // which one was new by picking the highest PetID, then a second ApplyEggStats call to
        // set the inherited stats/XP — a workaround for AddPet's proc not returning the new ID.
        // EF gives the identity value back directly from the insert, and the entity being
        // inserted can just carry the final stats/XP from the start — no re-fetch, no guess,
        // no second write. Source (deactivate-all-others-if-active) preserved.
        if (hasActive)
            await db.Pets.Where(p => p.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        var newPet = new PetEntity
        {
            UserId = UserId, ServerId = ServerId, Name = name, Species = species, Breed = breed,
            IsActive = hasActive,
            Hunger = baseHunger, Happiness = baseHappiness, Energy = baseEnergy, Hygiene = baseHygiene, Xp = baseXp
        };
        db.Pets.Add(newPet);
        egg.IsHatched = true;
        await db.SaveChangesAsync(); // generates newPet.PetId

        // HatchedPetId is a plain int column, not a configured navigation/FK, so it can't be
        // fixed up automatically pre-save — set it now that the real ID exists and save again.
        egg.HatchedPetId = newPet.PetId;
        await db.SaveChangesAsync();

        // ── Build response ─────────────────────────────────────────────────────
        string emoji = PetHelper.PetEmoji(species, baseHappiness, baseHunger, false, false);
        int level = PetHelper.LevelFromXp(baseXp);
        int parent1 = egg.Parent1Id;
        int parent2 = egg.Parent2Id;

        var statLines = new StringBuilder();
        statLines.AppendLine($"🍖 Hunger:    **{baseHunger}**");
        statLines.AppendLine($"😊 Happiness: **{baseHappiness}**");
        statLines.AppendLine($"⚡ Energy:    **{baseEnergy}**");
        statLines.AppendLine($"🛁 Hygiene:   **{baseHygiene}**");
        statLines.AppendLine($"⭐ Starting XP: **{baseXp:N0}** (Level **{level}**)");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{emoji}  {name} has hatched!",
            $"Your egg has hatched into a **{breed} {species}**!\n\n" +
            $"👨‍👩‍👧 Parents: Pet **#{parent1}** × Pet **#{parent2}**\n" +
            (hasActive
                ? $"✅ **{name}** is now your active pet."
                : $"Use `/setactive` to make **{name}** your active pet."),
            ColourSuccess, footer: $"{Username} • Use /petcard to see your new pet!", footerIconUrl: AvatarUrl,
            fields: [("Starting Stats", statLines.ToString(), false)]).Build());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Posts a standard Breeding-branded error embed.</summary>
    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Breeding", message, Username).Build());
}