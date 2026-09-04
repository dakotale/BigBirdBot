using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands
{
    /// <summary>Server moderation/admin utility commands: pronoun-role menu, bot nickname, message purge, and announcement toggling.</summary>
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private readonly StoredProcedure _sp = new();

        private string Username => Context.User.Username;

        /// <summary>Posts a button menu letting members self-assign a pronoun role (handled by BotHost.OnButtonExecutedAsync).</summary>
        [SlashCommand("pronoun", "Post a pronoun selection menu for members.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandlePronounAsync()
        {
            await DeferAsync();

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPronouns", []);

            var builder = new ComponentBuilder();
            foreach (DataRow dr in dt.Rows)
                builder.WithButton(dr["Pronoun"].ToString(), dr["ID"].ToString());

            await FollowupAsync(
                embed: _embed.BuildMessageEmbed(
                    "Pronoun Selection",
                    "Select your pronouns from the list below.",
                    "", Username, Color.Blue).Build(),
                components: builder.Build());
        }

        /// <summary>Changes the bot's own server nickname.</summary>
        [SlashCommand("editbotnickname", "Change the bot's nickname in this server.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleBotNicknameAsync(
            [MinLength(1), MaxLength(32)] string nickName)
        {
            await DeferAsync(ephemeral: true);
            await Context.Guild.CurrentUser.ModifyAsync(p => p.Nickname = nickName);
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Bot Nickname Updated",
                $"Nickname changed to **{nickName}**.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }

        /// <summary>
        /// Bulk-deletes up to 100 messages from the current channel.
        /// Discord only allows bulk-delete on messages newer than 14 days;
        /// older messages are skipped and the skipped count is reported.
        /// </summary>
        [SlashCommand("purge", "Bulk-delete up to 100 messages from this channel.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task HandlePurgeAsync(
            [MinValue(1), MaxValue(100),
         Summary("count", "Number of messages to delete (1–100)")] int count)
        {
            await DeferAsync(ephemeral: true);

            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();

            // Discord bulk-delete only works on messages < 14 days old.
            var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
            var eligible = messages.Where(m => m.Timestamp > cutoff).ToList();
            int skipped = messages.Count() - eligible.Count;

            if (eligible.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Purge", "No messages found that are younger than 14 days.", Username).Build(),
                    ephemeral: true);
                return;
            }

            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(eligible);

            string note = skipped > 0
                ? $"\n*{skipped} message(s) older than 14 days were skipped.*"
                : "";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "🗑️  Purge Complete",
                $"Deleted **{eligible.Count}** message(s).{note}",
                "", Username, Color.Orange).Build(), ephemeral: true);
        }

        /// <summary>
        /// Toggles timed bot announcements (the hourly word puzzle, birthday greetings) for this server.
        /// Defaults to disabled — must be explicitly enabled by an admin.
        /// </summary>
        [SlashCommand("announcements", "Toggle timed bot announcements (word puzzle, birthdays) for this server.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task HandleAnnouncementsAsync()
        {
            await DeferAsync(ephemeral: true);

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "ToggleAnnouncements",
            [
                new SqlParameter("@ServerUID", (long)Context.Guild.Id),
                new SqlParameter("@ChannelID", (long)Context.Channel.Id)
            ]);

            string result = dt.Rows.Count > 0 ? dt.Rows[0]["Result"].ToString() ?? "" : "Unknown error toggling announcements.";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "📣  Announcements",
                result,
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }
}

