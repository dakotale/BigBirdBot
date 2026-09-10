using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Server and user information commands, plus server management utilities.
/// </summary>
public class ServerCommands(SchedulingService scheduling) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    // Per-user /reportbug cooldown so the owner's log channel can't be spammed.
    private static readonly ConcurrentDictionary<ulong, DateTime> _bugReportCooldowns = new();
    private static readonly TimeSpan BugReportCooldown = TimeSpan.FromSeconds(60);


    /// <summary>Shows a member's (or the caller's) avatar at full resolution. Works in DMs (where only the caller can be targeted).</summary>
    [SlashCommand("avatar", "Display your avatar or another member's in full resolution.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleAvatarAsync(IUser? user = null)
    {
        await DeferAsync();
        var target = user ?? Context.User;
        string name = (target as IGuildUser)?.DisplayName ?? target.GlobalName ?? target.Username;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{name}'s Avatar", "", Color.Blue,
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
            fields: [("Owner", guild.Owner?.DisplayName ?? "Unknown", true),
                     ("Members", guild.MemberCount.ToString(), true),
                     ("Boost Level", $"Level {(int)guild.PremiumTier}", true),
                     ("Boosts", guild.PremiumSubscriptionCount.ToString(), true),
                     ("Text Channels", guild.TextChannels.Count.ToString(), true),
                     ("Voice Channels", guild.VoiceChannels.Count.ToString(), true),
                     ("Roles", guild.Roles.Count.ToString(), true),
                     ("Created", guild.CreatedAt.UtcDateTime.ToString("MMM dd, yyyy"), true)])
            .WithThumbnailUrl(guild.IconUrl).Build());
    }


    /// <summary>
    /// Registers a member's birthday for the yearly greeting. Re-registering the same member
    /// replaces their previous entry (no stacked announcements). You may always register your
    /// own; registering someone else needs Manage Server.
    /// </summary>
    [SlashCommand("addbirthday", "Add a birthday so the bot can celebrate it (yours, or anyone's with Manage Server).")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBirthdayAsync(
        SocketGuildUser user,
        [Summary("month", "Month (1–12)"), MinValue(1), MaxValue(12)] int monthNumber,
        [Summary("day", "Day of month (1–31)"), MinValue(1), MaxValue(31)] int dayNumber,
        [Summary("channel", "Where to post the greeting. Defaults to the server's announcement channel.")]
        SocketTextChannel? channel = null)
    {
        await DeferAsync(ephemeral: true);

        if (!MayManageBirthdayFor(user))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Birthday", "You can only add your own birthday — adding someone else's needs the **Manage Server** permission.",
                Username).Build(), ephemeral: true);
            return;
        }

        // Feb 29 is allowed (stored, and rolled to Feb 28 in common years); reject impossible
        // combinations like April 31.
        int maxDay = DateTime.DaysInMonth(2024, monthNumber); // 2024 is a leap year → February allows 29
        if (dayNumber > maxDay)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Birthday", $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthNumber)} only has {maxDay} days.",
                Username).Build(), ephemeral: true);
            return;
        }

        try
        {
            await scheduling.AddBirthdayAsync(
                monthNumber, dayNumber, user.Mention, Context.Guild.Id.ToString(), channel?.Id.ToString());

            string channelNote = channel is not null
                ? $" Greetings will post in {channel.Mention}."
                : " Greetings will post in the server's announcement channel.";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "🎂  Birthday Added",
                $"**{user.DisplayName}'s** birthday ({monthNumber}/{dayNumber}) is registered.{channelNote}",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(
                embed: _embed.BuildErrorEmbed("Birthday", ex.Message, Username).Build(),
                ephemeral: true);
        }
    }


    /// <summary>Lists the birthdays registered in this server, each shown as its next upcoming date.</summary>
    [SlashCommand("birthdays", "List the birthdays registered in this server.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBirthdaysAsync()
    {
        await DeferAsync(ephemeral: true);

        var birthdays = await scheduling.GetGuildBirthdaysAsync(Context.Guild.Id.ToString());

        if (birthdays.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "🎂  Birthdays", "No birthdays are registered in this server yet.",
                "", Username, Color.Blue).Build(), ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        foreach (var b in birthdays.Take(40))
            sb.AppendLine($"- {b.Mention} — **{b.NextDate:MMMM d}**");
        if (birthdays.Count > 40)
            sb.AppendLine($"*…and {birthdays.Count - 40} more.*");

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "🎂  Registered Birthdays", sb.ToString(), "", Username, Color.Blue).Build(), ephemeral: true);
    }


    /// <summary>Removes a member's registered birthday. Your own, or anyone's with Manage Server.</summary>
    [SlashCommand("birthdayremove", "Remove a member's registered birthday.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBirthdayRemoveAsync(SocketGuildUser user)
    {
        await DeferAsync(ephemeral: true);

        if (!MayManageBirthdayFor(user))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Birthday", "You can only remove your own birthday — removing someone else's needs the **Manage Server** permission.",
                Username).Build(), ephemeral: true);
            return;
        }

        int removed = await scheduling.RemoveBirthdayAsync(Context.Guild.Id.ToString(), user.Mention);

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            removed > 0 ? "🎂  Birthday Removed" : "🎂  Nothing to Remove",
            removed > 0 ? $"Removed **{user.DisplayName}'s** birthday." : $"**{user.DisplayName}** has no registered birthday.",
            "", Username, removed > 0 ? Color.Green : Color.Red).Build(), ephemeral: true);
    }

    /// <summary>True if the caller may add/remove a birthday for <paramref name="target"/> — always for themselves, otherwise Manage Server.</summary>
    private bool MayManageBirthdayFor(IUser target) =>
        target.Id == Context.User.Id ||
        Context.User is SocketGuildUser { GuildPermissions.ManageGuild: true };


    /// <summary>
    /// Sets a member's personal name-role to the given hex colour, creating it just below the
    /// bot's highest role if it doesn't exist. Setting your own is unrestricted; setting another
    /// member's needs Manage Roles. The bot needs Manage Roles either way.
    /// </summary>
    [SlashCommand("setrolecolor", "Set the colour of your personal role by hex code.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task HandleColorAsync(
        [Summary("hex", "Hex colour, e.g. #607C8C"), MinLength(1), MaxLength(10)] string hexCode,
        [Summary("member", "Whose colour to set (needs Manage Roles). Defaults to you.")]
        SocketGuildUser? member = null)
    {
        await DeferAsync(ephemeral: true);

        if (member is not null && member.Id != Context.User.Id &&
            Context.User is not SocketGuildUser { GuildPermissions.ManageRoles: true })
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Role Colour", "Setting another member's role colour needs the **Manage Roles** permission.",
                Username).Build(), ephemeral: true);
            return;
        }

        string bare = hexCode.TrimStart('#');

        if (!HexColor.TryParse(bare, out var roleColor))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Role Colour", $"`#{bare}` is not a valid hex code. Example: `#607C8C`", Username).Build(),
                ephemeral: true);
            return;
        }

        try
        {
            var guild = Context.Guild;
            var target = (IGuildUser)(member ?? (SocketGuildUser)Context.User);
            string name = target.Username;
            int botTop = guild.CurrentUser.Roles.Max(r => r.Position);

            if (guild.Roles.FirstOrDefault(r => r.Name == name) is { } existing)
            {
                await existing.ModifyAsync(p => p.Color = roleColor);
            }
            else
            {
                var created = await guild.CreateRoleAsync(name, null, roleColor, isHoisted: false, isMentionable: false);
                try { await created.ModifyAsync(p => p.Position = Math.Max(1, botTop - 1)); }
                catch { /* Discord can reject a position edit; the role still works, just lower down */ }
                await target.AddRoleAsync(created);
            }

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "🎨  Role Colour",
                $"{(target.Id == Context.User.Id ? "Your" : $"**{target.Username}**'s")} colour is now **#{bare.ToUpperInvariant()}**.",
                "", Username, roleColor).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Role Colour", $"Couldn't update the role: {ex.Message}", Username).Build(),
                ephemeral: true);
        }
    }


    /// <summary>Posts a native multi-select poll of the next 7 calendar days so members can vote on which days work for a given user's D&amp;D session.</summary>
    [SlashCommand("polldnd", "Poll the next 7 days to schedule a member's D&D session.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePollDndAsync(
        SocketGuildUser user,
        [Summary("duration_hours", "How many hours the poll stays open (1–768). Default 48."),
         MinValue(1), MaxValue(768)] int durationHours = 48)
    {
        await DeferAsync();

        var answers = Enumerable.Range(1, 7)
            .Select(i => DateTime.Now.AddDays(i))
            .Select(d => new PollMediaProperties { Text = $"{d.DayOfWeek} ({d:MM/dd})" })
            .ToList();

        var poll = new PollProperties
        {
            Question         = new PollMediaProperties { Text = $"Which days work for {user.DisplayName}'s D&D session?" },
            Answers          = answers,
            Duration         = (uint)durationHours,
            AllowMultiselect = true,
            // Must be set explicitly: Discord.Net serialises the CLR-default (0) otherwise,
            // and Discord rejects any layout_type other than 1 (PollLayout.Default).
            LayoutType       = PollLayout.Default,
        };

        // The text line is required: Discord.Net's followup precondition rejects a message with
        // no Content/Embed/File/Component and doesn't count the poll toward it.
        await FollowupAsync(
            text: $"📅 Scheduling poll started by **{Username}** for **{user.DisplayName}**",
            poll: poll,
            allowedMentions: AllowedMentions.None);
    }


    /// <summary>Forwards a user-submitted bug report to the bot owner's log channel, rate-limited to one per user per minute.</summary>
    [SlashCommand("reportbug", "Found a bug with the bot? Report it here.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleBugReportAsync(
        [MinLength(1), MaxLength(2000)] string bugFound)
    {
        var now = DateTime.UtcNow;
        if (_bugReportCooldowns.TryGetValue(Context.User.Id, out var last) && now - last < BugReportCooldown)
        {
            int wait = (int)Math.Ceiling((BugReportCooldown - (now - last)).TotalSeconds);
            await RespondAsync($"⏳ Please wait {wait}s before submitting another bug report.", ephemeral: true);
            return;
        }
        _bugReportCooldowns[Context.User.Id] = now;

        var channel = Context.Client.GetGuild(Constants.Constants.Bot.LogGuildId)
            ?.GetTextChannel(Constants.Constants.Bot.LogChannelId);

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
