using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Server and user information commands, plus server management utilities.
/// </summary>
public class ServerCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly string[] NumberEmojis =
    [
        "1️⃣","2️⃣","3️⃣","4️⃣","5️⃣",
        "6️⃣","7️⃣","8️⃣","9️⃣","🔟"
    ];


    [SlashCommand("avatar", "Display your avatar or another member's in full resolution.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
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


    [SlashCommand("userinfo", "Show information about yourself or another member.")]
    [CommandContextType(InteractionContextType.Guild)]
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


    [SlashCommand("serverinfo", "Show information about this server.")]
    [CommandContextType(InteractionContextType.Guild)]
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


    [SlashCommand("addbirthday", "Add a member's birthday so the bot can celebrate it.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBirthdayAsync(
        SocketGuildUser user,
        [MinValue(1), MaxValue(12)] int monthNumber,
        [MinValue(1), MaxValue(31)] int dayNumber,
        [Summary(description: "Channel to post the birthday message in. Defaults to the server's default channel.")]
        SocketTextChannel? channel = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var guild = Context.Guild;

            IRole birthdayRole = guild.Roles.FirstOrDefault(r => r.Name.Contains("birthday", StringComparison.OrdinalIgnoreCase));
            if (birthdayRole == null)
            {
                birthdayRole = await guild.CreateRoleAsync("birthday", null, Color.Purple, false, true);
            }

            await guild.DownloadUsersAsync();
            var nonBotMembers = guild.Users.Where(u => !u.IsBot).ToList();
            var membersToAdd = nonBotMembers.Where(u => !u.Roles.Any(r => r.Id == birthdayRole.Id)).ToList();
            foreach (var member in membersToAdd)
            {
                await member.AddRoleAsync(birthdayRole);
            }

            var birthday = new DateTime(DateTime.Now.Year, monthNumber, dayNumber);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBirthday",
            [
                new SqlParameter("@BirthdayDate",    birthday),
                new SqlParameter("@BirthdayUser",    user.Mention),
                new SqlParameter("@BirthdayGuild",   guild.Id.ToString()),
                new SqlParameter("@BirthdayChannel", (object?)channel?.Id.ToString() ?? DBNull.Value)
            ]);

            string channelNote = channel is not null
                ? $" Announcements will post in {channel.Mention}."
                : " Announcements will post in the server's default channel.";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Birthday Added",
                $"**{user.DisplayName}'s** birthday ({monthNumber}/{dayNumber}) was added.{channelNote}",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(
                embed: _embed.BuildErrorEmbed("Birthday", ex.Message, Username).Build(),
                ephemeral: true);
        }
    }


    [SlashCommand("setrolecolor", "Set the colour of your role by hex code.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleColorAsync(
        [MinLength(1), MaxLength(10)] string hexCode,
        SocketGuildUser? userName = null)
    {
        await DeferAsync(ephemeral: true);

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


    [SlashCommand("polldnd", "Reaction poll for D&D weekly scheduling (next 7 days).")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePollDndAsync(SocketGuildUser user)
    {
        await DeferAsync();

        var items = Enumerable.Range(1, 7)
            .Select(i => DateTime.Now.AddDays(i))
            .Select(d => $"{d.DayOfWeek} ({d:MM/dd})")
            .ToList();

        var sb = new System.Text.StringBuilder(
            $"**Best day for {user.Mention} / {user.DisplayName}'s campaign?**\n\nChoices:");
        for (int i = 0; i < items.Count; i++)
            sb.Append($"\n{NumberEmojis[i]}  **{items[i]}**");

        var msg = await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Poll — D&D Scheduling", sb.ToString(), "",
            $"Command from: {Username}", Color.Blue).Build());

        for (int i = 0; i < items.Count; i++)
            await msg.AddReactionAsync(new Emoji(NumberEmojis[i]));
    }


    [SlashCommand("reportbug", "Found a bug with the bot? Report it here.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
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

        await RespondAsync("✅ Bug report submitted — thank you!", ephemeral: true);
    }


    /// <summary>
    /// Unified profile card showing credits, active pet, and gambling snapshot.
    /// Pulls from the same stored procedures used by individual commands.
    /// </summary>
    [SlashCommand("profile", "View your full profile — credits, active pet, and stats.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleProfileAsync(SocketGuildUser? user = null)
    {
        await DeferAsync();

        var target = user ?? (SocketGuildUser)Context.User;
        string targetId = target.Id.ToString();

        // ── Credits ─────────────────────────────────────────────────────────
        var creditDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        string creditsField = "*No account yet*";
        if (creditDt.Rows.Count > 0)
        {
            long balance = long.Parse(creditDt.Rows[0]["Balance"].ToString()!);
            long earned  = long.Parse(creditDt.Rows[0]["TotalEarned"].ToString()!);
            creditsField = $"{CreditHelper.Format(balance)}\nTotal Earned: {CreditHelper.Format(earned)}";
        }

        // ── Active Pet ───────────────────────────────────────────────────────
        var petDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", targetId)]);

        string petField = "*No active pet*";
        if (petDt.Rows.Count > 0)
        {
            var row = petDt.Rows[0];
            string petName    = row["Name"].ToString()!;
            string petSpecies = row["Species"].ToString()!;
            int xp            = int.Parse(row["XP"].ToString()!);
            int level         = PetHelper.LevelFromXp(xp);
            int happiness     = int.Parse(row["Happiness"].ToString()!);
            int hunger        = int.Parse(row["Hunger"].ToString()!);
            bool hib          = bool.TryParse(row["IsHibernating"].ToString(), out bool h) && h;
            bool evolved      = level >= 50;
            string emoji      = PetHelper.PetEmoji(petSpecies, happiness, hunger, hib, evolved);

            petField = $"{emoji} **{petName}** — Lv.{level}\n" +
                       $"😊 {PetHelper.StatBar(happiness)} | 🍖 {PetHelper.StatBar(hunger)}";
        }

        // ── Gambling Stats ───────────────────────────────────────────────────
        var gambleDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetGambleStats",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        string gambleField = "*No gambling history*";
        if (gambleDt.Rows.Count > 0)
        {
            var row  = gambleDt.Rows[0];
            decimal wins   = decimal.Parse(row["Wins"].ToString()!);
            decimal losses = decimal.Parse(row["Losses"].ToString()!);
            decimal net    = decimal.Parse(row["NetTotal"].ToString()!);
            string netStr = net >= 0
                ? $"+{CreditHelper.Format(net)}"
                : $"-{CreditHelper.Format(Math.Abs(net))}";
            gambleField = $"W/L: **{wins}** / **{losses}** — Net: {netStr}";
        }

        bool isSelf = target.Id == Context.User.Id;
        string footer = isSelf
            ? $"Use /balance, /petcard, and /gamblestats for full details"
            : $"Requested by {Username}";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🪪  {target.DisplayName}'s Profile")
            .WithColor(target.Roles.MaxBy(r => r.Position)?.Color ?? Color.Blue)
            .WithThumbnailUrl(target.GetDisplayAvatarUrl(size: 256) ?? target.GetDefaultAvatarUrl())
            .AddField($"{CreditHelper.CurrencyEmoji} Credits", creditsField, inline: false)
            .AddField("🐾 Active Pet", petField, inline: false)
            .AddField("🎰 Gambling", gambleField, inline: false)
            .WithFooter(footer, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }
}
