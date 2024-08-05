using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;
using Discord;
using Fergun.Interactive;
using Discord.WebSocket;

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
                        await Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).GetTextChannel(ulong.Parse(dr["DefaultChannelID"].ToString())).SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Announcement", message, "", "BigBirdBot", Discord.Color.Gold, imageUrl).Build());
                }

                await FollowupAsync("Announcement sent.");
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await FollowupAsync(embed: embed.Build());
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

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Pronoun Selection", "Please select from the list of available pronouns.", "", Context.User.Username, Discord.Color.Blue).Build(), components: builder.Build());
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

                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", "Please select from the list of available roles.", "", Context.User.Username, Discord.Color.Blue).Build(), components: builder.Build());
            }
            else
            {
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", "There are no roles available to select.", Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Red).Build());
            }
        }

        [SlashCommand("thirstingroles", "Create a roles based on channels in the thirsting category.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task HandleCreateRoleToChannel()
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            string connStr = Constants.Constants.discordBotConnStr;
            var serverId = Int64.Parse(Context.Guild.Id.ToString());
            var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
            var categoryIdList = guild.CategoryChannels.Where(s => s.Name == "thirsting").ToList(); // prod: thirsting
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
                                await t.AddPermissionOverwriteAsync(role, permissionOverrides);
                            }
                        }
                    }
                }
            }

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Thirsting Roles Added", "Role were added successfully.", "", Context.User.Username, Discord.Color.Blue).Build());
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

            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Added to Role Selection", "Role was added successfully.", "", Context.User.Username, Discord.Color.Blue).Build());
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

            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Scheduled List", description, "", Context.User.Username, Discord.Color.Blue).Build());
        }
    }
}
