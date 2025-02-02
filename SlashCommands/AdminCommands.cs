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

            foreach (DataRow dr in dt.Rows)
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
                await FollowupAsync(embed: embed.BuildErrorEmbed("Roles", "There are no roles available to select.", Context.User.Username).Build());
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
                await FollowupAsync(embed: embed.BuildErrorEmbed("Roles", "The limit of 25 roles have been added, please delete a role before adding one.", Context.User.Username).Build());
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
    }
}
