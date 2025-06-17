using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using System.Data;
using System.Data.SqlClient;
using System.Net;

namespace DiscordBot.SlashCommands
{
    public class Parameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("random", "Randomize a number from the range provided.")]
        [EnabledInDm(true)]
        public async Task GenerateRandomNumber([MinValue(1), MaxValue(int.MaxValue)] int number)
        {
            await DeferAsync();
            Random r = new Random();
            int i = r.Next(1, number + 1);

            string title = "BigBirdBot - Random";
            string desc = $"{Context.User.Mention} rolled a **{i}**";
            string thumbnailUrl = "";
            string createdBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [SlashCommand("etext", "Convert your message into emojis.")]
        [EnabledInDm(true)]
        public async Task HandleEmojiTextCommand([MinLength(1), MaxLength(1000)] string message)
        {
            await DeferAsync();
            EmojiText emoji = new EmojiText();
            await FollowupAsync(emoji.GetEmojiString(message));
        }

        [SlashCommand("poll", "Create a poll for people to vote on.")]
        [EnabledInDm(true)]
        public async Task HandlePoll([MinLength(1), MaxLength(4000)] string statement, [MinLength(1)] string pollAnswer1, [MinLength(1)] string pollAnswer2, string pollAnswer3 = null, string pollAnswer4 = null, string pollAnswer5 = null, string pollAnswer6 = null, string pollAnswer7 = null, string pollAnswer8 = null, string pollAnswer9 = null, string pollAnswer10 = null)
        {
            await DeferAsync();
            List<Emoji> emojis = new List<Emoji>
            {
                new Emoji("1️⃣"),
                new Emoji("2️⃣"),
                new Emoji("3️⃣"),
                new Emoji("4️⃣"),
                new Emoji("5️⃣"),
                new Emoji("6️⃣"),
                new Emoji("7️⃣"),
                new Emoji("8️⃣"),
                new Emoji("9️⃣"),
                new Emoji("🔟")
            };
            List<string> items = new List<string>() { pollAnswer1, pollAnswer2, pollAnswer3, pollAnswer4, pollAnswer5, pollAnswer6, pollAnswer7, pollAnswer8, pollAnswer9, pollAnswer10 };
            items = items.Where(s => !string.IsNullOrEmpty(s)).Select(s => s.Trim()).ToList();
            EmbedHelper embed = new EmbedHelper();
            string title = "BigBirdBot - Poll";
            string desc = $"Poll Item: **{statement.Trim()}**\n\nChoices:";
            string createdByMsg = "Command from: " + Context.User.Username;

            for (int i = 0; i < items.Count; i++)
                desc += "\n" + i.ToString() + ". **" + items[i] + "**";

            IUserMessage msg = await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue).Build());

            for (int i = 0; i < items.Count; i++)
                await msg.AddReactionAsync(emojis[i]);
        }

        [SlashCommand("addbirthday", "Adds a role members birthday to celebrate.")]
        [EnabledInDm(false)]
        public async Task HandleBirthday(SocketGuildUser user, [MinValue(1), MaxValue(12)] int monthNumber, [MinValue(1), MaxValue(31)] int dayNumber)
        {
            await DeferAsync();
            StoredProcedure storedProcedure = new StoredProcedure();
            try
            {
                EmbedHelper embedHelper = new EmbedHelper();
                long serverId = Int64.Parse(Context.Guild.Id.ToString());
                SocketGuild guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));

                if (guild.Roles.Where(s => s.Name.Contains("birthday")).Count() == 0)
                {
                    // Create the birthday role and add all the users in the server
                    await guild.CreateRoleAsync("birthday", null, Discord.Color.Purple, false, true, null);

                    EmbedBuilder embed = new EmbedBuilder
                    {
                        Title = "BigBirdBot - Birthday",
                        Color = Color.Gold,
                        Description = "A **birthday** role was created, please have an administrator add the users to this role before running this command again."
                    };

                    await FollowupAsync(embed: embed.Build());

                    return;
                }

                DateTime birthday = DateTime.Parse(monthNumber.ToString() + "/" + dayNumber.ToString() + "/" + DateTime.Now.Year.ToString());

                storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBirthday", new List<SqlParameter>
                {
                    new SqlParameter("@BirthdayDate", birthday),
                    new SqlParameter("@BirthdayUser", user.Mention),
                    new SqlParameter("@BirthdayGuild", Context.Guild.Id.ToString())
                });

                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Birthday Added", $"{user.DisplayName} birthday was added to the bot.", "", Context.User.Username, Discord.Color.Blue).Build());
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Birthday", e.Message, Context.User.Username).Build());
            }
        }

        [SlashCommand("avatar", "See you or someone else's avatar in high quality.")]
        [EnabledInDm(true)]
        public async Task HandleAvatarCommand(SocketGuildUser user = null)
        {
            await DeferAsync();
            try
            {
                if (user == null)
                    user = Context.User as SocketGuildUser;

                EmbedBuilder embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot - {user.Username}'s Avatar",
                    Color = Color.Blue,
                    ImageUrl = user.GetDisplayAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl()
                };
                embed
                    .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                    .WithCurrentTimestamp();
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("", e.Message, Context.User.Username).Build());
            }
        }

        [SlashCommand("reportbug", "Found an issue with the bot?  Report it here, please.")]
        [EnabledInDm(true)]
        public async Task HandleBugReport([MinLength(1), MaxLength(4000)] string bugFound)
        {
            ulong guildId = ulong.Parse("880569055856185354");
            ulong textChannelId = ulong.Parse("1156625507840954369");
            await Context.Client.GetGuild(guildId).GetTextChannel(textChannelId).SendMessageAsync($"**Bug Report from {Context.User.Username} in {Context.Guild.Name}**: \n" + bugFound);
            await ReplyAsync("Bug report submitted.");
        }

        [SlashCommand("polldnd", "Poll feature specifically for D&D weekly scheduling")]
        [EnabledInDm(false)]
        public async Task HandlePollDND(SocketGuildUser user)
        {
            await DeferAsync();
            List<Emoji> emojis = new List<Emoji>
            {
                new Emoji("1️⃣"),
                new Emoji("2️⃣"),
                new Emoji("3️⃣"),
                new Emoji("4️⃣"),
                new Emoji("5️⃣"),
                new Emoji("6️⃣"),
                new Emoji("7️⃣"),
            };

            EmbedHelper embed = new EmbedHelper();
            string title = "BigBirdBot - Poll";
            string desc = $"Poll Item: **Best day for {user.Mention}/{user.DisplayName}'s campaign?**\n\nChoices:";
            string createdByMsg = "Command from: " + Context.User.Username;

            List<string> items = new List<string>();
            for (int i = 1; i < 8; i++)
            {
                DateTime dateTime = DateTime.Now.AddDays(i);
                string dayOfWeek = dateTime.DayOfWeek.ToString();

                string item = dayOfWeek + " (" + dateTime.ToString("MM/dd") + ")";
                items.Add(item);
            }

            for (int i = 0; i < items.Count; i++)
                desc += "\n" + i.ToString() + ". **" + items[i] + "**";

            IUserMessage msg = await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue).Build());

            for (int i = 0; i < items.Count; i++)
                await msg.AddReactionAsync(emojis[i]);
        }

        [SlashCommand("setrolecolor", "Set the color of your role by hex code, include the #")]
        [EnabledInDm(false)]
        public async Task HandleColor([MinLength(1), MaxLength(10)] string hexCode, SocketGuildUser userName = null)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();

            if (hexCode.StartsWith("#"))
                hexCode = hexCode.Substring(1);

            try
            {
                System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml(hexCode);
                long serverId = Int64.Parse(Context.Guild.Id.ToString());
                SocketGuild guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
                SocketUser user = userName ?? Context.User;

                if (color != System.Drawing.Color.Empty)
                {
                    Color roleColor = new Color(color.R, color.G, color.B);

                    if (guild.Roles.Any(s => s.Name.Equals(user)))
                    {
                        SocketRole role = guild.Roles.First(s => s.Name.Equals(user));
                        await role.ModifyAsync(f => f.Color = roleColor).ConfigureAwait(false);
                    }
                    else
                    {
                        SocketRole botRole = guild.Roles.First(s => s.Name.Equals("BigBirdBot"));
                        int botPos = botRole.Position;

                        Discord.Rest.RestRole role = await guild.CreateRoleAsync(Context.User.Username, null, roleColor, false, true);

                        // Should be 1 under the bot to prevent a missing permissions error
                        await role.ModifyAsync(f => f.Position = botPos - 1).ConfigureAwait(false);
                        await (user as IGuildUser).AddRoleAsync(role).ConfigureAwait(false);
                    }

                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Role Color", $"Color was updated successfully", "", Context.User.Username, Discord.Color.Blue).Build());
                }
                else
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Color", "The hex code entered was not valid.\nExample: #607c8c", Context.User.Username).Build());
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Color", ex.Message, Context.User.Username).Build());
            }
        }

        [SlashCommand("detectaibyattachment", "Upload an attachment through the bot to get the percentage chance it's AI.")]
        [EnabledInDm(true)]
        public async Task HandleAIByAttachment(Attachment attachment)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();

            try
            {
                string attachmentName = attachment.Filename;
                string withoutExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[0];
                string withExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[1];
                withoutExt = withoutExt + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfffff");

                string path = Constants.Constants.aiDetectorPath + withoutExt + "." + withExt;

                if (!attachment.ContentType.Contains("image"))
                {
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**The file provided was not an image, please upload an image and try again.**", Context.User.Username).Build());
                    return;
                }

                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFileTaskAsync(new Uri(attachment.Url), path).Wait();
                }

                HttpClient client = new HttpClient();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://api.sightengine.com/1.0/check.json");

                MultipartFormDataContent content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(File.ReadAllBytes(path)), "media", Path.GetFileName(path));
                content.Add(new StringContent("genai"), "models");
                content.Add(new StringContent(Constants.Constants.aiApiUserId), "api_user");
                content.Add(new StringContent(Constants.Constants.aiApiSecretId), "api_secret");
                request.Content = content;

                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrEmpty(responseBody))
                {
                    StoredProcedure stored = new StoredProcedure();
                    DataTable dt = new DataTable();

                    dt = stored.Select(Constants.Constants.discordBotConnStr, "GetAIJSONImageReturn", new List<SqlParameter> { new SqlParameter("@json", responseBody) });

                    if (dt.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            if (dr["Status"].ToString().Equals("success"))
                            {
                                var detectionRate = double.Parse(dr["PercentageChance"].ToString());
                                string description = "";

                                if (detectionRate <= 25.0)
                                    description = $"**There is a small chance ({detectionRate.ToString() + "%"}) this image is AI and would be safe to assume it is not AI.**";
                                if (detectionRate > 25.0 &&  detectionRate <= 50.0)
                                    description = $"**There is a chance ({detectionRate.ToString() + "%"}) this image is AI and should be investigated further.**";
                                if (detectionRate > 50.0 && detectionRate <= 75.0)
                                    description = $"**There is a high chance ({detectionRate.ToString() + "%"}) this image is AI and should be investigated further.**";
                                if (detectionRate > 75.0)
                                    description = $"**This image was created with AI based on the percentage matching of {detectionRate.ToString() + "%"}.**";

                                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - AI Detection", description, "", Context.User.Username, Discord.Color.Blue).Build());
                            }
                            else
                            {
                                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**The request failed when sending to the detection endpoint.**", Context.User.Username).Build());
                            }
                        }
                    }
                    else
                    {
                        await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**There was no response returned from the detection endpoint.**", Context.User.Username).Build());
                    }
                }
                else
                {
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**There was no response returned from the detection endpoint.**", Context.User.Username).Build());
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", ex.Message, Context.User.Username).Build());
            }

            
        }

        [SlashCommand("detectaibyurl", "Provide a URL through the bot to get the percentage chance the image URL is AI.")]
        [EnabledInDm(true)]
        public async Task HandleAIByURL(string url)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();

            try
            {
                HttpClient client = new HttpClient();

                if (!url.Contains("https") || url.Contains("discordapp.net"))
                {
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**The request does not have a proper URL and failed when sending to the detection endpoint.**", Context.User.Username).Build());
                    return;
                }

                string responseBody = await client.GetStringAsync($"https://api.sightengine.com/1.0/check.json?models=genai&api_user={Constants.Constants.aiApiUserId}&api_secret={Constants.Constants.aiApiSecretId}&url={url}");

                if (!string.IsNullOrEmpty(responseBody))
                {
                    StoredProcedure stored = new StoredProcedure();
                    DataTable dt = new DataTable();

                    dt = stored.Select(Constants.Constants.discordBotConnStr, "GetAIJSONImageReturn", new List<SqlParameter> { new SqlParameter("@json", responseBody) });

                    if (dt.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            if (dr["Status"].ToString().Equals("success"))
                            {
                                var detectionRate = double.Parse(dr["PercentageChance"].ToString());
                                string description = "";

                                if (detectionRate <= 25.0)
                                    description = $"**There is a small chance ({detectionRate.ToString() + "%"}) this image is AI and would be safe to assume it is not AI.**";
                                if (detectionRate > 25.0 && detectionRate <= 50.0)
                                    description = $"**There is a chance ({detectionRate.ToString() + "%"}) this image is AI and should be investigated further.**";
                                if (detectionRate > 50.0 && detectionRate <= 75.0)
                                    description = $"**There is a high chance ({detectionRate.ToString() + "%"}) this image is AI and should be investigated further.**";
                                if (detectionRate > 75.0)
                                    description = $"**This image was created with AI based on the percentage matching of {detectionRate.ToString() + "%"}.**";

                                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - AI Detection", description, "", Context.User.Username, Discord.Color.Blue).Build());
                            }
                            else
                            {
                                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**The request failed when sending to the detection endpoint.**", Context.User.Username).Build());
                            }
                        }
                    }
                    else
                    {
                        await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**There was no response returned from the detection endpoint.**", Context.User.Username).Build());
                    }
                }
                else
                {
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", "**There was no response returned from the detection endpoint.**", Context.User.Username).Build());
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("AI Detection Error", ex.Message, Context.User.Username).Build());
            }


        }
    }
}
