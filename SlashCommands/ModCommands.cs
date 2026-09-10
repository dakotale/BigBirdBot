using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// <c>/mod</c> — standard moderation actions. Each subcommand requires the matching Discord
/// permission from both the invoking user and the bot, and refuses targets neither the caller
/// nor the bot outranks.
/// </summary>
[Group("mod", "Moderation actions.")]
[CommandContextType(InteractionContextType.Guild)]
public class ModCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private string Username => Context.User.Username;

    /// <summary>Kicks a member. Requires Kick Members.</summary>
    [SlashCommand("kick", "Kick a member from the server.")]
    [RequireUserPermission(GuildPermission.KickMembers)]
    [RequireBotPermission(GuildPermission.KickMembers)]
    public async Task KickAsync(
        SocketGuildUser member,
        [Summary("reason"), MaxLength(400)] string? reason = null)
    {
        await DeferAsync(ephemeral: true);
        if (!CanActOn(member, out string why)) { await Deny(why); return; }

        await member.KickAsync(reason);
        await Confirm($"Kicked **{member.DisplayName}**.{ReasonNote(reason)}");
    }

    /// <summary>Bans a member, optionally pruning recent messages. Requires Ban Members.</summary>
    [SlashCommand("ban", "Ban a member (optionally deleting their recent messages).")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task BanAsync(
        SocketGuildUser member,
        [Summary("delete_message_days", "Delete this many days of their messages (0–7)"), MinValue(0), MaxValue(7)] int deleteMessageDays = 0,
        [Summary("reason"), MaxLength(400)] string? reason = null)
    {
        await DeferAsync(ephemeral: true);
        if (!CanActOn(member, out string why)) { await Deny(why); return; }

        await member.BanAsync(deleteMessageDays, reason);
        await Confirm($"Banned **{member.DisplayName}**.{ReasonNote(reason)}");
    }

    /// <summary>Lifts a ban by user id. Requires Ban Members.</summary>
    [SlashCommand("unban", "Lift a ban by user ID.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task UnbanAsync([Summary("user_id", "The banned user's ID")] string userId)
    {
        await DeferAsync(ephemeral: true);

        if (!ulong.TryParse(userId, out ulong id)) { await Deny("That isn't a valid user ID."); return; }

        var ban = await Context.Guild.GetBanAsync(id);
        if (ban is null) { await Deny("That user isn't banned."); return; }

        await Context.Guild.RemoveBanAsync(id);
        await Confirm($"Unbanned **{ban.User.Username}** (`{id}`).");
    }

    /// <summary>Times a member out for N minutes (max 28 days). Requires Moderate Members.</summary>
    [SlashCommand("timeout", "Time a member out for a number of minutes (max 28 days).")]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task TimeoutAsync(
        SocketGuildUser member,
        [Summary("minutes", "How many minutes (1–40320)"), MinValue(1), MaxValue(40320)] int minutes,
        [Summary("reason"), MaxLength(400)] string? reason = null)
    {
        await DeferAsync(ephemeral: true);
        if (!CanActOn(member, out string why)) { await Deny(why); return; }

        await member.SetTimeOutAsync(TimeSpan.FromMinutes(minutes),
            new RequestOptions { AuditLogReason = reason });
        await Confirm($"Timed **{member.DisplayName}** out for **{minutes} min**.{ReasonNote(reason)}");
    }

    /// <summary>Clears a member's active timeout. Requires Moderate Members.</summary>
    [SlashCommand("untimeout", "Clear a member's timeout.")]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task UntimeoutAsync(SocketGuildUser member)
    {
        await DeferAsync(ephemeral: true);

        if (member.TimedOutUntil is null || member.TimedOutUntil <= DateTimeOffset.UtcNow)
        {
            await Deny($"**{member.DisplayName}** isn't timed out.");
            return;
        }

        await member.RemoveTimeOutAsync();
        await Confirm($"Cleared **{member.DisplayName}**'s timeout.");
    }

    /// <summary>Sets the current text channel's slowmode. Requires Manage Channels.</summary>
    [SlashCommand("slowmode", "Set this channel's slowmode delay in seconds (0 disables, max 21600).")]
    [RequireUserPermission(ChannelPermission.ManageChannels)]
    [RequireBotPermission(ChannelPermission.ManageChannels)]
    public async Task SlowmodeAsync(
        [Summary("seconds", "0–21600 (6h); 0 disables"), MinValue(0), MaxValue(21600)] int seconds)
    {
        await DeferAsync(ephemeral: true);

        if (Context.Channel is not ITextChannel text) { await Deny("This isn't a text channel."); return; }

        await text.ModifyAsync(c => c.SlowModeInterval = seconds);
        await Confirm(seconds == 0 ? "Slowmode disabled in this channel." : $"Slowmode set to **{seconds}s** in this channel.");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Blocks acting on yourself, the bot, the server owner, or anyone the caller/bot doesn't outrank.</summary>
    private bool CanActOn(SocketGuildUser member, out string reason)
    {
        reason = "";
        if (member.Id == Context.User.Id) { reason = "You can't do that to yourself."; return false; }
        if (member.Id == Context.Client.CurrentUser.Id) { reason = "I'm not going to do that to myself."; return false; }
        if (member.Id == Context.Guild.OwnerId) { reason = "You can't act on the server owner."; return false; }

        var invoker = (SocketGuildUser)Context.User;
        if (invoker.Id != Context.Guild.OwnerId && member.Hierarchy >= invoker.Hierarchy)
        {
            reason = "That member's highest role is above (or equal to) yours.";
            return false;
        }
        if (member.Hierarchy >= Context.Guild.CurrentUser.Hierarchy)
        {
            reason = "That member's highest role is above mine — move my role up in Server Settings → Roles.";
            return false;
        }
        return true;
    }

    private static string ReasonNote(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "" : $"\n> {reason}";

    private Task Confirm(string body) =>
        FollowupAsync(embed: _embed.BuildMessageEmbed("🔨  Moderation", body, "", Username, Color.Green).Build(), ephemeral: true);

    private Task Deny(string body) =>
        FollowupAsync(embed: _embed.BuildErrorEmbed("Moderation", body, Username).Build(), ephemeral: true);
}


/// <summary><c>/role</c> — add or remove a single role on a member. Requires Manage Roles (user and bot).</summary>
[Group("role", "Add or remove a role on a member.")]
[CommandContextType(InteractionContextType.Guild)]
[RequireUserPermission(GuildPermission.ManageRoles)]
[RequireBotPermission(GuildPermission.ManageRoles)]
public class RoleCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private string Username => Context.User.Username;

    /// <summary>Gives a member a role.</summary>
    [SlashCommand("add", "Give a member a role.")]
    public async Task AddAsync(SocketGuildUser member, IRole role)
    {
        await DeferAsync(ephemeral: true);
        if (!Assignable(role, out string why)) { await Deny(why); return; }
        if (member.Roles.Any(r => r.Id == role.Id)) { await Deny($"**{member.DisplayName}** already has **{role.Name}**."); return; }

        await member.AddRoleAsync(role);
        await Confirm($"Gave **{member.DisplayName}** the **{role.Name}** role.");
    }

    /// <summary>Removes a role from a member.</summary>
    [SlashCommand("remove", "Take a role away from a member.")]
    public async Task RemoveAsync(SocketGuildUser member, IRole role)
    {
        await DeferAsync(ephemeral: true);
        if (!Assignable(role, out string why)) { await Deny(why); return; }
        if (member.Roles.All(r => r.Id != role.Id)) { await Deny($"**{member.DisplayName}** doesn't have **{role.Name}**."); return; }

        await member.RemoveRoleAsync(role);
        await Confirm($"Removed the **{role.Name}** role from **{member.DisplayName}**.");
    }

    /// <summary>Rejects managed roles, @everyone, and roles above the caller's or the bot's top role.</summary>
    private bool Assignable(IRole role, out string reason)
    {
        reason = "";
        if (role.IsManaged) { reason = $"**{role.Name}** is managed by an integration and can't be assigned manually."; return false; }
        if (role.Id == Context.Guild.EveryoneRole.Id) { reason = "That's the @everyone role."; return false; }
        if (role.Position >= Context.Guild.CurrentUser.Hierarchy) { reason = $"**{role.Name}** is above my highest role — move my role up."; return false; }

        var invoker = (SocketGuildUser)Context.User;
        if (invoker.Id != Context.Guild.OwnerId && role.Position >= invoker.Hierarchy)
        {
            reason = $"**{role.Name}** is above (or equal to) your highest role.";
            return false;
        }
        return true;
    }

    private Task Confirm(string body) =>
        FollowupAsync(embed: _embed.BuildMessageEmbed("🧩  Roles", body, "", Username, Color.Green).Build(), ephemeral: true);

    private Task Deny(string body) =>
        FollowupAsync(embed: _embed.BuildErrorEmbed("Roles", body, Username).Build(), ephemeral: true);
}
