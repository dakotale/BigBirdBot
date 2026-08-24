using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands
{
    /// <summary>/autorole subcommands — configure, clear, or check the role auto-assigned to new members (applied by BotHost.AssignAutoRoleAsync).</summary>
    [Group("autorole", "Configure the role automatically assigned to new members.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public class AutoRoleCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();

        private string Username => Context.User.Username;

        /// <summary>Sets (or replaces) the guild's auto-role.</summary>
        [SlashCommand("set", "Set the role to assign when a new member joins.")]
        public async Task HandleSetAsync(
            [Summary("role", "The role to assign on join.")] IRole role)
        {
            await DeferAsync(ephemeral: true);

            long guildId = (long)Context.Guild.Id;
            var existing = await db.GuildAutoRoles.FindAsync(guildId);
            if (existing is not null)
            {
                existing.RoleId = (long)role.Id;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.GuildAutoRoles.Add(new GuildAutoRole { GuildId = guildId, RoleId = (long)role.Id });
            }
            await db.SaveChangesAsync();

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Auto-Role Set",
                $"New members will automatically be given the **{role.Name}** role.",
                "", Username, EmbedColors.Green).Build(), ephemeral: true);
        }

        /// <summary>Removes the guild's auto-role configuration entirely.</summary>
        [SlashCommand("clear", "Remove the auto-role setting for this server.")]
        public async Task HandleClearAsync()
        {
            await DeferAsync(ephemeral: true);

            var existing = await db.GuildAutoRoles.FindAsync((long)Context.Guild.Id);
            if (existing is not null)
            {
                db.GuildAutoRoles.Remove(existing);
                await db.SaveChangesAsync();
            }

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Auto-Role Cleared",
                "New members will no longer be assigned a role on join.",
                "", Username, EmbedColors.Grey).Build(), ephemeral: true);
        }

        /// <summary>Shows the currently configured auto-role, if any (noting if the role has since been deleted).</summary>
        [SlashCommand("status", "Show the current auto-role configuration.")]
        public async Task HandleStatusAsync()
        {
            await DeferAsync(ephemeral: true);

            var autoRole = await db.GuildAutoRoles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.GuildId == (long)Context.Guild.Id);

            if (autoRole is null)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Auto-Role Status",
                    "No auto-role is configured for this server.",
                    "", Username, EmbedColors.Grey).Build(), ephemeral: true);
                return;
            }

            ulong roleId = (ulong)autoRole.RoleId;
            var role = Context.Guild.GetRole(roleId);
            string roleName = role is not null ? $"**{role.Name}**" : $"<deleted role `{roleId}`>";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Auto-Role Status",
                $"New members are assigned {roleName} when they join.",
                "", Username, EmbedColors.Blue).Build(), ephemeral: true);
        }
    }
}
