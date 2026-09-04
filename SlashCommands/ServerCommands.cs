using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Server and user information commands, plus server management utilities.
/// </summary>
public class ServerCommands(SchedulingService scheduling) : InteractionModuleBase<SocketInteractionContext>
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

            await scheduling.AddBirthdayAsync(birthday, user.Mention, guild.Id.ToString(), channel?.Id.ToString());

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
}
