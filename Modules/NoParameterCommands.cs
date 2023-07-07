using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System.Data;
using DiscordBot.Anime;
using DiscordBot.Helper;
using Discord.Interactions;

namespace DiscordBot.Modules
{
    public class NoParameterCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();

        [Command("avatar")]
        [Discord.Commands.Summary("See you or someone else's avatar in high quality.")]
        public async Task HandleAvatarCommand(IUser user = null)
        {
            try
            {
                audit.InsertAudit("avatar", Context.User.Username, Constants.Constants.discordBotConnStr);

                string title = "BigBirdBot - Avatar";
                string desc = $"";
                string thumbnailUrl = "";
                string createdBy = "Command from: " + Context.User.Username;
                string imageUrl = "";

                if (user != null && !user.IsBot && user.Mention.Length > 0)
                {
                    imageUrl = user.GetAvatarUrl(ImageFormat.Png, 256);
                }
                else
                {
                    imageUrl = Context.Message.Author.GetAvatarUrl(ImageFormat.Png, 256);
                }


                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("populateallusers")]
        public async Task HandlePopulateAllUserCommand()
        {
            try
            {
                audit.InsertAudit("populateallusers", Context.User.Username, Constants.Constants.discordBotConnStr);

                using (SqlConnection conn = new SqlConnection(Constants.Constants.discordBotConnStr))
                {
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

        [Command("amm")]
        [Discord.Commands.Summary("Anime/Manga characters to marry.  This is a data dump and may not be accurate.")]
        public async Task HandleAmmCommand()
        {
            audit.InsertAudit("amm", Context.User.Username, Constants.Constants.discordBotConnStr);

            string connStr = Constants.Constants.discordBotConnStr;

            Random r = new Random();
            Anime.MarriageCharacter marriage = new Anime.MarriageCharacter();

            List<Anime.MarriageCharacter> marriageCharacters = marriage.GetOneMarriageCharacters(connStr);

            foreach (var m in marriageCharacters)
            {
                var embed = new EmbedBuilder
                {
                    Title = "BigBirdBot - Marriage - " + m.CharacterName,
                    Description = "Currency Given: " + m.CurrencyValue + " :diamond_shape_with_a_dot_inside: ",
                    ImageUrl = m.CharacterURL,
                    Color = Color.Gold
                };
                var message = await ReplyAsync(embed: embed.Build());
                Emoji emoji = new Emoji("\uD83E\uDD0D");
                await message.AddReactionAsync(emoji);
                emoji = new Emoji("\u2754");
                await message.AddReactionAsync(emoji);
                emoji = new Emoji("❌");
                await message.AddReactionAsync(emoji);
            }
        }

        [Command("ammlist")]
        [Discord.Commands.Summary("List all your marriages into a pretty HTML file.")]
        public async Task HandleAmmListCommand()
        {
            try
            {
                audit.InsertAudit("ammlist", Context.User.Username, Constants.Constants.discordBotConnStr);

                string connStr = Constants.Constants.discordBotConnStr;
                string currentUser = Context.User.Username;
                string html = "";
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1.  create a command object identifying the stored procedure
                    SqlCommand cmd = new SqlCommand("GetMarriageList", conn);

                    // 2. set the command object so it knows to execute a stored procedure
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 3. add parameter to command, which will be passed to the stored procedure
                    cmd.Parameters.Add(new SqlParameter("@CreatedBy", currentUser));

                    // execute the command
                    SqlDataReader rdr = cmd.ExecuteReader();

                    if (rdr.Read())
                    {
                        html = rdr["HTML"].ToString();
                        html = html.Replace("td", "td class=text-center");
                    }

                    conn.Close();
                    cmd.Dispose();
                }

                await File.WriteAllTextAsync(@"C:\Users\Administrator\Desktop\HTML\" + currentUser + "Marriages.html", html);

                Marriage marriage = new Marriage();
                List<Anime.Marriage> marriages = marriage.GetMarriages(connStr);
                await ReplyAsync(":bell:**" + currentUser + "'s Marriages (" + marriages.Where(s => s.CreatedBy.Equals(currentUser)).ToList().Count.ToString() + " total) - Download the file below** :bell:");
                await Context.Channel.SendFileAsync(@"C:\Users\DZL\Desktop\HTML\" + currentUser + "Marriages.html");
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("errmusiclog")]
        [Alias("eml")]
        public async Task HandleMusicLog()
        {
            try
            {
                string connStr = Constants.Constants.discordBotConnStr;

                audit.InsertAudit("errmusiclog", Context.User.Username, Constants.Constants.discordBotConnStr);

                MusicLog musicLog = new MusicLog();
                List<MusicLog> musicLogs = musicLog.GetMusicLog(connStr);
                
                if (musicLogs.Count == 0)
                {
                    await ReplyAsync("No errors reported.");
                }
                else
                {
                    foreach (var m in musicLogs)
                    {
                        string error = "Code: " + m.Code.ToString() + "\nReason: " + m.Reason + "\nServer Name: " + m.ServerName + "\nDate Logged: " + m.CreatedOn.ToString();
                        var embed = new EmbedBuilder
                        {
                            Title = $"BigBirdBot Music - Latest Error",
                            Color = Color.Red,
                            Description = error,
                            //ThumbnailUrl = "https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png",
                        };

                        embed.WithCurrentTimestamp();
                        await ReplyAsync(embed: embed.Build());
                    }
                }
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
            audit.InsertAudit("kaonoff", Context.User.Username, Constants.Constants.discordBotConnStr);
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

        [Command("snake")]
        public async Task HandleSnake()
        {
            audit.InsertAudit("snake", Context.User.Username, Constants.Constants.discordBotConnStr);
            StoredProcedure procedure = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            CommandHelper helper = new CommandHelper();
            var image = helper.GetImage("snake").Result;

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetSnake", new List<SqlParameter>());
            string snakeFact = "";

            if (dt.Rows.Count > 0 && image.Data.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    snakeFact = row["SnakeFact"].ToString();

                    string title = "BigBirdBot - Snake";
                    string desc = $"{snakeFact}";
                    string thumbnailUrl = "";
                    string imageUrl = image.Data[0].Url;
                    string createdBy = "Command from: " + Context.User.Username;

                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green, imageUrl).Build());
                }
            }
        }

        [Command("turtle")]
        public async Task HandleTurtle()
        {
            audit.InsertAudit("turtle", Context.User.Username, Constants.Constants.discordBotConnStr);
            StoredProcedure procedure = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            CommandHelper helper = new CommandHelper();
            var image = helper.GetImage("turtle").Result;

            if (image.Data.Count > 0)
            {
                string title = "BigBirdBot - Turtle";
                string desc = $"";
                string thumbnailUrl = "";
                string imageUrl = image.Data[0].Url;
                string createdBy = "Command from: " + Context.User.Username;

                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green, imageUrl).Build());
            }
        }

        [Command("lizard")]
        public async Task HandleLizard()
        {
            audit.InsertAudit("lizard", Context.User.Username, Constants.Constants.discordBotConnStr);
            StoredProcedure procedure = new StoredProcedure();
            EmbedHelper embed = new EmbedHelper();
            CommandHelper helper = new CommandHelper();
            var image = helper.GetImage("lizard").Result;

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetLizard", new List<SqlParameter>());
            string lizardFact = "";

            if (dt.Rows.Count > 0 && image.Data.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    lizardFact = row["LizardFact"].ToString();

                    string title = "BigBirdBot - Lizard";
                    string desc = $"{lizardFact}";
                    string thumbnailUrl = "";
                    string imageUrl = image.Data[0].Url;
                    string createdBy = "Command from: " + Context.User.Username;

                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green, imageUrl).Build());
                }
            }
        }

        [Command("raffle")]
        public async Task HandleRaffle()
        {
            audit.InsertAudit("raffle", Context.User.Username, Constants.Constants.discordBotConnStr);
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
            audit.InsertAudit("twitter", Context.User.Username, Constants.Constants.discordBotConnStr);
            StoredProcedure procedure = new StoredProcedure();
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "UpdateTwitterBroken", new List<SqlParameter>());

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
    }
}
