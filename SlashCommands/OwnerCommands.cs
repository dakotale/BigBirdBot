using Discord;
using Discord.Interactions;
using Discord.Net.Extensions.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;
using Discord.WebSocket;

namespace DiscordBot.SlashCommands
{
    [GuildModule(880569055856185354)]
    public class OwnerCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("announcement", "ONLY THE BOT OWNER CAN RUN THIS - Broadcast a message to all server.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleAnnouncement([MinValue(1), MaxLength(4000)] string message, Attachment attachment = null)
        {
            await DeferAsync();
            List<string> serverList = new List<string>();
            List<string> serverListNoPerms = new List<string>();
            try
            {
                StoredProcedure stored = new StoredProcedure();
                string imageUrl = "";

                if (attachment != null)
                    imageUrl = attachment.Url;

                // GetServer ulong IDs
                // var test = Context.Client.GetGuild(id).Users.Where(s => s.IsBot == false).ToList();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServersNonNullDefaultChannel", new List<SqlParameter>());
                EmbedHelper embedHelper = new EmbedHelper();
                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        var guild = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString()));
                        var textChannel = guild.GetTextChannel(ulong.Parse(dr["DefaultChannelID"].ToString()));
                        if (textChannel != null)
                        {
                            IUser bot = guild.Users.Where(s => s.IsBot && s.Username.Contains("BigBirdBot")).FirstOrDefault();
                            if (bot != null)
                            {
                                var user = textChannel.Users.Where(s => s.Id == bot.Id).FirstOrDefault();
                                if (user != null)
                                {
                                    var permissions = user.GetPermissions(textChannel as IGuildChannel);
                                    if (permissions.SendMessages)
                                    {
                                        serverList.Add(guild.Name);
                                        await textChannel.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Announcement", message, "", "BigBirdBot", Discord.Color.Gold, imageUrl).Build()).ConfigureAwait(false);
                                    }
                                    else
                                        serverListNoPerms.Add(guild.Name);
                                }
                            }
                        }
                    }
                }
                string delimiter = ", ";
                string result = string.Join(delimiter, serverList);
                await FollowupAsync($"Announcement sent to **{result}**.\nNot Sent: {string.Join(delimiter, serverListNoPerms)}").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("", e.Message, Context.User.Username).Build());
            }
        }

        [SlashCommand("schedulelist", "Get list of all users scheduled times.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleServerList()
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetScheduledEventUsers", new List<SqlParameter>());
            EmbedHelper embedHelper = new EmbedHelper();
            string description = "";

            if (dt.Rows.Count > 0)
                foreach (DataRow dr in dt.Rows)
                    description += "- " + dr["Username"].ToString() + " - " + dr["ScheduledEventTable"].ToString() + " - " + DateTime.Parse(dr["EventDateTime"].ToString()).ToString("MM/dd hh:mm tt") + "\n";

            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Scheduled List", description, "", Context.User.Username, Discord.Color.Blue).Build()).ConfigureAwait(false);
        }

        [SlashCommand("log", "Most recent error message in the bot.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleLog()
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            string output = "";

            DataTable dt = stored.Select(connStr, "GetLog", new List<SqlParameter>());
            EmbedHelper embedHelper = new EmbedHelper();

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    output += $"__Most Recent Error Message Reported__\nDate Logged: {dr["CreatedOn"].ToString()}\nSource: {dr["Source"].ToString()}\nSeverity: {dr["Severity"].ToString()}\nMessage: {dr["Message"].ToString()}\nException: {dr["Exception"].ToString()}";
                }
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Log", output, Context.User.Username).Build());
            }
            else
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Log", "No recent exceptions found.", Context.User.Username).Build());
        }

        [SlashCommand("connplayers", "List of all connected players in voice channels.")]
        [EnabledInDm(true)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePlayersConnected()
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetPlayerConnected", new List<SqlParameter>());
            EmbedHelper embed = new EmbedHelper();

            string title = "BigBirdBot - Players Connected";
            string desc = "";
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            if (dt.Rows.Count > 0)
            {
                desc = $"Total Players Connected: {dt.Rows.Count}\n";
                foreach (DataRow dr in dt.Rows)
                {
                    desc += "\n- " + dr["ServerName"];
                }
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build());
            }
            else
            {
                desc = "No Players are connected at this time.";
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build());
            }
        }

        [SlashCommand("populateallusers", "Populate users into the DB.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePopulateAllUserCommand()
        {
            await DeferAsync();
            try
            {
                StoredProcedure stored = new StoredProcedure();

                // GetServer ulong IDs
                // var test = Context.Client.GetGuild(id).Users.Where(s => s.IsBot == false).ToList();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServers", new List<SqlParameter>());

                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        var users = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).Users.Where(s => s.IsBot == false && s.IsWebhook == false).ToList() ?? new List<SocketGuildUser>();
                        if (users.Count > 0)
                        {
                            foreach (var u in users)
                            {
                                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddUser", new List<SqlParameter>
                                {
                                    new SqlParameter("@UserID", u.Id.ToString()),
                                    new SqlParameter("@Username", u.Username),
                                    new SqlParameter("@JoinDate", u.JoinedAt),
                                    new SqlParameter("@ServerUID", Int64.Parse(u.Guild.Id.ToString())),
                                    new SqlParameter("@Nickname", u.Nickname)
                                });

                                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddUserByServer", new List<SqlParameter>
                                {
                                    new SqlParameter("@UserID", u.Id.ToString()),
                                    new SqlParameter("@Username", u.Username),
                                    new SqlParameter("@JoinDate", u.JoinedAt),
                                    new SqlParameter("@ServerUID", Int64.Parse(u.Guild.Id.ToString())),
                                    new SqlParameter("@Nickname", u.Nickname)
                                });
                            }
                        }
                    }
                }

                await FollowupAsync("User table updated.");
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("", e.Message, Context.User.Username).Build());
            }
        }

        [SlashCommand("delmultiimage", "Deletes a multi-keyword image with a given path")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task HandleThirstImageDelete([MinLength(1)] string fileName, [MinLength(1)] string chatName)
        {
            await DeferAsync();

            EmbedHelper embedHelper = new EmbedHelper();
            string tableName = chatName.Trim();
            fileName = @"C:\Temp\DiscordBot\" + tableName + @"\" + fileName.Trim();

            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "CheckIfThirstURLExists", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", fileName),
                new SqlParameter("TableName", tableName)
            });

            if (dt.Rows.Count > 0)
            {
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteThirstURL", new List<SqlParameter>
                {
                    new SqlParameter("@FilePath", fileName),
                    new SqlParameter("TableName", tableName)
                });

                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Delete Successful", $"Image {fileName} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The image doesn't exist in the table provided or the table doesn't exist.", Constants.Constants.errorImageUrl, Context.User.Username, Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

    }
}
