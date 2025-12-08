using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using Discord.Net.Extensions.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    // GuildModule decoration limits these commands to only show by the guild below.
    [GuildModule(880569055856185354)]
    public class OwnerCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("announcement", "Broadcast a message to all servers.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleAnnouncement([MinValue(1), MaxLength(4000)] string message, Attachment attachment = null)
        {
            await DeferAsync(ephemeral: true);
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
                        SocketGuild guild = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString()));
                        SocketTextChannel textChannel = guild.GetTextChannel(ulong.Parse(dr["DefaultChannelID"].ToString()));
                        if (textChannel != null)
                        {
                            IUser bot = guild.Users.Where(s => s.IsBot && s.Username.Contains("BigBirdBot")).FirstOrDefault();
                            if (bot != null)
                            {
                                SocketGuildUser? user = textChannel.Users.Where(s => s.Id == bot.Id).FirstOrDefault();
                                if (user != null)
                                {
                                    ChannelPermissions permissions = user.GetPermissions(textChannel);
                                    if (permissions.SendMessages)
                                    {
                                        serverList.Add(guild.Name);
                                        await textChannel.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("Announcement", message, "", "BigBirdBot", Discord.Color.Gold, imageUrl).Build()).ConfigureAwait(false);
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
                await FollowupAsync($"Announcement sent to **{result}**.\nNot Sent: {string.Join(delimiter, serverListNoPerms)}", ephemeral: true).ConfigureAwait(false);
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
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetScheduledEventUsers", new List<SqlParameter>());
            EmbedHelper embedHelper = new EmbedHelper();
            string description = "";

            if (dt.Rows.Count > 0)
                foreach (DataRow dr in dt.Rows)
                    description += "- " + dr["Username"].ToString() + " - " + dr["ScheduledEventTable"].ToString() + " - " + DateTime.Parse(dr["EventDateTime"].ToString()).ToString("MM/dd hh:mm tt") + "\n";

            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Scheduled List", description, "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true).ConfigureAwait(false);
        }

        [SlashCommand("connplayers", "List of all connected players in voice channels.")]
        [EnabledInDm(true)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePlayersConnected()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetPlayerConnected", new List<SqlParameter>());
            EmbedHelper embed = new EmbedHelper();

            string title = "Players Connected";
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
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build(), ephemeral: true);
            }
            else
            {
                desc = "No Players are connected at this time.";
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build(), ephemeral: true);
            }
        }

        [SlashCommand("populateallusers", "Populate users into the DB.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePopulateAllUserCommand()
        {
            await DeferAsync(ephemeral: true);
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
                        List<SocketGuildUser> users = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).Users.Where(s => s.IsBot == false && s.IsWebhook == false).ToList() ?? new List<SocketGuildUser>();
                        if (users.Count > 0)
                        {
                            foreach (SocketGuildUser? u in users)
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

                await FollowupAsync("User table updated.", ephemeral: true);
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
            await DeferAsync(ephemeral: true);

            EmbedHelper embedHelper = new EmbedHelper();
            string tableName = chatName.Trim();
            fileName = @"C:\Temp\DiscordBot\" + tableName + @"\" + fileName.Trim();

            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "CheckIfThirstURLExists", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", fileName),
                new SqlParameter("@TableName", tableName)
            });

            if (dt.Rows.Count > 0)
            {
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteThirstURL", new List<SqlParameter>
                {
                    new SqlParameter("@FilePath", fileName),
                    new SqlParameter("@TableName", tableName)
                });

                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Delete Successful", $"Image {fileName} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            else
            {
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", "The image doesn't exist in the table provided or the table doesn't exist.", Constants.Constants.errorImageUrl, Context.User.Username, Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }

        [SlashCommand("editbotavatar", "Change the bot's avatar from the blue bird to anything you would like.")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task HandleBotAvatar(Attachment attachment)
        {
            await DeferAsync(ephemeral: true);
            EmbedHelper embed = new EmbedHelper();

            // 10MB is the max attachment size for free users
            if (attachment.ContentType.StartsWith("image/") && attachment.Size < 10000000)
            {
                try
                {
                    // Download into memory to avoid file locks and race conditions with async file download
                    using (var httpClient = new HttpClient())
                    {
                        byte[] imageBytes = await httpClient.GetByteArrayAsync(attachment.Url).ConfigureAwait(false);

                        // Create image from memory stream and set avatar while the stream is open
                        using (var ms = new MemoryStream(imageBytes))
                        {
                            var image = new Image(ms);
                            await Context.Client.CurrentUser.ModifyAsync(user => user.Avatar = image).ConfigureAwait(false);
                        }
                    }

                    await FollowupAsync(embed: embed.BuildMessageEmbed("Edit Bot Avatar", "The bot's avatar was successfully updated.", "", Context.User.Username, Discord.Color.Blue, attachment.Url).Build(), ephemeral: true).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    await FollowupAsync(embed: embed.BuildErrorEmbed("Edit Bot Avatar", $"Failed to download the provided image: {ex.Message}", Context.User.Username).Build(), ephemeral: true).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    await FollowupAsync(embed: embed.BuildErrorEmbed("Edit Bot Avatar", $"File access error while processing avatar: {ex.Message}", Context.User.Username).Build(), ephemeral: true).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    await FollowupAsync(embed: embed.BuildErrorEmbed("Edit Bot Avatar", $"Unexpected error: {ex.Message}", Context.User.Username).Build(), ephemeral: true).ConfigureAwait(false);
                }
            }
            else
            {
                await FollowupAsync(embed: embed.BuildErrorEmbed("Edit Bot Avatar", "The provided attachment is not a valid avatar image.", Context.User.Username).Build(), ephemeral: true).ConfigureAwait(false);
            }
        }

    }
}
