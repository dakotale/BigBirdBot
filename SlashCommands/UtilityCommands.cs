using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// General-purpose utility commands.
/// These are self-contained tools with no external API dependencies.
/// </summary>
public class UtilityCommands(SchedulingService scheduling, ServerService servers) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly string[] NumberEmojis =
    [
        "1️⃣","2️⃣","3️⃣","4️⃣","5️⃣",
        "6️⃣","7️⃣","8️⃣","9️⃣","🔟"
    ];


    /// <summary>Rolls a random integer between 1 and the given upper bound (inclusive).</summary>
    [SlashCommand("random", "Randomise a number between 1 and the value you provide.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task GenerateRandomNumberAsync(
        [MinValue(1), MaxValue(int.MaxValue)] int number)
    {
        await DeferAsync();
        int result = Random.Shared.Next(1, number + 1);
        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Random",
            $"{Context.User.Mention} rolled a **{result}** (1 – {number})",
            AvatarUrl, $"Command from: {Username}", Color.Green).Build());
    }


    /// <summary>Posts a reaction poll for up to 10 non-empty choices (2 required), with an optional image attachment.</summary>
    [SlashCommand("poll", "Create a reaction poll with up to 10 choices.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandlePollAsync(
        [MinLength(1), MaxLength(2000)] string statement,
        [MinLength(1)] string pollAnswer1,
        [MinLength(1)] string pollAnswer2,
        string? pollAnswer3 = null, string? pollAnswer4 = null,
        string? pollAnswer5 = null, string? pollAnswer6 = null,
        string? pollAnswer7 = null, string? pollAnswer8 = null,
        string? pollAnswer9 = null, string? pollAnswer10 = null,
        Attachment? attachment = null)
    {
        await DeferAsync();

        var items = new[]
        {
            pollAnswer1, pollAnswer2, pollAnswer3, pollAnswer4, pollAnswer5,
            pollAnswer6, pollAnswer7, pollAnswer8, pollAnswer9, pollAnswer10
        }
        .Where(s => !string.IsNullOrEmpty(s))
        .Select(s => s!.Trim())
        .ToList();

        var sb = new StringBuilder($"**{statement.Trim()}**\n\nChoices:");
        for (int i = 0; i < items.Count; i++)
            sb.Append($"\n{NumberEmojis[i]}  **{items[i]}**");

        var msg = await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Poll", sb.ToString(), "",
            $"Command from: {Username}", Color.Blue,
            attachment?.Url ?? "").Build());

        for (int i = 0; i < items.Count; i++)
            await msg.AddReactionAsync(new Emoji(NumberEmojis[i]));
    }


    /// <summary>Schedules a one-off DM reminder at a parsed local date/time (converted to UTC via the given offset), rejecting times under 1 minute or over 1 year away.</summary>
    [SlashCommand("remind", "Set a DM reminder for yourself at a specific date/time.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleRemindAsync(
        [MinLength(1), MaxLength(500)] string reminder,
        [Summary("when", "Date and time, e.g. '03/25/2026 3:30 PM' or '2026-03-25 15:30'")] string when,
        [Summary("utc_offset", "Your UTC offset, e.g. -5 for EST, -8 for PST, +1 for CET")]
        double utcOffset = 0)
    {
        await DeferAsync(ephemeral: true);

        if (!DateTime.TryParse(when, out DateTime parsedLocal))
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "⏰  Invalid Date",
                "Couldn't parse that date/time. Try a format like `03/25/2026 3:30 PM` or `2026-03-25 15:30`.",
                AvatarUrl, Username, Color.Red).Build(), ephemeral: true);
            return;
        }

        var reminderUtc = DateTime.SpecifyKind(parsedLocal, DateTimeKind.Unspecified)
                          - TimeSpan.FromHours(utcOffset);
        var delay = reminderUtc - DateTime.UtcNow;

        if (delay < TimeSpan.FromMinutes(1))
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "⏰  Too Soon",
                "Reminders must be at least **1 minute** from now.",
                AvatarUrl, Username, Color.Red).Build(), ephemeral: true);
            return;
        }

        if (delay > TimeSpan.FromDays(365))
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "⏰  Too Far",
                "Reminders can be set at most **1 year** in advance.",
                AvatarUrl, Username, Color.Red).Build(), ephemeral: true);
            return;
        }

        await scheduling.AddReminderAsync(Context.User.Id.ToString(), reminder, reminderUtc);

        string offsetLabel = utcOffset >= 0 ? $"UTC+{utcOffset}" : $"UTC{utcOffset}";
        string displayTime = parsedLocal.ToString("MMMM d, yyyy 'at' h:mm tt");

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "⏰  Reminder Set",
            $"I'll DM you on **{displayTime}** ({offsetLabel}).\n> {reminder}",
            AvatarUrl, Username, Color.Gold).Build(), ephemeral: true);
    }


    /// <summary>Shows a hex code's RGB breakdown and a rendered swatch image, without applying it anywhere.</summary>
    [SlashCommand("colorpreview", "Preview what a hex colour looks like before applying it.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleColorPreviewAsync(
        [MinLength(1), MaxLength(10)] string hexCode)
    {
        await DeferAsync();

        string bare = hexCode.TrimStart('#').ToUpperInvariant();

        try
        {
            var sys = System.Drawing.ColorTranslator.FromHtml("#" + bare);
            var role = new Color(sys.R, sys.G, sys.B);

            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"🎨  Color Preview — #{bare}",
                $"**Hex:** `#{bare}`\n" +
                $"**RGB:** `{sys.R}, {sys.G}, {sys.B}`",
                role, footer: $"Requested by {Username}", footerIconUrl: AvatarUrl)
                .WithImageUrl($"https://singlecolorimage.com/get/{bare}/300x80").Build());
        }
        catch
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Color Preview",
                $"`#{bare}` is not a valid hex code. Example: `#607C8C`",
                Username).Build());
        }
    }


    /// <summary>Rolls the given number of dice with the given side count plus a flat modifier, flagging natural 1s and max rolls.</summary>
    [SlashCommand("dnddice", "Roll any number of any-sided dice with an optional modifier.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleDndDiceAsync(
        [MinValue(1)] int numberOfDice,
        [MinValue(2)] int sidesOnDice,
        int modifier = 0)
    {
        await DeferAsync();

        var rolls = Enumerable.Range(0, numberOfDice)
            .Select(_ => Random.Shared.Next(1, sidesOnDice + 1))
            .ToList();

        var annotated = rolls.Select(v => v switch
        {
            1 => $"{v} **(Natural 1!)**",
            _ when v == sidesOnDice => $"{v} **(Maximum Roll!)**",
            _ => v.ToString()
        });

        string sign = modifier >= 0 ? "+" : "";

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "D&D Dice Roller",
            $"{Context.User.Mention} rolled **{numberOfDice}d{sidesOnDice}** {sign}{modifier}\n\n" +
            $"**Rolls:** {string.Join(", ", annotated)}\n" +
            $"**Total:** {rolls.Sum() + modifier}",
            AvatarUrl, $"Command from: {Username}", Color.Green).Build());
    }


    /// <summary>Toggles this server's automatic link-embed-fixing (Twitter/Reddit/TikTok/Bsky) via the UpdateBrokenEmbed stored procedure.</summary>
    [SlashCommand("fixembed", "Let the bot fix embeds for Twitter, Reddit, Tiktok, and Bsky links.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleEmbeds()
    {
        await DeferAsync(ephemeral: true);

        string result = await servers.ToggleEmbedFixAsync(Context.Guild.Id) ?? "";

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Embeds", result, "", $"Command from: {Username}", Color.Green).Build(),
            ephemeral: true);
    }
}
