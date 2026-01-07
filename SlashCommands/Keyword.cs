using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    public class Keyword : InteractionModuleBase<SocketInteractionContext>
    {
        #region Add Keywords
        [SlashCommand("addkeyword", "Adds a keyword that can access multiple actions.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleAddKeyMulti([MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                StoredProcedure stored = new StoredProcedure();
                keyword = keyword.Trim();
                string addCommand = "add" + keyword;

                string createdBy = Context.User.Username;
                long serverId = Int64.Parse(Context.Guild.Id.ToString());
                EmbedHelper embed = new EmbedHelper();
                string desc = $"Added Command Successfully.";
                string createdByMsg = "Command from: " + Context.User.Username;

                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddChatKeywordMap", new List<System.Data.SqlClient.SqlParameter>
                {
                    new SqlParameter("@ServerID", serverId),
                    new SqlParameter("@Keyword", keyword),
                    new SqlParameter("@AddKeyword", addCommand),
                    new SqlParameter("@CreatedBy", createdBy)
                });

                await FollowupAsync(embed: embed.BuildMessageEmbed("Keyword Multi Added", desc, "", createdByMsg, Color.Blue).Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                EmbedHelper embed = new EmbedHelper();
                string title = "Keyword Multi Error";
                string desc = ex.Message;
                string createdByMsg = "Command from: " + Context.User.Username;
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build(), ephemeral: true);
            }
        }

        [SlashCommand("addmultieventadmin", "Adds a scheduled job to send a photo for a user.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleUserEventAdminAdd(SocketGuildUser user, [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                StoredProcedure stored = new StoredProcedure();
                EmbedHelper embedHelper = new EmbedHelper();

                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "AddUsersScheduledKeyword", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(user.Id.ToString())),
                    new SqlParameter("@Keyword", keyword.Trim())
                });

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        EmbedBuilder embed = embedHelper.BuildMessageEmbed("Multiple Keyword User Added", $"{keyword} was successfully added and **{user.Username}** will start receiving this on {DateTime.Parse(dr["ScheduleTime"].ToString()).ToString("MM/dd/yyyy hh:mm t")} ET.\nThe current list of multi people/characters for this user are; *{dr["ScheduledEventTable"].ToString()}*", "", Context.User.Username, Color.Blue, "");
                        await FollowupAsync(embed: embed.Build(), ephemeral: true);
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }
        #endregion

        #region Delete Keywords
        [SlashCommand("deletemultiurl", "Deletes a multi-keyword URL with a given table and link.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleChatKeywordURLDelete([MinLength(1)] string url, [MinLength(1), MaxLength(50)] string chatName)
        {
            await DeferAsync(ephemeral: true);

            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();
            string tableName = chatName.Trim();
            url = url.Trim();

            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteChatKeywordURL", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", url),
                new SqlParameter("@Keyword", chatName.Trim())
            });

            EmbedBuilder embed = embedHelper.BuildMessageEmbed("Delete Successful", $"URL {url} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }

        [SlashCommand("deletekeyword", "Deletes a multi-keyword that was created.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleChatKeywordDelete([MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embedHelper = new EmbedHelper();

            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteChatKeyword", new List<SqlParameter>
            {
                new SqlParameter("@Keyword", keyword.Trim()),
            });

            EmbedBuilder embed = embedHelper.BuildMessageEmbed("Delete Successful", "The multi-keyword provided was removed successfully.", "", "", Color.Blue, "");
            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        #endregion

        #region Misc Keyword Functions
        [SlashCommand("requeuemulti", "Requeue a keyword event if something goes wrong when sending the scheduled action.")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task RequeueThirst(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);
            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "UpdateUsersScheduledKeywordRequeue", new List<SqlParameter>
            {
                new SqlParameter("@UserID", user.Id.ToString())
            });

            foreach (DataRow dr in dt.Rows)
            {
                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Event Requeue", dr["Message"].ToString(), "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
            }
        }
        #endregion
    }
}
