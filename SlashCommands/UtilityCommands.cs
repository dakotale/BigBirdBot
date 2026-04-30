using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// General-purpose utility commands.
/// These are self-contained tools with no external API dependencies.
/// </summary>
public class UtilityCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly string[] NumberEmojis =
    [
        "1️⃣","2️⃣","3️⃣","4️⃣","5️⃣",
        "6️⃣","7️⃣","8️⃣","9️⃣","🔟"
    ];


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


    [SlashCommand("etext", "Convert your message into regional-indicator emojis.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleEmojiTextAsync(
        [MinLength(1), MaxLength(1000)] string message)
    {
        await DeferAsync();
        await FollowupAsync(new EmojiText().GetEmojiString(message));
    }


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


    [SlashCommand("8ball", "Ask the magic 8-ball a yes/no question.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleEightBallAsync(
        [MinLength(1), MaxLength(500)] string question)
    {
        await DeferAsync();

        (string text, Color color)[] responses =
        [
            ("It is certain.",             Color.Green), ("It is decidedly so.",    Color.Green),
            ("Without a doubt.",           Color.Green), ("Yes, definitely.",        Color.Green),
            ("You may rely on it.",        Color.Green), ("As I see it, yes.",       Color.Green),
            ("Most likely.",               Color.Green), ("Outlook good.",           Color.Green),
            ("Signs point to yes.",        Color.Green), ("Yes.",                    Color.Green),
            ("Reply hazy, try again.",     Color.Blue),  ("Ask again later.",        Color.Blue),
            ("Better not tell you now.",   Color.Blue),  ("Cannot predict now.",     Color.Blue),
            ("Concentrate and ask again.", Color.Blue),
            ("Don't count on it.",         Color.Red),   ("My reply is no.",         Color.Red),
            ("My sources say no.",         Color.Red),   ("Outlook not so good.",    Color.Red),
            ("Very doubtful.",             Color.Red)
        ];

        var (text, color) = responses[Random.Shared.Next(responses.Length)];

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎱  Magic 8-Ball")
            .WithColor(color)
            .AddField("Question", question, inline: false)
            .AddField("Answer", $"*{text}*", inline: false)
            .WithFooter($"Asked by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("choose", "Let the bot pick from your comma-separated options.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleChooseAsync(
        [MinLength(3), MaxLength(1000),
         Summary("options", "Comma-separated list, e.g. Pizza, Sushi, Tacos")]
        string options)
    {
        await DeferAsync();

        var choices = options.Split(
            ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (choices.Length < 2)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Choose", "Please provide at least two comma-separated options.", Username).Build());
            return;
        }

        string winner = choices[Random.Shared.Next(choices.Length)];
        string list = string.Join("\n", choices.Select(c =>
            c == winner ? $"➡️  **{c}**" : $"　 {c}"));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎯  The bot chooses…")
            .WithColor(Color.Purple)
            .WithDescription(list)
            .WithFooter($"Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


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

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddReminder",
        [
            new SqlParameter("@UserID",      Context.User.Id.ToString()),
            new SqlParameter("@Message",     reminder),
            new SqlParameter("@RemindAtUtc", reminderUtc)
        ]);

        string offsetLabel = utcOffset >= 0 ? $"UTC+{utcOffset}" : $"UTC{utcOffset}";
        string displayTime = parsedLocal.ToString("MMMM d, yyyy 'at' h:mm tt");

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "⏰  Reminder Set",
            $"I'll DM you on **{displayTime}** ({offsetLabel}).\n> {reminder}",
            AvatarUrl, Username, Color.Gold).Build(), ephemeral: true);
    }


    [SlashCommand("daysince", "Calculate how many days since or until a date.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleDaySinceAsync(
        [Summary("date", "Date in MM/DD/YYYY format")] string date)
    {
        await DeferAsync();

        if (!DateTime.TryParse(date, out var parsed))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Day Since", "Invalid date format — please use MM/DD/YYYY.", Username).Build());
            return;
        }

        int totalDays = (int)Math.Abs((parsed.Date - DateTime.Today).TotalDays);
        bool past = parsed.Date <= DateTime.Today;
        string dir = past ? "since" : "until";

        int years = totalDays / 365;
        int months = totalDays % 365 / 30;
        int days = totalDays % 30;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📅  Days {dir} {parsed:MMMM dd, yyyy}")
            .WithColor(past ? Color.LightGrey : Color.Green)
            .WithDescription($"**{years}y {months}mo {days}d** ({totalDays:N0} total days)")
            .WithFooter($"Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


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

            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"🎨  Color Preview — #{bare}")
                .WithColor(role)
                .WithDescription(
                    $"**Hex:** `#{bare}`\n" +
                    $"**RGB:** `{sys.R}, {sys.G}, {sys.B}`")
                .WithImageUrl($"https://singlecolorimage.com/get/{bare}/300x80")
                .WithFooter($"Requested by {Username}", AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
        }
        catch
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Color Preview",
                $"`#{bare}` is not a valid hex code. Example: `#607C8C`",
                Username).Build());
        }
    }


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


    [SlashCommand("fixembed", "Let the bot fix embeds for Twitter, Reddit, Tiktok, and Bsky links.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleEmbeds()
    {
        await DeferAsync(ephemeral: true);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "UpdateBrokenEmbed",
            [new SqlParameter("@ServerID", long.Parse(Context.Guild.Id.ToString()))]);

        string result = dt.Rows.Count > 0 ? dt.Rows[^1]["Result"].ToString() ?? "" : "";

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Embeds", result, "", $"Command from: {Username}", Color.Green).Build(),
            ephemeral: true);
    }
}
