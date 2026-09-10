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


    /// <summary>Posts a native Discord poll with 2–10 answers, an optional multi-select, and a configurable run time. Guild-only — Discord does not allow bots to create polls in DMs.</summary>
    [SlashCommand("poll", "Create a poll with up to 10 choices.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePollAsync(
        [Summary("question"), MinLength(1), MaxLength(300)] string question,
        [Summary("answer1"), MinLength(1), MaxLength(55)] string answer1,
        [Summary("answer2"), MinLength(1), MaxLength(55)] string answer2,
        [Summary("answer3"), MaxLength(55)] string? answer3 = null,
        [Summary("answer4"), MaxLength(55)] string? answer4 = null,
        [Summary("answer5"), MaxLength(55)] string? answer5 = null,
        [Summary("answer6"), MaxLength(55)] string? answer6 = null,
        [Summary("answer7"), MaxLength(55)] string? answer7 = null,
        [Summary("answer8"), MaxLength(55)] string? answer8 = null,
        [Summary("answer9"), MaxLength(55)] string? answer9 = null,
        [Summary("answer10"), MaxLength(55)] string? answer10 = null,
        [Summary("duration_hours", "How many hours the poll stays open (1–768). Default 24."),
         MinValue(1), MaxValue(768)] int durationHours = 24,
        [Summary("allow_multiple", "Let voters pick more than one answer")] bool allowMultiple = false)
    {
        await DeferAsync();

        var answers = new[]
        {
            answer1, answer2, answer3, answer4, answer5,
            answer6, answer7, answer8, answer9, answer10
        }
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => new PollMediaProperties { Text = s!.Trim() })
        .ToList();

        var poll = new PollProperties
        {
            Question         = new PollMediaProperties { Text = question.Trim() },
            Answers          = answers,
            Duration         = (uint)durationHours,
            AllowMultiselect = allowMultiple,
        };

        await FollowupAsync(poll: poll);
    }


    /// <summary>
    /// Schedules a one-off DM reminder. <c>when</c> is parsed relative to the user's saved
    /// <c>/timezone</c> (or the per-call <c>timezone</c> override) via <see cref="WhenParser"/>,
    /// rejecting times under 1 minute or over 1 year away.
    /// </summary>
    [SlashCommand("remind", "Set a DM reminder — 'in 2h', 'tomorrow 9am', '2026-03-25 15:30'.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleRemindAsync(
        [Summary("message", "What to remind you about"), MinLength(1), MaxLength(500)] string reminder,
        [Summary("when", "e.g. 'in 90m', 'in 3 days', 'tomorrow 9am', '2026-03-25 15:30' (month/day order)")] string when,
        [Summary("timezone", "Override your saved zone just for this reminder — e.g. -5, Europe/London")] string? timezone = null)
    {
        await DeferAsync(ephemeral: true);

        string userId = Context.User.Id.ToString();

        TimeZoneInfo zone;
        string zoneLabel;
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            if (!TimeZoneResolver.TryResolve(timezone, out zone, out var canonical))
            {
                await RemindErrorAsync("Unknown Time Zone",
                    $"Couldn't understand `{timezone}`. Try an offset like `-5` / `+5:30`, or an IANA name like `Europe/London`.");
                return;
            }
            zoneLabel = canonical;
        }
        else
        {
            string? saved = await scheduling.GetUserTimeZoneAsync(userId);
            if (saved is null)
            {
                await RemindErrorAsync("Set Your Time Zone First",
                    "I don't know your time zone, so I can't tell when you mean. Run `/timezone` once " +
                    "(e.g. `/timezone -5` or `/timezone Europe/London`), or pass `timezone:` on this command.");
                return;
            }
            zone = TimeZoneResolver.Resolve(saved);
            zoneLabel = saved;
        }

        if (!WhenParser.TryParse(when, zone, DateTime.UtcNow, out var remindAtUtc))
        {
            await RemindErrorAsync("Invalid Time",
                "Couldn't parse that. Try `in 90m`, `in 3 days`, `tomorrow 9am`, or `2026-03-25 15:30`.");
            return;
        }

        var delay = remindAtUtc - DateTime.UtcNow;
        if (delay < TimeSpan.FromMinutes(1))
        {
            await RemindErrorAsync("Too Soon", "Reminders must be at least **1 minute** from now.");
            return;
        }
        if (delay > TimeSpan.FromDays(365))
        {
            await RemindErrorAsync("Too Far", "Reminders can be set at most **1 year** in advance.");
            return;
        }

        int id = await scheduling.AddReminderAsync(userId, reminder, remindAtUtc);

        long unix = new DateTimeOffset(DateTime.SpecifyKind(remindAtUtc, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeSeconds();
        var localWhen = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(remindAtUtc, DateTimeKind.Utc), zone);

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "⏰  Reminder Set",
            $"**#{id}** — I'll DM you on **{localWhen:MMMM d, yyyy 'at' h:mm tt}** ({zoneLabel})  •  <t:{unix}:R>\n> {reminder}\n\n" +
            $"*Cancel with* `/reminddelete id:{id}`.",
            AvatarUrl, Username, Color.Gold).Build(), ephemeral: true);
    }

    private Task RemindErrorAsync(string title, string body) =>
        FollowupAsync(embed: _embed.BuildMessageEmbed(
            $"⏰  {title}", body, AvatarUrl, Username, Color.Red).Build(), ephemeral: true);


    /// <summary>Lists the caller's pending (not-yet-sent) reminders with their cancel ids.</summary>
    [SlashCommand("reminders", "List your pending reminders.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleRemindersAsync()
    {
        await DeferAsync(ephemeral: true);

        var pending = await scheduling.GetPendingRemindersAsync(Context.User.Id.ToString());

        if (pending.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "⏰  Reminders", "You have no pending reminders.",
                AvatarUrl, Username, Color.Blue).Build(), ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        foreach (var r in pending.Take(25))
        {
            long unix = new DateTimeOffset(DateTime.SpecifyKind(r.RemindAtUtc, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeSeconds();
            string msg = r.Message.Length > 80 ? r.Message[..79] + "…" : r.Message;
            sb.AppendLine($"**#{r.ReminderId}** — <t:{unix}:f>  (<t:{unix}:R>)\n> {msg}");
        }
        if (pending.Count > 25)
            sb.AppendLine($"*…and {pending.Count - 25} more.*");

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "⏰  Your Reminders", sb.ToString(),
            AvatarUrl, Username, Color.Blue).Build(), ephemeral: true);
    }


    /// <summary>Cancels one of the caller's pending reminders by the id shown in <c>/reminders</c>.</summary>
    [SlashCommand("reminddelete", "Cancel one of your pending reminders by its number.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleRemindDeleteAsync(
        [Summary("id", "The reminder number from /reminders"), MinValue(1)] int id)
    {
        await DeferAsync(ephemeral: true);

        bool ok = await scheduling.CancelReminderAsync(Context.User.Id.ToString(), id);

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            ok ? "⏰  Reminder Cancelled" : "⏰  Not Found",
            ok ? $"Reminder **#{id}** was cancelled." : $"You have no pending reminder **#{id}**.",
            AvatarUrl, Username, ok ? Color.Green : Color.Red).Build(), ephemeral: true);
    }


    /// <summary>Shows a hex code's RGB breakdown and a rendered swatch image, without applying it anywhere.</summary>
    [SlashCommand("colorpreview", "Preview what a hex colour looks like before applying it.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleColorPreviewAsync(
        [MinLength(1), MaxLength(10)] string hexCode)
    {
        await DeferAsync();

        string bare = hexCode.TrimStart('#').ToUpperInvariant();

        if (!HexColor.TryParse(bare, out var role))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Color Preview",
                $"`#{bare}` is not a valid hex code. Example: `#607C8C`",
                Username).Build());
            return;
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🎨  Color Preview — #{bare}",
            $"**Hex:** `#{bare}`\n" +
            $"**RGB:** `{role.R}, {role.G}, {role.B}`",
            role, footer: $"Requested by {Username}", footerIconUrl: AvatarUrl)
            .WithImageUrl($"https://singlecolorimage.com/get/{bare}/300x80").Build());
    }


    /// <summary>Rolls the given number of dice with the given side count plus a flat modifier, flagging natural 1s and max rolls.</summary>
    [SlashCommand("dnddice", "Roll up to 100 dice of up to 1000 sides, with an optional modifier.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleDndDiceAsync(
        [Summary("number_of_dice", "How many dice (1–100)"), MinValue(1), MaxValue(100)] int numberOfDice,
        [Summary("sides_on_dice", "Sides per die (2–1000)"), MinValue(2), MaxValue(1000)] int sidesOnDice,
        [Summary("modifier", "Flat number added to the total")] int modifier = 0)
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


    /// <summary>Toggles this server's automatic link-embed-fixing (Twitter/Reddit/TikTok/Bsky). Requires Manage Server, matching <c>/announcements</c>.</summary>
    [SlashCommand("fixembed", "Toggle whether the bot fixes Twitter/Reddit/TikTok/Bluesky embeds here.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task HandleEmbeds()
    {
        await DeferAsync(ephemeral: true);

        string result = await servers.ToggleEmbedFixAsync(Context.Guild.Id) ?? "";

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Embeds", result, "", $"Command from: {Username}", Color.Green).Build(),
            ephemeral: true);
    }
}
