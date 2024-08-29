using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;
using Discord;
using Fergun.Interactive;
using Discord.WebSocket;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;

namespace DiscordBot.SlashCommands
{
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly InteractiveService _interactive;

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
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

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
            var builder = new ComponentBuilder();

            dt = stored.Select(connStr, "GetPronouns", new List<SqlParameter>());

            foreach(DataRow dr in dt.Rows)
                builder.WithButton(dr["Pronoun"].ToString(), dr["ID"].ToString());

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Pronoun Selection", "Please select from the list of available pronouns.", "", Context.User.Username, Discord.Color.Blue).Build(), components: builder.Build()).ConfigureAwait(false);
        }

        [SlashCommand("roles", "Select a list of available roles.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleRoles()
        {
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.discordBotConnStr;
            DataTable dt = new DataTable();
            EmbedHelper embed = new EmbedHelper();
            await DeferAsync();

            dt = stored.Select(connStr, "GetRoles", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())) });

            if (dt.Rows.Count > 0)
            {
                // We will need to implement clickable buttons with the pronouns returned from the DB as a modal
                var builder = new ComponentBuilder();

                foreach (DataRow dr in dt.Rows)
                    builder.WithButton(dr["RoleName"].ToString(), dr["RoleID"].ToString());

                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", "Please select from the list of available roles.", "", Context.User.Username, Discord.Color.Blue).Build(), components: builder.Build()).ConfigureAwait(false);
            }
            else
            {
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", "There are no roles available to select.", Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Red).Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("addkeymultiroles", "Create a roles based on channels in the multiple action keyword category.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleCreateRoleToChannel()
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            DataTable dt = new DataTable();
            string connStr = Constants.Constants.discordBotConnStr;
            var serverId = Int64.Parse(Context.Guild.Id.ToString());
            var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));

            dt = stored.Select(connStr, "GetRoles", new List<SqlParameter> { new SqlParameter("@ServerID", serverId) });

            if (dt.Rows.Count > 24)
            {
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Roles Error", "The limit of 25 roles have been added, please delete a role before adding one.", Constants.Constants.errorImageUrl, Context.User.Username, Color.Red).Build());
                return;
            }

            var categoryIdList = guild.CategoryChannels.Where(s => s.Name.ToLower() == "thirsting" || s.Name.ToLower() == "stanning" || s.Name.ToLower() == "keyword multi").ToList(); // prod: thirsting
            ulong categoryId = default(ulong);

            if (categoryIdList.Any())
            {
                foreach (var c in categoryIdList)
                {
                    foreach (var t in c.Channels)
                    {
                        // Check if the role exists for channel
                        // If it doesn't exist, create one
                        if (guild.Roles.Where(s => s.Name.Equals(t.Name)).Count() == 0)
                        {
                            var role = await guild.CreateRoleAsync(t.Name);

                            if (role != null)
                            {
                                stored.UpdateCreate(connStr, "AddRoles", new List<SqlParameter>
                                {
                                    new SqlParameter("@RoleID", Int64.Parse(role.Id.ToString())),
                                    new SqlParameter("@RoleName", role.Name),
                                    new SqlParameter("@ServerID", serverId)
                                });

                                // Map the role to the channel as a permission
                                var permissionOverrides = new OverwritePermissions(viewChannel: PermValue.Allow);
                                await t.AddPermissionOverwriteAsync(role, permissionOverrides).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Multi-Keyword Role Added", "Role was added successfully.", "", Context.User.Username, Discord.Color.Blue).Build());
        }

        [SlashCommand("delkeymultiroles", "Delete a role based on channels in the multiple action keyword category.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleDeleteRoleToChannel([MinLength(1)] string roleName)
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            DataTable dt = new DataTable();
            string connStr = Constants.Constants.discordBotConnStr;
            var serverId = Int64.Parse(Context.Guild.Id.ToString());
            var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));

            var categoryIdList = guild.CategoryChannels.Where(s => s.Name.ToLower() == "thirsting" || s.Name.ToLower() == "stanning" || s.Name.ToLower() == "keyword multi").ToList();
            if (categoryIdList.Count > 0) 
            {
                stored.UpdateCreate(connStr, "DeleteRoles", new List<SqlParameter>
                {
                    new SqlParameter("@RoleName", roleName.Trim()),
                    new SqlParameter("@ServerID", serverId)
                });
            }

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Multi-Keyword Role Deleted", "Role was deleted successfully.", "", Context.User.Username, Discord.Color.Blue).Build());
        }

        [SlashCommand("addrole", "Add a role for the bot to handle when the roles command is ran.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleAddRoles(SocketRole role)
        {
            await DeferAsync();

            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.discordBotConnStr;
            EmbedHelper embed = new EmbedHelper();

            stored.UpdateCreate(connStr, "AddRoles", new List<SqlParameter>
            {
                new SqlParameter("@RoleID", Int64.Parse(role.Id.ToString())),
                new SqlParameter("@RoleName", role.Name),
                new SqlParameter("@ServerID", Int64.Parse(role.Guild.Id.ToString()))
            });

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Added to Role Selection", "Role was added successfully.", "", Context.User.Username, Discord.Color.Blue).Build()).ConfigureAwait(false);
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

        [SlashCommand("chattothread", "Move items from a chat into a thread and create the thread.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleChattoThread([MinValue(1), MaxValue(100)] int numberOfMsgs, string threadName, SocketGuildUser user)
        {
            await DeferAsync();
            var channel = Context.Channel;
            var msgs = await channel.GetMessagesAsync(numberOfMsgs, CacheMode.AllowDownload, RequestOptions.Default).FlattenAsync();

            var msgsofUser = msgs.Where(s => s.Author.Username.Equals(user.Username)).OrderBy(s => s.CreatedAt).ToList();
            var textChannel = channel as SocketTextChannel;
            SocketThreadChannel? thread = null;

            if (textChannel != null)
            {
                thread = await textChannel.CreateThreadAsync(threadName, ThreadType.PublicThread, ThreadArchiveDuration.OneWeek, msgsofUser.First(), true).ConfigureAwait(false);

                foreach (var msg in msgsofUser)
                    await thread.SendMessageAsync(msg.CleanContent).ConfigureAwait(false);

                await FollowupAsync($"{user.Mention}, the {threadName} thread was created here {thread.Mention}");
            }
            else
                await FollowupAsync("An error occurred generating the thread.");

        }
    }
}
