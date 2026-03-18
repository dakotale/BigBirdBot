using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Cosmetic Forge — a pure credit sink.
/// Users burn credits to permanently attach a custom-named title or aura
/// to their active pet. Higher tiers cost more and unlock more characters
/// and a colour option. Forged cosmetics are stored permanently and
/// displayed on /pet card alongside regular shop cosmetics.
/// </summary>
[Group("forge", "Burn credits to craft custom cosmetics for your active pet.")]
public class Forge : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly Economy _eco = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourForge = new(255, 140, 0);
    private static readonly Color ColourSuccess = new(87, 242, 135);
    private static readonly Color ColourRed = new(237, 66, 69);

    // ── Tier definitions ──────────────────────────────────────────────────────
    // Cost is a pure sink — no payout, no RNG. Higher tiers unlock longer
    // names and the colour picker.
    private static readonly (string name, decimal cost, int maxChars, bool colourPicker, string flavour)[] Tiers =
    [
        ("Common",    5_000_000m,   16, false, "A simple mark of effort."),
        ("Rare",      25_000_000m,  24, false, "A sign of serious dedication."),
        ("Epic",      100_000_000m, 32, true,  "Few pets bear this mark."),
        ("Legendary", 500_000_000m, 48, true,  "The rarest honour a pet can wear."),
    ];

    // ── /forge title ──────────────────────────────────────────────────────────

    [SlashCommand("title", "Forge a custom title for your active pet.")]
    [EnabledInDm(false)]
    public async Task HandleForgeTitleAsync(
        [Choice("Common (5M)",      "1"),
         Choice("Rare (25M)",       "2"),
         Choice("Epic (100M)",      "3"),
         Choice("Legendary (500M)", "4")]
        string tierStr,
        [Summary("text", "The title text to display on your pet card.")]
        [MinLength(1), MaxLength(48)] string text,
        [Summary("colour", "Hex colour code e.g. #FF4500 (Epic/Legendary only).")]
        string? colour = null)
    {
        await DeferAsync();
        await HandleForge("title", int.Parse(tierStr), text, colour);
    }

    // ── /forge aura ───────────────────────────────────────────────────────────

    [SlashCommand("aura", "Forge a custom aura label for your active pet.")]
    [EnabledInDm(false)]
    public async Task HandleForgeAuraAsync(
        [Choice("Common (5M)",      "1"),
         Choice("Rare (25M)",       "2"),
         Choice("Epic (100M)",      "3"),
         Choice("Legendary (500M)", "4")]
        string tierStr,
        [Summary("text", "The aura name to display on your pet card.")]
        [MinLength(1), MaxLength(48)] string text,
        [Summary("colour", "Hex colour code e.g. #9B59B6 (Epic/Legendary only).")]
        string? colour = null)
    {
        await DeferAsync();
        await HandleForge("aura", int.Parse(tierStr), text, colour);
    }

    // ── /forge list ───────────────────────────────────────────────────────────

    [SlashCommand("list", "View all forged cosmetics on your active pet.")]
    [EnabledInDm(false)]
    public async Task HandleForgeListAsync()
    {
        await DeferAsync();

        var petDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", UserId)]);

        if (petDt.Rows.Count == 0)
        {
            await ErrorAsync("You don't have an active pet.");
            return;
        }

        int petId = int.Parse(petDt.Rows[0]["PetID"].ToString()!);
        string petName = petDt.Rows[0]["Name"].ToString()!;

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetForgedCosmetics",
        [
            new SqlParameter("@PetID",  petId),
            new SqlParameter("@UserID", UserId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"Forge — {petName}")
                .WithColor(ColourForge)
                .WithDescription(
                    $"**{petName}** has no forged cosmetics yet.\n\n" +
                    $"Use `/forge title` or `/forge aura` to craft one.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        var desc = new StringBuilder();
        decimal totalBurned = 0m;

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string type = row["Type"].ToString()!;
            string tier = TierName(int.Parse(row["Tier"].ToString()!));
            string display = row["DisplayText"].ToString()!;
            string hex = row["ColourHex"].ToString()!;
            decimal cost = decimal.Parse(row["CreditsCost"].ToString()!);
            totalBurned += cost;

            string colourNote = hex != "#FFFFFF" ? $" `{hex}`" : "";
            desc.AppendLine($"**[{tier} {type}]** {display}{colourNote} — {CreditHelper.Format(cost)}");
        }

        desc.AppendLine();
        desc.AppendLine($"-# Total burned on {petName}: **{CreditHelper.Format(totalBurned)}**");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"Forge — {petName}'s Cosmetics ({dt.Rows.Count})")
            .WithColor(ColourForge)
            .WithDescription(desc.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /forge tiers ─────────────────────────────────────────────────────────

    [SlashCommand("tiers", "View forge tier costs and limits.")]
    [EnabledInDm(false)]
    public async Task HandleForgeTiersAsync()
    {
        await DeferAsync();

        var desc = new StringBuilder();
        desc.AppendLine("Forged cosmetics are permanently attached to your active pet.");
        desc.AppendLine("Credits are burned on forge — there is no refund.\n");

        for (int i = 0; i < Tiers.Length; i++)
        {
            var (name, cost, maxChars, colourPicker, flavour) = Tiers[i];
            desc.AppendLine($"**{name}** — {CreditHelper.Format(cost)}");
            desc.AppendLine($"　Max {maxChars} characters" +
                            (colourPicker ? " · Custom hex colour" : "") +
                            $"\n　*{flavour}*");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("Cosmetic Forge — Tiers")
            .WithColor(ColourForge)
            .WithDescription(desc.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Shared forge logic ────────────────────────────────────────────────────

    private async Task HandleForge(string type, int tierIdx, string text, string? colourHex)
    {
        var tier = Tiers[tierIdx - 1];

        // ── Validate text length ───────────────────────────────────────────────
        text = text.Trim();
        if (text.Length > tier.maxChars)
        {
            await ErrorAsync(
                $"**{tier.name}** tier allows up to **{tier.maxChars}** characters. " +
                $"Your text is **{text.Length}**.");
            return;
        }

        // ── Validate colour ────────────────────────────────────────────────────
        string resolvedColour = "#FFFFFF";
        if (colourHex is not null)
        {
            if (!tier.colourPicker)
            {
                await ErrorAsync(
                    $"Custom colours are only available on **Epic** and **Legendary** tiers.");
                return;
            }

            colourHex = colourHex.Trim();
            if (!colourHex.StartsWith('#')) colourHex = "#" + colourHex;
            if (colourHex.Length != 7 || !IsValidHex(colourHex))
            {
                await ErrorAsync(
                    $"`{colourHex}` isn't a valid hex colour. Use format `#RRGGBB` e.g. `#FF4500`.");
                return;
            }
            resolvedColour = colourHex.ToUpperInvariant();
        }

        // ── Check active pet ───────────────────────────────────────────────────
        var petDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", UserId)]);

        if (petDt.Rows.Count == 0)
        {
            await ErrorAsync("You don't have an active pet to forge a cosmetic for.");
            return;
        }

        int petId = int.Parse(petDt.Rows[0]["PetID"].ToString()!);
        string petName = petDt.Rows[0]["Name"].ToString()!;

        // ── Check balance ──────────────────────────────────────────────────────
        decimal balance = _eco.GetBalance(UserId, ServerId);
        if (balance < tier.cost)
        {
            await ErrorAsync(
                $"A **{tier.name}** forge costs {CreditHelper.Format(tier.cost)}.\n" +
                $"You have {CreditHelper.Format(balance)}.");
            return;
        }

        // ── Deduct and record ──────────────────────────────────────────────────
        _eco.DeductCredits(UserId, ServerId, tier.cost, $"forge_{type}_{tier.name.ToLower()}");

        var forgeDt = _sp.Select(Constants.Constants.discordBotConnStr, "AddForgedCosmetic",
        [
            new SqlParameter("@UserID",      UserId),
            new SqlParameter("@ServerID",    ServerId),
            new SqlParameter("@PetID",       petId),
            new SqlParameter("@Type",        type),
            new SqlParameter("@Tier",        (byte)tierIdx),
            new SqlParameter("@DisplayText", text),
            new SqlParameter("@ColourHex",   resolvedColour),
            new SqlParameter("@CreditsCost", tier.cost)
        ]);

        decimal newBalance = _eco.GetBalance(UserId, ServerId);

        // ── Build result embed ─────────────────────────────────────────────────
        string colourDisplay = resolvedColour != "#FFFFFF"
            ? $"\nColour: `{resolvedColour}`"
            : "";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"Forged — {tier.name} {char.ToUpper(type[0])}{type[1..]}")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"**{petName}** now bears the {type}:\n\n" +
                $"**\"{text}\"**{colourDisplay}\n\n" +
                $"*{tier.flavour}*")
            .AddField("Tier", tier.name, inline: true)
            .AddField("Burned", CreditHelper.Format(tier.cost), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter($"{Username} • Use /pet card to see it on your pet", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string TierName(int tier) => tier switch
    {
        1 => "Common",
        2 => "Rare",
        3 => "Epic",
        _ => "Legendary"
    };

    private static bool IsValidHex(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return false;
        foreach (char c in hex[1..])
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Forge", message, Username).Build());
}