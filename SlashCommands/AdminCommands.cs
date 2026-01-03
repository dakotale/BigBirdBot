using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands
{
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("pronoun", "Select a list of available pronouns.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandlePronoun()
        {
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.discordBotConnStr;
            DataTable dt = new DataTable();
            EmbedHelper embed = new EmbedHelper();
            await DeferAsync();

            // We will need to implement clickable buttons with the pronouns returned from the DB as a modal
            ComponentBuilder builder = new ComponentBuilder();

            dt = stored.Select(connStr, "GetPronouns", new List<SqlParameter>());

            foreach (DataRow dr in dt.Rows)
                builder.WithButton(dr["Pronoun"].ToString(), dr["ID"].ToString());

            await FollowupAsync(embed: embed.BuildMessageEmbed("Pronoun Selection", "Please select from the list of available pronouns.", "", Context.User.Username, Discord.Color.Blue).Build(), components: builder.Build()).ConfigureAwait(false);
        }

        [SlashCommand("editbotnickname", "Change the bot's nickname from BigBirdBot to anything you would like.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleBotNickname(string nickName)
        {
            await DeferAsync(ephemeral: true);
            EmbedHelper embed = new EmbedHelper();

            await Context.Guild.CurrentUser.ModifyAsync(s => s.Nickname = nickName);
            await FollowupAsync(embed: embed.BuildMessageEmbed("Edit Bot Nickname", "The bot's nickname was successfully updated to **" + nickName + "**.", "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true).ConfigureAwait(false);
        }
    }
}
