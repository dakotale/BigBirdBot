using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Server and user information commands, plus server management utilities.
/// </summary>
public class ServerCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly string[] NumberEmojis =
    [
        "1️⃣","2️⃣","3️⃣","4️⃣","5️⃣",
        "6️⃣","7️⃣","8️⃣","9️⃣","🔟"
    ];


    /// <summary>Shows a member's (or the caller's) avatar at full resolution.</summary>
    [SlashCommand("avatar", "Display your avatar or another member's in full resolution.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleAvatarAsync(SocketGuildUser? user = null)
    {
        await DeferAsync();
        var target = user ?? (SocketGuildUser)Context.User;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{target.DisplayName}'s Avatar", "", Color.Blue,
            footer: $"Requested by {Username}", footerIconUrl: AvatarUrl)
            .WithImageUrl(target.GetDisplayAvatarUrl(size: 1024) ?? target.GetDefaultAvatarUrl()).Build());
    }


    /// <summary>Shows a member's (or the caller's) username, nickname, account/join dates, and roles.</summary>
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

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"👤  {target.DisplayName}", "", target.Roles.MaxBy(r => r.Position)?.Color ?? Color.Default,
            footer: $"ID: {target.Id}  •  Requested by {Username}", footerIconUrl: AvatarUrl,
            fields: [("Username", target.Username, true),
                     ("Nickname", target.Nickname ?? "*None*", true),
                     ("Bot", target.IsBot ? "Yes" : "No", true),
                     ("Account Created", target.CreatedAt.UtcDateTime.ToString("MMM dd, yyyy"), true),
                     ("Joined Server", target.JoinedAt?.UtcDateTime.ToString("MMM dd, yyyy") ?? "*Unknown*", true),
                     ("Roles", roleList, false)])
            .WithThumbnailUrl(target.GetDisplayAvatarUrl(size: 256) ?? target.GetDefaultAvatarUrl()).Build());
    }


    /// <summary>Shows the current server's owner, member/channel/role counts, boost level, and creation date.</summary>
    [SlashCommand("serverinfo", "Show information about this server.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleServerInfoAsync()
    {
        await DeferAsync();
        var guild = Context.Guild;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🏰  {guild.Name}", "", Color.Blue,
            footer: $"ID: {guild.Id}  •  Requested by {Username}", footerIconUrl: AvatarUrl,
            fields: [("Owner", guild.Owner.DisplayName, true),
                     ("Members", guild.MemberCount.ToString(), true),
                     ("Boost Level", $"Level {(int)guild.PremiumTier}", true),
                     ("Boosts", guild.PremiumSubscriptionCount.ToString(), true),
                     ("Text Channels", guild.TextChannels.Count.ToString(), true),
                     ("Voice Channels", guild.VoiceChannels.Count.ToString(), true),
                     ("Roles", guild.Roles.Count.ToString(), true),
                     ("Created", guild.CreatedAt.UtcDateTime.ToString("MMM dd, yyyy"), true)])
            .WithThumbnailUrl(guild.IconUrl).Build());
    }


    /// <summary>Records a member's birthday for future celebration, creating (and backfilling) a "birthday" role on the server if one doesn't already exist.</summary>
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

            // Source (AddBirthday) inserted one row per year for the next 9 years so the
            // exact-date match in GetTodaysBirthdays fires once annually without wraparound
            // logic — not a bug, replicated exactly. BirthdayDate is a calendar date with no
            // real "instant" meaning; treated as local midnight then converted to UTC to match
            // this app's GETDATE()-local convention (see Keyword.cs/Program.cs for the pattern).
            for (int n = 0; n <= 8; n++)
            {
                db.Birthdays.Add(new Birthday
                {
                    BirthdayDate = birthday.AddYears(n).ToUniversalTime(),
                    BirthdayUser = user.Mention,
                    BirthdayGuild = guild.Id.ToString(),
                    BirthdayChannel = channel?.Id.ToString()
                });
            }
            await db.SaveChangesAsync();

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


    /// <summary>Sets a member's personal name-role to the given hex color, creating the role positioned just below the bot's role if it doesn't already exist.</summary>
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


    /// <summary>Posts a reaction poll for the next 7 calendar days so members can vote on the best day for a given user's D&amp;D session.</summary>
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


    /// <summary>Forwards a user-submitted bug report to the bot owner's fixed log channel.</summary>
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
        var credit = await db.Credits.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == targetId && c.ServerId == ServerId);

        string creditsField = "*No account yet*";
        if (credit is not null)
        {
            creditsField = $"{CreditHelper.Format(credit.Balance)}\nTotal Earned: {CreditHelper.Format(credit.TotalEarned)}";
        }

        // ── Active Pet ───────────────────────────────────────────────────────
        // Source (GetActivePet) filters by UserID only, no ServerID — an active pet is a
        // per-user, not per-server, concept. Preserved exactly.
        var pet = await db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == targetId && p.IsActive);

        string petField = "*No active pet*";
        if (pet is not null)
        {
            int level     = PetHelper.LevelFromXp(pet.Xp);
            bool evolved  = level >= 50;
            string emoji  = PetHelper.PetEmoji(pet.Species, pet.Happiness, pet.Hunger, pet.IsHibernating, evolved);

            petField = $"{emoji} **{pet.Name}** — Lv.{level}\n" +
                       $"😊 {PetHelper.StatBar(pet.Happiness)} | 🍖 {PetHelper.StatBar(pet.Hunger)}";
        }

        // ── Gambling Stats ───────────────────────────────────────────────────
        // Source (GetGambleStats) returns one row PER GAME, ordered by TotalWagered DESC —
        // the original C# only ever read row[0], i.e. only the user's highest-wagered game's
        // W/L/Net, silently mislabeled as overall gambling stats when a user has played more
        // than one game. Pre-existing application-level bug (not introduced by this
        // conversion) — replicated exactly here rather than silently fixed; flagged for the
        // user to decide whether to aggregate across all games instead.
        var topGame = await db.GambleLogs.AsNoTracking()
            .Where(g => g.UserId == targetId && g.ServerId == ServerId)
            .GroupBy(g => g.Game)
            .Select(grp => new
            {
                Wins = grp.Count(g => g.Net > 0),
                Losses = grp.Count(g => g.Net < 0),
                NetTotal = grp.Sum(g => g.Net),
                TotalWagered = grp.Sum(g => g.Bet)
            })
            .OrderByDescending(g => g.TotalWagered)
            .FirstOrDefaultAsync();

        string gambleField = "*No gambling history*";
        if (topGame is not null)
        {
            string netStr = topGame.NetTotal >= 0
                ? $"+{CreditHelper.Format(topGame.NetTotal)}"
                : $"-{CreditHelper.Format(Math.Abs(topGame.NetTotal))}";
            gambleField = $"W/L: **{topGame.Wins}** / **{topGame.Losses}** — Net: {netStr}";
        }

        bool isSelf = target.Id == Context.User.Id;
        string footer = isSelf
            ? $"Use /balance, /petcard, and /gamblestats for full details"
            : $"Requested by {Username}";

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🪪  {target.DisplayName}'s Profile", "", target.Roles.MaxBy(r => r.Position)?.Color ?? Color.Blue,
            footer: footer, footerIconUrl: AvatarUrl,
            fields: [($"{CreditHelper.CurrencyEmoji} Credits", creditsField, false),
                     ("🐾 Active Pet", petField, false),
                     ("🎰 Gambling", gambleField, false)])
            .WithThumbnailUrl(target.GetDisplayAvatarUrl(size: 256) ?? target.GetDefaultAvatarUrl()).Build());
    }
}
