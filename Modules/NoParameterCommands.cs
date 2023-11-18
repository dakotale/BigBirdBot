using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System.Data;
using DiscordBot.Helper;
using Discord.Interactions;
using System.Runtime.CompilerServices;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    public class NoParameterCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();

        [Command("info")]
        [Alias("serverinfo", "server")]
        [Discord.Commands.RequireBotPermission(GuildPermission.EmbedLinks)]
        public async Task HandleServerInformation()
        {
            audit.InsertAudit("serverinfo", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());

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
                    $"**Voice Channels:** {Context.Guild.VoiceChannels.Count }\n" +
                    $"**Roles:** {Context.Guild.Roles.Count}\n" +
                    $"**Emotes:** {Context.Guild.Emotes.Count}\n" +
                    $"**Stickers:** {Context.Guild.Stickers.Count}\n\n" +
                    $"**Security level:** {Context.Guild.VerificationLevel}")
                 .WithImageUrl(bannerUrl)
                 .WithCurrentTimestamp();

            await ReplyAsync(embed: embed.Build());
        }

        [Command("populateallusers")]
        public async Task HandlePopulateAllUserCommand()
        {
            try
            {
                audit.InsertAudit("populateallusers", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());

                using (SqlConnection conn = new SqlConnection(Constants.Constants.discordBotConnStr))
                {
                    await Context.Guild.DownloadUsersAsync();
                    foreach (var user in Context.Guild.Users.ToList())
                    {
                        if (!user.IsBot)
                        {
                            conn.Open();

                            // 1.  create a command object identifying the stored procedure
                            SqlCommand cmd = new SqlCommand("AddUser", conn);

                            // 2. set the command object so it knows to execute a stored procedure
                            cmd.CommandType = CommandType.StoredProcedure;

                            // 3. add parameter to command, which will be passed to the stored procedure
                            cmd.Parameters.Add(new SqlParameter("@UserID", user.Id.ToString()));
                            cmd.Parameters.Add(new SqlParameter("@Username", user.Username));
                            cmd.Parameters.Add(new SqlParameter("@JoinDate", user.JoinedAt));
                            cmd.Parameters.Add(new SqlParameter("@GuildName", user.Guild.Name));
                            cmd.Parameters.Add(new SqlParameter("@Nickname", user.Nickname));
                            // execute the command
                            cmd.ExecuteNonQuery();

                            conn.Close();
                            cmd.Dispose();
                        }
                    }
                }
                await ReplyAsync("All users of the server were added to the database.");
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("kaonoff")]
        public async Task HandleKeywordOnOff()
        {
            audit.InsertAudit("kaonoff", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
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
            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
        }

        [Command("raffle")]
        public async Task HandleRaffle()
        {
            audit.InsertAudit("raffle", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
            var userList = Context.Guild.GetUsersAsync().ToListAsync().Result;
            foreach (var user in userList)
            {
                var finalList = user.Where(s => !s.IsBot).ToList();
                Random r = new Random();
                var winningUser = finalList[r.Next(0, finalList.Count)];
                
                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Raffle", $"Congratulations {winningUser.Mention}, you won the raffle!", "", Context.Message.Author.Username, Discord.Color.Green).Build());
            }
        }

        [Command("brendancounter")]
        [RequireRole("actual degens")]
        public async Task HandleLowLevel()
        {
            // AddLowLevel
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(connStr, "AddLowLevel", new List<SqlParameter>
            { new SqlParameter("@CreatedBy", Context.User.Username)});

            string counterHistory = "Here are the most recent times low level content was stated.\n";
            string currentCounter = "";
            string currentDateTime = "";

            foreach (DataRow dr in dt.Rows)
            {
                currentCounter = dr["CurrentCounter"].ToString();
                currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET");
                counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET")}\n";
            }

            EmbedHelper embed = new EmbedHelper();
            await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Low Level", $"**The low level content counter was updated to {currentCounter} on {currentDateTime}**\n{counterHistory}", "", Context.Message.Author.Username, Discord.Color.Green).Build());
        }

        [Command("twitter")]
        public async Task HandleTwitterEmbeds()
        {
            audit.InsertAudit("twitter", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
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
            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
        }

        [Command("kalist")]
        [Alias("kal")]
        public async Task HandleKeywordList()
        {
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
                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - List of Active Keywords", output, "", Context.Message.Author.Username, Discord.Color.Green).Build());
            }
        }

        [Command("log")]
        public async Task HandleLog()
        {
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            string output = "";

            DataTable dt = stored.Select(connStr, "GetLog", new List<SqlParameter>());
            EmbedHelper embed = new EmbedHelper();

            if (dt.Rows.Count > 0)
            {
                foreach(DataRow dr in dt.Rows)
                {
                    output += $"__Most Recent Error Message Reported__\nDate Logged: {dr["CreatedOn"].ToString()}\nSource: {dr["Source"].ToString()}\nSeverity: {dr["Severity"].ToString()}\nMessage: {dr["Message"].ToString()}\nException: {dr["Exception"].ToString()}";
                }
                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error Log", output, "", Context.Message.Author.Username, Discord.Color.Red).Build());
            }
            else
            {
                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error Log", "No recent exceptions found.", "", Context.Message.Author.Username, Discord.Color.Green).Build());
            }
        }
    }
}
