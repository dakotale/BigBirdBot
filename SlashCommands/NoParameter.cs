using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RequireBotPermissionAttribute = Discord.Interactions.RequireBotPermissionAttribute;
using RequireContextAttribute = Discord.Interactions.RequireContextAttribute;
using RequireUserPermissionAttribute = Discord.Interactions.RequireUserPermissionAttribute;

namespace DiscordBot.SlashCommands
{
    public class NoParameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("help", "Gets a list of commands and descriptions available to the bot.")]
        public async Task TaskHelpCommand()
        {
            await DeferAsync();
            StoredProcedure storedProcedure = new StoredProcedure();
            EmbedHelper helper = new EmbedHelper();
            DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "GetCommandList", new List<System.Data.SqlClient.SqlParameter>());
            string output = "";

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    output += $"**{dr["CommandName"].ToString()} ({dr["CommandAliases"]})\nDescription:** {dr["CommandDescription"].ToString()}\n\n";
                }
            }

            await FollowupAsync(embed: helper.BuildMessageEmbed("BigBirdBot - Help Commands", output, "", "BigBirdBot", Discord.Color.Gold, null, null).Build());
        }

        // Ban a user
        [SlashCommand("ban", "Bans a user but the bot and user must have permission.")]
        [RequireContext(Discord.Interactions.ContextType.Guild)]
        // make sure the user invoking the command can ban
        [RequireUserPermission(GuildPermission.BanMembers)]
        // make sure the bot itself can ban
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanUserAsync(IGuildUser user, string reason)
        {
            await DeferAsync();
            await user.Guild.AddBanAsync(user, reason: reason);
            await FollowupAsync($"{user.Username} was banned successfully.");
        }

        [SlashCommand("info", "Shows information of the current server.")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        public async Task HandleServerInformation()
        {
            await DeferAsync();
            double botPercentage = Math.Round(Context.Guild.Users.Count(x => x.IsBot) / Context.Guild.MemberCount * 100d, 2);

            string bannerUrl = Context.Guild.BannerUrl ?? "";

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithTitle($"Server Information for {Context.Guild.Name}")
                .WithDescription(
                    $"**Guild name:** {Context.Guild.Name}\n" +
                    $"**Guild ID:** {Context.Guild.Id}\n" +
                    $"**Created On:** {Context.Guild.CreatedAt:MM/dd/yyyy}\n" +
                    $"**Owner:** {Context.Guild.Owner}\n\n" +
                    $"**Users:** {Context.Guild.MemberCount - Context.Guild.Users.Count(x => x.IsBot)}\n" +
                    $"**Bots:** {Context.Guild.Users.Count(x => x.IsBot)} [ {botPercentage}% ]\n" +
                    $"**Text Channels:** {Context.Guild.TextChannels.Count}\n" +
                    $"**Voice Channels:** {Context.Guild.VoiceChannels.Count}\n" +
                    $"**Roles:** {Context.Guild.Roles.Count}\n" +
                    $"**Emotes:** {Context.Guild.Emotes.Count}\n" +
                    $"**Stickers:** {Context.Guild.Stickers.Count}\n\n" +
                    $"**Security level:** {Context.Guild.VerificationLevel}")
                 .WithImageUrl(bannerUrl)
                 .WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("populateallusers", "Populate users into the DB.")]
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
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("kaonoff", "Disables all keywords for the server.")]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordOnOff()
        {
            await DeferAsync();
            StoredProcedure procedure = new StoredProcedure();
            var serverId = Int64.Parse(Context.Guild.Id.ToString());
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "TurnAllOnOffKeywordsByServer", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", serverId)
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString();
            }

            string title = "BigBirdBot - Keywords Updated";
            string desc = result;
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
        }

        [SlashCommand("raffle", "Picks a random person in the server to win a prize.")]
        public async Task HandleRaffle()
        {
            await DeferAsync();
            var userList = Context.Guild.GetUsersAsync().ToListAsync().Result;
            foreach (var user in userList)
            {
                var finalList = user.Where(s => !s.IsBot).ToList();
                Random r = new Random();
                var winningUser = finalList[r.Next(0, finalList.Count)];

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Raffle", $"Congratulations {winningUser.Mention}, you won the raffle!", "", Context.User.Username, Discord.Color.Green).Build());
            }
        }

        [SlashCommand("twitter", "When Twitter embeds fail, have the bot autocorrect the links.")]
        public async Task HandleTwitterEmbeds()
        {
            await DeferAsync();
            StoredProcedure procedure = new StoredProcedure();
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "UpdateTwitterBroken", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())) });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString();
            }

            string title = "BigBirdBot - Twitter Embeds";
            string desc = result;
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
        }

        [SlashCommand("kalist", "List of all keywords in the server.")]
        public async Task HandleKeywordList()
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            var serverId = Int64.Parse(Context.Guild.Id.ToString());
            int i = 1;

            StoredProcedure stored = new StoredProcedure();
            string output = "";

            DataTable dt = stored.Select(connStr, "GetKeywordsByServerUID", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", serverId)
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    output += $"**{i.ToString()}.** {dr["Keyword"].ToString().Trim()}\n";
                    i++;
                }

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - List of Active Keywords", output, "", Context.User.Username, Discord.Color.Green).Build());
            }
        }

        [SlashCommand("log", "Most recent error message in the bot.")]
        public async Task HandleLog()
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            string output = "";

            DataTable dt = stored.Select(connStr, "GetLog", new List<SqlParameter>());
            EmbedHelper embed = new EmbedHelper();

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    output += $"__Most Recent Error Message Reported__\nDate Logged: {dr["CreatedOn"].ToString()}\nSource: {dr["Source"].ToString()}\nSeverity: {dr["Severity"].ToString()}\nMessage: {dr["Message"].ToString()}\nException: {dr["Exception"].ToString()}";
                }
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error Log", output, "", Context.User.Username, Discord.Color.Red).Build());
            }
            else
            {
                await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error Log", "No recent exceptions found.", "", Context.User.Username, Discord.Color.Blue).Build());
            }
        }

        [SlashCommand("welcomemsg", "Enables/Disables the welcome message for the bot.")]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleWelcomeMessage()
        {
            await DeferAsync();
            StoredProcedure procedure = new StoredProcedure();
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "UpdateShowWelcomeMessage", new List<SqlParameter> { new SqlParameter("@ServerUID", Int64.Parse(Context.Guild.Id.ToString())) });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString();
            }

            string title = "BigBirdBot - Welcome Message Configuration";
            string desc = result;
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build());
        }

        [SlashCommand("connplayers", "List of all connected players in voice channels.")]
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
    }
}
