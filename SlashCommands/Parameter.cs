using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using DiscordBot.Models;    // ← add whatever namespace SpotifyTrack lives in
using DiscordBot.Services;  // ← add whatever namespace SpotifyService lives in
using Microsoft.Extensions.AI;
using OpenAI;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.SlashCommands;

/// <summary>
/// General-purpose slash command module.
/// Optimised for .NET 10 / C# 13 — primary changes:
///   • Random.Shared replaces per-call `new Random()`
///   • Collection expressions replace `new List&lt;T&gt; { … }`
///   • Primary-constructor injection for IHttpClientFactory
///   • Computed properties replace repeated Context.User accessors
///   • Switch expressions replace if-chains and switch statements
///   • String interpolation and StringBuilder replace concatenation loops
///   • LINQ used throughout in place of imperative loops
/// </summary>
public class Parameter : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISpotifyService _spotifyService;
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    public Parameter(ISpotifyService spotifyService)
    {
        _spotifyService = spotifyService;
    }

    // Shorthand properties — avoid repeating Context.User.* everywhere
    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    // Number emojis shared by poll commands
    private static readonly string[] NumberEmojis =
    [
        "1️⃣","2️⃣","3️⃣","4️⃣","5️⃣",
        "6️⃣","7️⃣","8️⃣","9️⃣","🔟"
    ];


    [SlashCommand("random", "Randomise a number between 1 and the value you provide.")]
    [EnabledInDm(true)]
    public async Task GenerateRandomNumberAsync(
        [MinValue(1), MaxValue(int.MaxValue)] int number)
    {
        await DeferAsync();

        // Random.Shared is thread-safe and avoids allocating a new Random instance per call.
        int result = Random.Shared.Next(1, number + 1);

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Random",
            $"{Context.User.Mention} rolled a **{result}** (1 – {number})",
            AvatarUrl, $"Command from: {Username}", Color.Green).Build());
    }

    [SlashCommand("etext", "Convert your message into regional-indicator emojis.")]
    [EnabledInDm(true)]
    public async Task HandleEmojiTextAsync(
        [MinLength(1), MaxLength(1000)] string message)
    {
        await DeferAsync();
        await FollowupAsync(new EmojiText().GetEmojiString(message));
    }

    [SlashCommand("poll", "Create a reaction poll with up to 10 choices.")]
    [EnabledInDm(true)]
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

        // Filter nulls and trim in one LINQ pass; no intermediate mutable list needed.
        var items = new[]
        {
            pollAnswer1, pollAnswer2, pollAnswer3, pollAnswer4, pollAnswer5,
            pollAnswer6, pollAnswer7, pollAnswer8, pollAnswer9, pollAnswer10
        }
        .Where(s => !string.IsNullOrEmpty(s))
        .Select(s => s!.Trim())
        .ToList();

        // StringBuilder avoids repeated string allocations inside the loop.
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

    [SlashCommand("addbirthday", "Add a member's birthday so the bot can celebrate it.")]
    [EnabledInDm(false)]
    public async Task HandleBirthdayAsync(
        SocketGuildUser user,
        [MinValue(1), MaxValue(12)] int monthNumber,
        [MinValue(1), MaxValue(31)] int dayNumber)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var guild = Context.Guild;

            if (!guild.Roles.Any(r => r.Name.Contains("birthday", StringComparison.OrdinalIgnoreCase)))
            {
                await guild.CreateRoleAsync("birthday", null, Color.Purple, false, true);
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle("Birthday")
                    .WithColor(Color.Gold)
                    .WithDescription(
                        "A **birthday** role was created. " +
                        "Please have an administrator assign it before running this command again.")
                    .Build(), ephemeral: true);
                return;
            }

            // DateTime constructor is cleaner than string-parsing month/day/year.
            var birthday = new DateTime(DateTime.Now.Year, monthNumber, dayNumber);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBirthday",
            [
                new SqlParameter("@BirthdayDate",  birthday),
                new SqlParameter("@BirthdayUser",  user.Mention),
                new SqlParameter("@BirthdayGuild", guild.Id.ToString())
            ]);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Birthday Added",
                $"**{user.DisplayName}'s** birthday ({monthNumber}/{dayNumber}) was added.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(
                embed: _embed.BuildErrorEmbed("Birthday", ex.Message, Username).Build(),
                ephemeral: true);
        }
    }

    [SlashCommand("avatar", "Display your avatar or another member's in full resolution.")]
    [EnabledInDm(true)]
    public async Task HandleAvatarAsync(SocketGuildUser? user = null)
    {
        await DeferAsync();
        var target = user ?? (SocketGuildUser)Context.User;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{target.DisplayName}'s Avatar")
            .WithColor(Color.Blue)
            .WithImageUrl(target.GetDisplayAvatarUrl(size: 1024) ?? target.GetDefaultAvatarUrl())
            .WithFooter($"Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    [SlashCommand("reportbug", "Found a bug with the bot? Report it here.")]
    [EnabledInDm(true)]
    public async Task HandleBugReportAsync(
        [MinLength(1), MaxLength(2000)] string bugFound)
    {
        const ulong LogGuildId = 880569055856185354UL;
        const ulong LogChannelId = 1156625507840954369UL;

        var channel = Context.Client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);

        if (channel is not null)
        {
            await channel.SendMessageAsync(embed: _embed.BuildMessageEmbed(
                "Bug Report",
                $"**From:** {Context.User.Mention} in **{Context.Guild?.Name ?? "DM"}**\n\n{bugFound}",
                AvatarUrl, Username, Color.Red).Build());
        }

        // Ephemeral confirmation is cleaner than ReplyAsync in a slash-command context.
        await RespondAsync("✅ Bug report submitted — thank you!", ephemeral: true);
    }

    [SlashCommand("polldnd", "Reaction poll for D&D weekly scheduling (next 7 days).")]
    [EnabledInDm(false)]
    public async Task HandlePollDndAsync(SocketGuildUser user)
    {
        await DeferAsync();

        // LINQ builds the date list without a mutable loop variable.
        var items = Enumerable.Range(1, 7)
            .Select(i => DateTime.Now.AddDays(i))
            .Select(d => $"{d.DayOfWeek} ({d:MM/dd})")
            .ToList();

        var sb = new StringBuilder(
            $"**Best day for {user.Mention} / {user.DisplayName}'s campaign?**\n\nChoices:");
        for (int i = 0; i < items.Count; i++)
            sb.Append($"\n{NumberEmojis[i]}  **{items[i]}**");

        var msg = await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Poll — D&D Scheduling", sb.ToString(), "",
            $"Command from: {Username}", Color.Blue).Build());

        for (int i = 0; i < items.Count; i++)
            await msg.AddReactionAsync(new Emoji(NumberEmojis[i]));
    }

    [SlashCommand("setrolecolor", "Set the colour of your role by hex code.")]
    [EnabledInDm(false)]
    public async Task HandleColorAsync(
        [MinLength(1), MaxLength(10)] string hexCode,
        SocketGuildUser? userName = null)
    {
        await DeferAsync(ephemeral: true);

        // Normalise: strip leading '#' so ColorTranslator always receives the bare hex,
        // then re-add it. This fixes the original bug where '#abc' became '##abc'.
        string bare = hexCode.TrimStart('#');
        string html = "#" + bare;

        try
        {
            var sysColor = System.Drawing.ColorTranslator.FromHtml(html);
            var roleColor = new Color(sysColor.R, sysColor.G, sysColor.B);
            var guild = Context.Guild;
            var target = (IGuildUser)(userName ?? (SocketGuildUser)Context.User);
            string name = ((SocketGuildUser)target).Username;

            if (guild.Roles.FirstOrDefault(r => r.Name == name) is { } existing)
            {
                await existing.ModifyAsync(p => p.Color = roleColor);
            }
            else
            {
                int botPos = guild.Roles.First(r => r.Name == "BigBirdBot").Position;
                var created = await guild.CreateRoleAsync(name, null, roleColor, false, true);
                await created.ModifyAsync(p => p.Position = botPos - 1);
                await target.AddRoleAsync(created);
            }

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Role Colour",
                $"Colour updated to **#{bare.ToUpperInvariant()}**.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Role Colour", $"Invalid hex code: {ex.Message}", Username).Build(),
                ephemeral: true);
        }
    }

    [SlashCommand("detectaibyattachment", "Upload an image to check the probability it was AI-generated.")]
    [EnabledInDm(true)]
    public async Task HandleAiByAttachmentAsync(Attachment attachment)
    {
        await DeferAsync();

        if (!attachment.ContentType.Contains("image"))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "AI Detection", "Only image files are supported.", Username).Build());
            return;
        }

        try
        {
            string[] parts = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string unique = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}";
            string path = Constants.Constants.aiDetectorPath + unique + "." + parts[1];

            using var http = new HttpClient();
            using var apiClient = new HttpClient();

            var bytes = await http.GetByteArrayAsync(attachment.Url);
            await File.WriteAllBytesAsync(path, bytes);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.sightengine.com/1.0/check.json");

            request.Content = new MultipartFormDataContent
            {
                { new ByteArrayContent(await File.ReadAllBytesAsync(path)), "media", Path.GetFileName(path) },
                { new StringContent("genai"),                                 "models"     },
                { new StringContent(Constants.Constants.aiApiUserId),        "api_user"   },
                { new StringContent(Constants.Constants.aiApiSecretId),      "api_secret" }
            };

            var response = await apiClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(body))
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "AI Detection", "No response from the detection endpoint.", Username).Build());
                return;
            }

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAIJSONImageReturn",
                [new SqlParameter("@json", body)]);

            if (dt.Rows.Count == 0 || dt.Rows[0]["Status"].ToString() != "success")
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "AI Detection", "The detection request failed.", Username).Build());
                return;
            }

            double rate = double.Parse(dt.Rows[0]["PercentageChance"].ToString()!);

            // Switch expression replaces the four sequential if-statements.
            string desc = rate switch
            {
                <= 25 => $"✅ **Small chance ({rate}%) this is AI** — likely safe to assume it is not.",
                <= 50 => $"⚠️ **Possible AI ({rate}%)** — worth investigating further.",
                <= 75 => $"🔶 **High chance ({rate}%) this is AI** — investigate further.",
                _ => $"🚨 **Almost certainly AI ({rate}%)** — {rate}% pattern match."
            };

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "AI Detection", desc, "", Username, Color.Blue, attachment.Url).Build());
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "AI Detection", ex.Message, Username).Build());
        }
    }

    [SlashCommand("chat", "Have a conversation with the bot using a chosen personality.")]
    [EnabledInDm(true)]
    public async Task HandleChatAsync(
        [MinLength(1), MaxLength(1000)] string message,
        [Choice("Yes", "Yes"), Choice("No", "No")] string startNew,
        [Choice("eSports Gamer Lesbian", "eSports Gamer Lesbian"),
         Choice("Sett",                 "Sett"),
         Choice("T. M. Opera O",        "T. M. Opera O"),
         Choice("Meisho Doto",          "Meisho Doto")] string personality)
    {
        await DeferAsync();

        // Switch expression replaces switch statement with string botPersona variable.
        string persona = personality switch
        {
            "eSports Gamer Lesbian" =>
                "You are a giga lesbian e-sports gamer who plays League of Legends, Valorant, Counter-Strike — everything. " +
                "You are the best and everyone else is trash. Don't be afraid to trash talk but provide no slurs.",
            "Sett" =>
                "You are Sett from League of Legends. Speak in their mannerisms but remain positive, helpful, and loving.",
            "T. M. Opera O" =>
                "You are T. M. Opera O from Umamusume: Pretty Derby. Speak in their mannerisms but remain positive, helpful, and loving.",
            "Meisho Doto" =>
                "You are Meisho Doto from Umamusume: Pretty Derby. Speak in their mannerisms but remain positive, helpful, and loving.",
            _ => "You are a friendly and helpful assistant."
        };

        string userId = Context.User.Id.ToString();
        string serverUid = Context.Guild?.Id.ToString() ?? "";
        string channelId = Context.Channel.Id.ToString();
        bool isNew = startNew == "Yes";
        message = message.Trim();

        try
        {
            if (isNew)
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteBotAIMessage",
                [
                    new SqlParameter("@UserID",    userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId)
                ]);
            }

            var history = isNew
                ? new DataTable()
                : _sp.Select(Constants.Constants.discordBotConnStr, "GetBotAIMessage",
                [
                    new SqlParameter("@UserID",    userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId)
                ]);

            IChatClient chatClient = new OpenAIClient(Constants.Constants.openAiToken)
                .GetChatClient(Constants.Constants.openAiModel)
                .AsIChatClient();

            var messages = new List<ChatMessage> { new(ChatRole.System, persona) };

            // Pattern-match the role string rather than equality-chain.
            foreach (DataRow dr in history.Rows)
            {
                string role = dr["ChatRole"].ToString()!;
                string text = dr["ChatMessage"].ToString()!;

                messages.Add(role switch
                {
                    var r when r == ChatRole.Assistant.ToString() => new(ChatRole.Assistant, text),
                    var r when r == ChatRole.Tool.ToString() => new(ChatRole.Tool, text),
                    var r when r == ChatRole.System.ToString() => new(ChatRole.System, text),
                    _ => new(ChatRole.User, text)
                });
            }

            messages.Add(new ChatMessage(ChatRole.User, message));

            var sb = new StringBuilder($"**Message:** {message}\n\n**Response:** ");
            await foreach (var chunk in chatClient.GetStreamingResponseAsync(messages))
                sb.Append(chunk.Text);

            // Slice instead of Substring — avoids bounds-check boilerplate.
            string response = sb.Length > 2000 ? sb.ToString()[..2000] : sb.ToString();

            // Spread operator flattens the shared base params into each call.
            SqlParameter[] baseParams =
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerUID", serverUid),
                new SqlParameter("@ChannelID", channelId)
            ];

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    ChatRole.User.ToString()),
                new SqlParameter("@ChatMessage", message)
            ]);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    ChatRole.Assistant.ToString()),
                new SqlParameter("@ChatMessage", response)
            ]);

            await FollowupAsync(response);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Chat", ex.Message, Username).Build());
        }
    }

    [SlashCommand("dnddice", "Roll any number of any-sided dice with an optional modifier.")]
    [EnabledInDm(true)]
    public async Task HandleDndDiceAsync(
        [MinValue(1)] int numberOfDice,
        [MinValue(2)] int sidesOnDice,
        int modifier = 0)
    {
        await DeferAsync();

        var rolls = Enumerable.Range(0, numberOfDice)
            .Select(_ => Random.Shared.Next(1, sidesOnDice + 1))
            .ToList();

        // Switch expression annotates natural 1/20; falls through to plain string.
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


    /// <summary>
    /// Classic magic 8-ball with 20 canonical responses across positive (green),
    /// neutral (blue), and negative (red) categories.
    /// </summary>
    [SlashCommand("8ball", "Ask the magic 8-ball a yes/no question.")]
    [EnabledInDm(true)]
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

    /// <summary>
    /// Picks randomly from a comma-separated list provided by the user.
    /// Highlights the winner inline so the choice is obvious at a glance.
    /// </summary>
    [SlashCommand("choose", "Let the bot pick from your comma-separated options.")]
    [EnabledInDm(true)]
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

    /// <summary>
    /// Displays a rich member card: join date, account creation, top role colour,
    /// nickname, bot flag, and a full role list.
    /// </summary>
    [SlashCommand("userinfo", "Show information about yourself or another member.")]
    [EnabledInDm(false)]
    public async Task HandleUserInfoAsync(SocketGuildUser? user = null)
    {
        await DeferAsync();
        var target = user ?? (SocketGuildUser)Context.User;

        string roleList = string.Join(", ", target.Roles
            .Where(r => !r.IsEveryone)
            .OrderByDescending(r => r.Position)
            .Select(r => r.Mention));

        if (string.IsNullOrEmpty(roleList)) roleList = "*None*";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"👤  {target.DisplayName}")
            .WithColor(target.Roles.MaxBy(r => r.Position)?.Color ?? Color.Default)
            .WithThumbnailUrl(target.GetDisplayAvatarUrl(size: 256) ?? target.GetDefaultAvatarUrl())
            .AddField("Username", target.Username, inline: true)
            .AddField("Nickname", target.Nickname ?? "*None*", inline: true)
            .AddField("Bot", target.IsBot ? "Yes" : "No", inline: true)
            .AddField("Account Created", target.CreatedAt.UtcDateTime.ToString("MMM dd, yyyy"), inline: true)
            .AddField("Joined Server", target.JoinedAt?.UtcDateTime.ToString("MMM dd, yyyy") ?? "*Unknown*", inline: true)
            .AddField("Roles", roleList, inline: false)
            .WithFooter($"ID: {target.Id}  •  Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>
    /// Server snapshot: member count, boost level, channel counts, owner, and creation date.
    /// </summary>
    [SlashCommand("serverinfo", "Show information about this server.")]
    [EnabledInDm(false)]
    public async Task HandleServerInfoAsync()
    {
        await DeferAsync();
        var guild = Context.Guild;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🏰  {guild.Name}")
            .WithColor(Color.Blue)
            .WithThumbnailUrl(guild.IconUrl)
            .AddField("Owner", guild.Owner.DisplayName, inline: true)
            .AddField("Members", guild.MemberCount.ToString(), inline: true)
            .AddField("Boost Level", $"Level {(int)guild.PremiumTier}", inline: true)
            .AddField("Boosts", guild.PremiumSubscriptionCount.ToString(), inline: true)
            .AddField("Text Channels", guild.TextChannels.Count.ToString(), inline: true)
            .AddField("Voice Channels", guild.VoiceChannels.Count.ToString(), inline: true)
            .AddField("Roles", guild.Roles.Count.ToString(), inline: true)
            .AddField("Created", guild.CreatedAt.UtcDateTime.ToString("MMM dd, yyyy"), inline: true)
            .WithFooter($"ID: {guild.Id}  •  Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    /// <summary>
    /// Renders a live colour swatch for a hex code via singlecolorimage.com
    /// so users can visually verify a colour before applying it with /setrolecolor.
    /// </summary>
    [SlashCommand("colorpreview", "Preview what a hex colour looks like before applying it.")]
    [EnabledInDm(true)]
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

    /// <summary>
    /// Sends a timed DM reminder. Fires from a background Task.Run so the
    /// interaction responds immediately and the countdown runs off the gateway thread.
    /// Maximum window is 24 hours (1 440 minutes).
    /// </summary>
    [SlashCommand("remind", "Set a DM reminder for yourself.")]
    [EnabledInDm(true)]
    public async Task HandleRemindAsync(
        [MinLength(1), MaxLength(500)] string reminder,
        [MinValue(1), MaxValue(1440),
         Summary("minutes", "How many minutes from now (max 1 440 = 24 h)")]
        int minutes)
    {
        await DeferAsync(ephemeral: true);

        var user = Context.User;

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "⏰  Reminder Set",
            $"I'll DM you in **{minutes} minute(s)**.\n> {reminder}",
            AvatarUrl, Username, Color.Gold).Build(), ephemeral: true);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(minutes));
            try
            {
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("⏰  Reminder")
                    .WithColor(Color.Gold)
                    .WithDescription(reminder)
                    .WithFooter("You asked me to remind you at this time.")
                    .WithCurrentTimestamp()
                    .Build());
            }
            catch { /* User has DMs disabled — nothing actionable */ }
        });
    }

    /// <summary>
    /// Calculates the full breakdown (years, months, days, and total days)
    /// between today and any past or future date the user provides.
    /// </summary>
    [SlashCommand("daysince", "Calculate how many days since or until a date.")]
    [EnabledInDm(true)]
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

    #region Spotify Mood

    private const string BtnRerollPrefix = "spotify:reroll:";
    private static readonly Color ColourSpotify = new(30, 215, 96);
    private static readonly Color ColourError = new(237, 66, 69);

    /// <summary>
    /// Picks a random Spotify track matching any mood the user describes.
    /// </summary>
    [SlashCommand("mood", "Get a random Spotify track that matches your mood.")]
    public async Task MoodAsync(
        [Summary("mood", "Describe your mood — e.g. melancholy, hype, chill, heartbreak")]
    [MinLength(1), MaxLength(100)]
    string mood)
    {
        await DeferAsync();
        mood = mood.Trim();
        var (embed, components) = await BuildMoodResponseAsync(mood);
        await FollowupAsync(embed: embed, components: components);
    }

    /// <summary>
    /// Re-rolls the track for the same mood when the 🎲 button is pressed.
    /// The mood is encoded directly in the custom ID — no session state needed.
    /// </summary>
    [ComponentInteraction($"{BtnRerollPrefix}*")]
    public async Task OnMoodRerollAsync(string mood)
    {
        await DeferAsync();
        var (embed, components) = await BuildMoodResponseAsync(mood);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }


    private async Task<(Embed embed, MessageComponent components)> BuildMoodResponseAsync(string mood)
    {
        var track = await _spotifyService.GetRandomTrackAsync(mood);

        if (track is null)
        {
            return (
                new EmbedBuilder()
                    .WithTitle("❌  No Results")
                    .WithColor(ColourError)
                    .WithDescription($"Spotify returned nothing for **{EscapeMd(mood)}**. Try a different mood!")
                    .WithFooter($"Requested by {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build(),
                new ComponentBuilder().Build());
        }

        return (BuildSpotifyEmbed(mood, track).Build(), BuildRerollButton(mood));
    }

    private EmbedBuilder BuildSpotifyEmbed(string mood, SpotifyTrack t)
    {
        var duration = TimeSpan.FromMilliseconds(t.DurationMs);
        string explicit_ = t.Explicit ? " 🅴" : "";

        var embed = new EmbedBuilder()
            .WithTitle($"{t.Name}{explicit_}")
            .WithUrl(t.Url)
            .WithColor(ColourSpotify)
            .WithThumbnailUrl(t.ArtworkUrl)
            .WithDescription($"A track picked for your **{EscapeMd(mood)}** mood.")
            .AddField("Artist", t.Artist, inline: true)
            .AddField("Album", $"[{t.Album}]({t.AlbumUrl})", inline: true)
            .AddField("Duration", $"`{duration:mm\\:ss}`", inline: true)
            .AddField("Popularity", $"{SpotifyStars(t.Popularity)} `{t.Popularity}/100`", inline: true)
            .WithFooter($"Powered by Spotify  •  Requested by {Context.User.Username}",
                        Context.User.GetAvatarUrl())
            .WithCurrentTimestamp();

        if (!string.IsNullOrEmpty(t.PreviewUrl))
            embed.AddField("30s Preview", $"[▶ Listen]({t.PreviewUrl})", inline: true);

        return embed;
    }

    private static MessageComponent BuildRerollButton(string mood) =>
        new ComponentBuilder()
            .WithButton("🎲  Reroll", $"{BtnRerollPrefix}{mood}", ButtonStyle.Success)
            .Build();

    private static string SpotifyStars(int popularity)
    {
        int stars = (int)Math.Round(popularity / 20.0);
        return string.Create(5, stars, static (span, s) =>
        {
            span.Fill('☆');
            span[..s].Fill('★');
        });
    }

    private static string EscapeMd(string s) =>
        s.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`").Replace("~", "\\~");

    #endregion
}
