using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Data;
using System.Data.SqlClient;

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

            string title = "Random";
            string desc = $"{Context.User.Mention} rolled a **{i}**";
            string thumbnailUrl = Context.User.GetAvatarUrl();
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
        public async Task HandlePoll([MinLength(1), MaxLength(2000)] string statement, [MinLength(1)] string pollAnswer1, [MinLength(1)] string pollAnswer2, string pollAnswer3 = null, string pollAnswer4 = null, string pollAnswer5 = null, string pollAnswer6 = null, string pollAnswer7 = null, string pollAnswer8 = null, string pollAnswer9 = null, string pollAnswer10 = null, Attachment attachment = null)
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

            string imageUrl = "";

            if (attachment != null)
                imageUrl = attachment.Url;

            List<string> items = new List<string>() { pollAnswer1, pollAnswer2, pollAnswer3, pollAnswer4, pollAnswer5, pollAnswer6, pollAnswer7, pollAnswer8, pollAnswer9, pollAnswer10 };
            items = items.Where(s => !string.IsNullOrEmpty(s)).Select(s => s.Trim()).ToList();
            EmbedHelper embed = new EmbedHelper();
            string title = "Poll";
            string desc = $"Poll Item: **{statement.Trim()}**\n\nChoices:";
            string createdByMsg = "Command from: " + Context.User.Username;

            for (int i = 0; i < items.Count; i++)
                desc += "\n" + i.ToString() + ". **" + items[i] + "**";

            IUserMessage msg = await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue, imageUrl).Build());

            for (int i = 0; i < items.Count; i++)
                await msg.AddReactionAsync(emojis[i]);
        }

        [SlashCommand("addbirthday", "Adds a role members birthday to celebrate.")]
        [EnabledInDm(false)]
        public async Task HandleBirthday(SocketGuildUser user, [MinValue(1), MaxValue(12)] int monthNumber, [MinValue(1), MaxValue(31)] int dayNumber)
        {
            await DeferAsync(ephemeral: true);
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
                        Title = "Birthday",
                        Color = Color.Gold,
                        Description = "A **birthday** role was created, please have an administrator add the users to this role before running this command again."
                    };

                    await FollowupAsync(embed: embed.Build(), ephemeral: true);

                    return;
                }

                DateTime birthday = DateTime.Parse(monthNumber.ToString() + "/" + dayNumber.ToString() + "/" + DateTime.Now.Year.ToString());

                storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBirthday", new List<SqlParameter>
                {
                    new SqlParameter("@BirthdayDate", birthday),
                    new SqlParameter("@BirthdayUser", user.Mention),
                    new SqlParameter("@BirthdayGuild", Context.Guild.Id.ToString())
                });

                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Birthday Added", $"{user.DisplayName} birthday was added to the bot.", "", Context.User.Username, Discord.Color.Blue).Build());
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
                    Title = $"{user.Username}'s Avatar",
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
        public async Task HandleBugReport([MinLength(1), MaxLength(2000)] string bugFound)
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
            string title = "Poll";
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
            await DeferAsync(ephemeral: true);
            EmbedHelper embedHelper = new EmbedHelper();

            if (hexCode.StartsWith("#"))
                hexCode = hexCode.Substring(1);
            else
                hexCode = "#" + hexCode;

            try
            {
                System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml(hexCode);
                long serverId = Int64.Parse(Context.Guild.Id.ToString());
                SocketGuild guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
                SocketUser user = userName ?? Context.User;

                if (color != System.Drawing.Color.Empty)
                {
                    Color roleColor = new Color(color.R, color.G, color.B);

                    if (guild.Roles.Any(s => s.Name.Equals(user.Username)))
                    {
                        SocketRole role = guild.Roles.First(s => s.Name.Equals(user.Username));
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

                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Role Color", $"Color was updated successfully", "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
                }
                else
                    await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Color", "The hex code entered was not valid.\nExample: #607c8c", Context.User.Username).Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("Color", "The color entered was not valid: " +  ex.Message, Context.User.Username).Build(), ephemeral: true);
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

                using (HttpClient httpClient = new HttpClient())
                {
                    var bytes = await httpClient.GetByteArrayAsync(new Uri(attachment.Url));
                    await File.WriteAllBytesAsync(path, bytes);
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

                                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("AI Detection", description, "", Context.User.Username, Discord.Color.Blue, attachment.Url).Build());
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

        [SlashCommand("chat", "Have a wonderful conversation with the bot.")]
        [EnabledInDm(true)]
        public async Task HandleChat(string message, [Choice("Yes", "Yes"), Choice("No", "No")] string startNew,
                                    [Choice ("eSports Gamer Lesbian", "eSports Gamer Lesbian"), 
                                    Choice("Sett", "Sett"),
                                    Choice("T. M. Opera O", "T. M. Opera O"),
                                    Choice("Meisho Doto", "Meisho Doto")] string personality)
        {
            await DeferAsync();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = new DataTable();
            EmbedHelper embed = new EmbedHelper();
            List<ChatMessage> chatMessages = new List<ChatMessage>();
            string botPersona = "";

            switch (personality)
            {
                case "eSports Gamer Lesbian":
                    botPersona = "You are a giga lesbian e-sports gamer who plays League of Legends, Valorant, Counter Strike, you play those and everything else.  You are the best and everyone else is trash.  Don't be afraid to trash talk but do NOT provide any slurs.";
                    break;
                case "Sett":
                    botPersona = "You are Sett from League of Legends.  You will only be allowed to discuss in their mannerisms, but you are very positive and helpful, loving even.";
                    break;
                case "T. M. Opera O":
                    botPersona = "You are T. M. Opera O from Umamusume: Pretty Derby.  You will only be allowed to discuss in their mannerisms, but you are very positive and helpful, loving even.";
                    break;
                case "Meisho Doto":
                    botPersona = "You are Meisho Doto from Umamusume: Pretty Derby.  You will only be allowed to discuss in their mannerisms, but you are very positive and helpful, loving even.";
                    break;
            }
            string response = string.Empty;
            bool isNew = (startNew.Equals("Yes") ? true : false);
            message = message.Trim();

            string userId = Context.User.Id.ToString();
            string userName = Context.User.Username;
            string serverUid = (Context.Guild != null ? Context.Guild.Id.ToString() : "");
            string channelId = Context.Channel.Id.ToString();
            string connStr = Constants.Constants.discordBotConnStr;
            string openAiKey = Constants.Constants.openAiToken;
            string openAiModel = Constants.Constants.openAiModel;

            try
            {
                // 0. If IsNew is true, delete existing messages
                if (isNew)
                {
                    stored.UpdateCreate(connStr, "DeleteBotAIMessage", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", userId),
                        new SqlParameter("@ServerUID", serverUid),
                        new SqlParameter("@ChannelID", channelId)
                    });
                }
                else
                {
                    // 0. Pull existing messages (if available) based on UserID and ServerUID
                    dt = stored.Select(connStr, "GetBotAIMessage", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", userId),
                        new SqlParameter("@ServerUID", serverUid),
                        new SqlParameter("@ChannelID", channelId)
                    });
                }

                // 1. add the following code to connect and authenticate to the AI model.
                // Create the IChatClient
                IChatClient chatClient = new OpenAIClient(openAiKey).GetChatClient(openAiModel).AsIChatClient();

                // 2. Create a system prompt to provide the AI model with initial role context and instructions about hiking recommendations:
                // Start the conversation with context for the AI model
                // See how we can tailor the AI to be silly
                ChatMessage botPersonality = new ChatMessage(ChatRole.System, botPersona);
                chatMessages.Add(botPersonality);

                // 3. Create a conversational loop that accepts an input prompt from the user, sends the prompt to the model
                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (dr["ChatRole"].Equals(ChatRole.Assistant.ToString()))
                            chatMessages.Add(new ChatMessage(ChatRole.Assistant, dr["ChatMessage"].ToString()));
                        if (dr["ChatRole"].Equals(ChatRole.Tool.ToString()))
                            chatMessages.Add(new ChatMessage(ChatRole.Tool, dr["ChatMessage"].ToString()));
                        if (dr["ChatRole"].Equals(ChatRole.System.ToString()))
                            chatMessages.Add(new ChatMessage(ChatRole.System, dr["ChatMessage"].ToString()));
                        if (dr["ChatRole"].Equals(ChatRole.User.ToString()))
                            chatMessages.Add(new ChatMessage(ChatRole.User, dr["ChatMessage"].ToString()));
                    }
                }

                chatMessages.Add(new ChatMessage(ChatRole.User, message));
                response = "**Message:** " + message + "\n\n" + "**Bot Response: **";

                await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatMessages)) { response += item.Text; }
                response = (response.Length > 2000 ? response.Substring(0, 2000) : response);

                stored.UpdateCreate(connStr, "AddBotAIMessage", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId),
                    new SqlParameter("@ChatRole", ChatRole.User.ToString()),
                    new SqlParameter("@ChatMessage", message)
                });

                stored.UpdateCreate(connStr, "AddBotAIMessage", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId),
                    new SqlParameter("@ChatRole", ChatRole.Assistant.ToString()),
                    new SqlParameter("@ChatMessage", response)
                });

                await FollowupAsync(response);
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embed.BuildErrorEmbed("Chat", ex.Message, userName).Build());
            }
        }
    }
}
