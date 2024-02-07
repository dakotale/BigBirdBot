using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Figgle;
using System.Data;
using System.Data.SqlClient;
using System.Net;

namespace DiscordBot.SlashCommands
{
    public class Parameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("random", "Randomize a number from the range provided.")]
        public async Task GenerateRandomNumber([MinValue(1), MaxValue(int.MaxValue)]int number)
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
        public async Task HandleEmojiTextCommand([MinLength(1)] string message)
        {
            await DeferAsync();
            EmojiText emoji = new EmojiText();
            await FollowupAsync(emoji.GetEmojiString(message));
        }

        [SlashCommand("8ball", "Shake the figurative eight ball.")]
        public async Task HandleEightBallCommand([MinLength(1)] string message)
        {
            await DeferAsync();
            Random r = new Random();
            EightBall eight = new EightBall();
            List<EightBall> list = new List<EightBall>();
            list = eight.GetEightBall(Constants.Constants.discordBotConnStr);
            int i = r.Next(1, list.Count + 1);

            string title = "BigBirdBot - 8ball";
            string desc = $"{list.Where(s => s.ID == i).Select(s => s.Saying).FirstOrDefault()}";
            string thumbnailUrl = "https://www.nicepng.com/png/detail/568-5689463_magic-8-ball-eight-ball-billiard-balls-billiards.png";
            string createdBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [SlashCommand("ka", "Adds a keyword to the server.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleKeywordAdd([MinLength(1)] string keyword, string action = null, Attachment attachment = null)
        {
            await DeferAsync();
            try
            {
                if (keyword.Trim().Length > 0 && (!string.IsNullOrEmpty(action) || attachment != null))
                {
                    if (attachment != null)
                    {
                        if (attachment.Size > 52428800)
                        {
                            throw new Exception("The attachment provided is too large, please create the keyword using an attachment that is 50mb or less.");
                        }
                        else
                        {
                            string path = @"C:\Users\Unmolded\Desktop\DiscordBot\KeywordAttachment\" + attachment.Filename;
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFileAsync(new Uri(attachment.Url), path);
                            }

                            string word = keyword.Trim();
                            string createdBy = Context.User.Username;
                            var serverId = Int64.Parse(Context.Guild.Id.ToString());

                            StoredProcedure procedure = new StoredProcedure();

                            procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddChatKeywordAction", new List<System.Data.SqlClient.SqlParameter>
                            {
                                new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                                new System.Data.SqlClient.SqlParameter("@Keyword", word),
                                new System.Data.SqlClient.SqlParameter("@CreatedBy", createdBy),
                                new System.Data.SqlClient.SqlParameter("@Action", path)
                            });

                            string title = "BigBirdBot - Keyword Added";
                            string desc = $"Successfully added Keyword -> **{word}** \nAction -> Attachment added";
                            string thumbnailUrl = "";
                            string imageUrl = "";
                            string embedCreatedBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                        }
                    }
                    else
                    {
                        if (action.Trim().Length > 0)
                        {
                            if (keyword.Length <= 50 && action.Length <= 2000)
                            {
                                string word = keyword.Trim();
                                action = action.Trim();
                                string createdBy = Context.User.Username;
                                var serverId = Int64.Parse(Context.Guild.Id.ToString());

                                StoredProcedure procedure = new StoredProcedure();

                                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddChatKeywordAction", new List<System.Data.SqlClient.SqlParameter>
                                {
                                    new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                                    new System.Data.SqlClient.SqlParameter("@Keyword", word),
                                    new System.Data.SqlClient.SqlParameter("@CreatedBy", createdBy),
                                    new System.Data.SqlClient.SqlParameter("@Action", action)
                                });

                                string title = "BigBirdBot - Keyword Added";
                                string desc = $"Successfully added Keyword -> **{word}** \nAction -> {action}";
                                string thumbnailUrl = "";
                                string imageUrl = "";
                                string embedCreatedBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                            }
                            else
                            {
                                string title = "BigBirdBot - Error";
                                string desc = $"Maximum number of characters for the action is 50 characters and the action is 2000 characters.";
                                string thumbnailUrl = Constants.Constants.errorImageUrl;
                                string imageUrl = "";
                                string createdBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"To add a chat keyword action, enter a word and action/attachment.";
                            string thumbnailUrl = Constants.Constants.errorImageUrl;
                            string imageUrl = "";
                            string createdBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                        }
                    }
                }
                else
                {
                    string title = "BigBirdBot - Error";
                    string desc = $"To add a chat keyword action, enter a word and action.";
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdBy = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                }
            }
            catch (Exception e)
            {
                string title = "BigBirdBot - Error";
                string desc = $"Keyword added resulted in an error.\n" + e.Message;
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [SlashCommand("kaedit", "Edits a keyword to the server.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleKeywordUpdate([MinLength(1)] string keyword, string action = null, Attachment attachment = null)
        {
            await DeferAsync();
            try
            {
                if (keyword.Trim().Length > 0 && (!string.IsNullOrEmpty(action) || attachment != null))
                {
                    if (attachment != null)
                    {
                        string word = keyword.Trim();
                        string createdBy = Context.User.Username;
                        var serverId = Int64.Parse(Context.Guild.Id.ToString());

                        StoredProcedure procedure = new StoredProcedure();

                        DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetChatActionActiveAndInActive", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                            new System.Data.SqlClient.SqlParameter("@Message", word)
                        });

                        if (dt.Rows.Count > 0)
                        {
                            if (attachment.Size > 52428800)
                            {
                                throw new Exception("The attachment provided is too large, please create the keyword using an attachment that is 50mb or less.");
                            }
                            else
                            {
                                string path = @"C:\Users\Unmolded\Desktop\DiscordBot\KeywordAttachment\" + attachment.Filename;
                                using (WebClient client = new WebClient())
                                {
                                    client.DownloadFileAsync(new Uri(attachment.Url), path);
                                }

                                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateChatAction", new List<System.Data.SqlClient.SqlParameter>
                                {
                                    new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                                    new System.Data.SqlClient.SqlParameter("@Keyword", word),
                                    new System.Data.SqlClient.SqlParameter("@Action", path)
                                });

                                string title = "BigBirdBot - Keyword Updated";
                                string desc = $"Successfully updated Keyword -> **{word}**\n Action -> Attachment added.";
                                string thumbnailUrl = "";
                                string imageUrl = "";
                                string embedCreatedBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"This keyword does not exist for this server.";
                            string thumbnailUrl = Constants.Constants.errorImageUrl;
                            string imageUrl = "";
                            string createdByMsg = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                        }
                    }
                    else
                    {
                        if (action.Trim().Length > 0)
                        {
                            if (keyword.Length <= 50 && action.Length <= 2000)
                            {
                                string word = keyword.Trim();
                                action = action.Trim();
                                string createdBy = Context.User.Username;
                                var serverId = Int64.Parse(Context.Guild.Id.ToString());

                                StoredProcedure procedure = new StoredProcedure();

                                DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetChatActionActiveAndInActive", new List<System.Data.SqlClient.SqlParameter>
                                {
                                    new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                                    new System.Data.SqlClient.SqlParameter("@Message", word)
                                });

                                if (dt.Rows.Count > 0)
                                {
                                    procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateChatAction", new List<System.Data.SqlClient.SqlParameter>
                                    {
                                        new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                                        new System.Data.SqlClient.SqlParameter("@Keyword", word),
                                        new System.Data.SqlClient.SqlParameter("@Action", action)
                                    });

                                    string title = "BigBirdBot - Keyword Updated";
                                    string desc = $"Successfully updated Keyword -> **{word}**\n Action -> {action}";
                                    string thumbnailUrl = "";
                                    string imageUrl = "";
                                    string embedCreatedBy = "Command from: " + Context.User.Username;

                                    EmbedHelper embed = new EmbedHelper();
                                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                                }
                                else
                                {
                                    string title = "BigBirdBot - Error";
                                    string desc = $"This keyword does not exist for this server.";
                                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                                    string imageUrl = "";
                                    string createdByMsg = "Command from: " + Context.User.Username;

                                    EmbedHelper embed = new EmbedHelper();
                                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                                }
                            }
                            else
                            {
                                string title = "BigBirdBot - Error";
                                string desc = $"Maximum number of characters for the action is 50 characters and the action is 2000 characters.";
                                string thumbnailUrl = Constants.Constants.errorImageUrl;
                                string imageUrl = "";
                                string createdBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"To add a chat keyword action, enter a word and action.  Ex: -ka laugh, LOL";
                            string thumbnailUrl = Constants.Constants.errorImageUrl;
                            string imageUrl = "";
                            string createdBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                        }
                    }
                }
                else if (keyword.Trim().Length > 0)
                {
                    string word = keyword.Trim();
                    string createdBy = Context.User.Username;
                    var serverId = Int64.Parse(Context.Guild.Id.ToString());

                    StoredProcedure procedure = new StoredProcedure();

                    DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetChatActionActiveAndInActive", new List<System.Data.SqlClient.SqlParameter>
                    {
                        new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                        new System.Data.SqlClient.SqlParameter("@Message", word)
                    });

                    if (dt.Rows.Count > 0)
                    {
                        procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateChatAction", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                            new System.Data.SqlClient.SqlParameter("@Keyword", word),
                            new System.Data.SqlClient.SqlParameter("@Action", "")
                        });

                        string title = "BigBirdBot - Keyword Updated";
                        string desc = $"Successfully enabled Keyword -> **{word}**\n";
                        string thumbnailUrl = "";
                        string imageUrl = "";
                        string embedCreatedBy = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                    }
                    else
                    {
                        string title = "BigBirdBot - Error";
                        string desc = $"This keyword does not exist for this server.";
                        string thumbnailUrl = Constants.Constants.errorImageUrl;
                        string imageUrl = "";
                        string createdByMsg = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                    }
                }
                else
                {
                    string title = "BigBirdBot - Error";
                    string desc = $"To add a chat keyword action, enter a word and action.  Ex: -ka laugh, LOL";
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdBy = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
                }
            }
            catch (Exception e)
            {
                string title = "BigBirdBot - Error";
                string desc = $"Keyword added resulted in an error.\n" + e.Message;
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [SlashCommand("kadelete", "Deletes a keyword from the server.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleKeywordDelete([MinLength(1)]  string keyword)
        {
            await DeferAsync();
            var serverId = Int64.Parse(Context.Guild.Id.ToString());

            StoredProcedure procedure = new StoredProcedure();

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetChatAction", new List<System.Data.SqlClient.SqlParameter>
            {
                new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                new System.Data.SqlClient.SqlParameter("@Message", keyword.Trim())
            });

            if (dt.Rows.Count > 0)
            {
                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "DisableChatKeyword", new List<System.Data.SqlClient.SqlParameter>
                {
                    new System.Data.SqlClient.SqlParameter("@ServerID", serverId),
                    new System.Data.SqlClient.SqlParameter("@Keyword", keyword.Trim())
                });

                string title = "BigBirdBot - Keyword Disabled";
                string desc = $"Successfully disabled Keyword -> **{keyword.Trim()}**";
                string thumbnailUrl = "";
                string imageUrl = "";
                string embedCreatedBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"This keyword does not exist for this server.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [SlashCommand("ascii", "Turn words into ascii.")]
        public async Task HandleAscii([MinLength(1)] string message)
        {
            await DeferAsync();
            await FollowupAsync($"```{FiggleFonts.Standard.Render(message.Trim())}```");
        }

        [SlashCommand("delete", "Removes messages from the chat.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleDelete([MinValue(1), MaxValue(20)] int numToDelete)
        {
            await DeferAsync();
            var channel = Context.Channel as SocketTextChannel;
            var messages = await channel.GetMessagesAsync(numToDelete + 1).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);
        }

        [SlashCommand("poll", "Create a poll for people to vote on.")]
        public async Task HandlePoll([MinLength(1)] string statement, [MinLength(1)] string pollAnswer1, [MinLength(1)] string pollAnswer2, string pollAnswer3 = null, string pollAnswer4 = null, string pollAnswer5 = null, string pollAnswer6 = null, string pollAnswer7 = null, string pollAnswer8 = null, string pollAnswer9 = null, string pollAnswer10 = null)
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
            string desc = $"Poll Item: **{items[0]}**\n\nChoices:";
            string createdByMsg = "Command from: " + Context.User.Username;

            for (int i = 1; i < items.Count; i++)
                desc += "\n" + i.ToString() + ". **" + items[i] + "**";

            IUserMessage msg = await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue).Build());

            for (int i = 0; i < items.Count - 1; i++)
                await msg.AddReactionAsync(emojis[i]);
        }

        [SlashCommand("addkeymulti", "Adds a keyword that can access multiple actions.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleAddKeyMulti([MinLength(1)] string addCommand, [MinLength(1)] string keyword, [MinLength(1)] string chatName, [Choice("Yes", "Yes"), Choice("No", "No"), MinLength(1)] string createChannel)
        {
            await DeferAsync();
            try
            {
                StoredProcedure stored = new StoredProcedure();
                addCommand = addCommand.Trim();
                keyword = keyword.Trim();
                chatName = chatName.Trim();
                createChannel = createChannel.Trim();

                string createdBy = Context.User.Username;
                var serverId = Int64.Parse(Context.Guild.Id.ToString());
                EmbedHelper embed = new EmbedHelper();
                string title = "";
                string desc = $"Added Command Successfully.";
                string createdByMsg = "Command from: " + Context.User.Username;

                // Create text channel in a specific category
                string textChannelName = chatName;

                var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
                var categoryId = guild.CategoryChannels.First(c => c.Name == "thirsting").Id; // prod: thirsting

                if (categoryId == default(ulong) && createChannel.Equals("Yes"))
                {
                    await guild.CreateCategoryChannelAsync("thirsting");
                    title = "BigBirdBot - Keyword Multi Information";
                    desc = "The channel category does not exist, creating one for you to store the thirsting channels.\n**NOTE**: For a channel to not be created, pass 'no' into the command arguments.\n";
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }

                DataTable dtCheck = stored.Select(Constants.Constants.discordBotConnStr, "CheckKeywordExistsThirstMap", new List<SqlParameter>
                {
                    new SqlParameter("@Keyword", keyword)
                });

                if (dtCheck.Rows.Count > 0)
                {
                    // Keyword exists already so we just need to create the channel and map to the server
                    foreach (DataRow drCheck in dtCheck.Rows)
                    {
                        if (drCheck["ServerID"].ToString().Equals(serverId.ToString()))
                        {
                            throw new Exception("Keyword exists for this server");
                        }
                    }

                    DataRow dr = dtCheck.Rows[0];

                    stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddThirstCommand", new List<System.Data.SqlClient.SqlParameter>
                    {
                        new SqlParameter("@ServerID", serverId),
                        new SqlParameter("@Keyword", keyword),
                        new SqlParameter("@AddKeyword", dr["AddKeyword"].ToString()),
                        new SqlParameter("@CreatedBy", createdBy),
                        new SqlParameter("@TableName", dr["TableName"].ToString())
                    });
                }
                else
                {
                    // Add Thirst Command
                    stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddThirstCommand", new List<System.Data.SqlClient.SqlParameter>
                    {
                        new SqlParameter("@ServerID", serverId),
                        new SqlParameter("@Keyword", keyword),
                        new SqlParameter("@AddKeyword", addCommand),
                        new SqlParameter("@CreatedBy", createdBy),
                        new SqlParameter("@TableName", chatName)
                    });

                    // Create directory on the server
                    Directory.CreateDirectory(@"C:\Users\Unmolded\Desktop\DiscordBot\" + chatName + "_Thirst");
                }

                // Have it not create a channel
                if (createChannel.Equals("No"))
                {
                    // Output when we are all good
                    title = "BigBirdBot - " + chatName + " Information";
                    desc = $"Keyword Added: **{keyword}**\nAdd Command: **-{addCommand}**";
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }
                else
                {
                    title = "BigBirdBot - " + chatName + " Information";
                    desc = $"Keyword Added: **{keyword}**\nAdd Command: **-{addCommand}**";
                    await guild.CreateTextChannelAsync(textChannelName, tcp => tcp.CategoryId = categoryId).Result.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build()).Result.PinAsync();

                    // Output when we are all good
                    title = "BigBirdBot - Added Keyword Multi Command";
                    desc = "Added command successfully, please check the **" + chatName + "** channel created.";
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }
            }
            catch (Exception ex)
            {
                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Keyword Multi Error";
                string desc = ex.Message;
                string createdByMsg = "Command from: " + Context.User.Username;
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build());
            }
        }

        [SlashCommand("addbirthday", "Adds a role members birthday to celebrate.")]
        public async Task HandleBirthday(SocketGuildUser user, DateTime birthday)
        {
            await DeferAsync();
            StoredProcedure storedProcedure = new StoredProcedure();
            try
            {
                var serverId = Int64.Parse(Context.Guild.Id.ToString());
                var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));

                if (guild.Roles.Where(s => s.Name.Contains("birthday")).Count() == 0)
                {
                    // Create the birthday role and add all the users in the server
                    await guild.CreateRoleAsync("birthday", null, Discord.Color.Purple, false, true, null);

                    var embed = new EmbedBuilder
                    {
                        Title = "BigBirdBot - Birthday",
                        Color = Color.Gold,
                        Description = "A **birthday** role was created, please have an administrator add the users to this role before running this command again."
                    };

                    return;
                }

                DataTable dtNewEvent = storedProcedure.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                {
                    new SqlParameter("@EventDateTime", birthday),
                    new SqlParameter("@EventName", user.DisplayName + " Birthday"),
                    new SqlParameter("@EventDescription", "Happy Birthday to " + user.DisplayName),
                    new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(birthday, TimeZoneInfo.Local)),
                    new SqlParameter("@EventChannelSource", Context.Channel.Id.ToString()),
                    new SqlParameter("@CreatedBy", guild.Roles.Where(s => s.Name.Contains("birthday")).Select(s => s.Mention).FirstOrDefault())
                });

                foreach (DataRow dr in dtNewEvent.Rows)
                {
                    var embed = new EmbedBuilder
                    {
                        Title = ":calendar_spiral: BigBirdBot - Birthday - " + dr["EventName"].ToString(),
                        Color = Color.Gold
                    };
                    embed
                        .AddField("Time", dr["eventDateTime"].ToString())
                        .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                        .WithCurrentTimestamp();
                    await FollowupAsync(embed: embed.Build());
                }
            }
            catch (Exception e)
            {
                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Birthday Error";
                string desc = e.Message;
                string createdByMsg = "Command from: " + Context.User.Username;
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build());
            }
        }

        [SlashCommand("avatar", "See you or someone else's avatar in high quality.")]
        public async Task HandleAvatarCommand(SocketGuildUser user = null)
        {
            await DeferAsync();
            try
            {
                if (user == null)
                    user = Context.User as SocketGuildUser;

                var embed = new EmbedBuilder
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
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("delmultiurl", "Deletes a multi-keyword URL with a given table and link.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleThirstURLDelete([MinLength(1)] string url, [MinLength(1)] string chatName)
        {
            await DeferAsync();

            EmbedHelper embedHelper = new EmbedHelper();
            string tableName = chatName.Trim();
            url = url.Trim();

            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "CheckIfThirstURLExists", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", url),
                new SqlParameter("TableName", tableName)
            });

            if (dt.Rows.Count > 0)
            {
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteThirstURL", new List<SqlParameter>
                    {
                        new SqlParameter("@FilePath", url),
                        new SqlParameter("TableName", tableName)
                    });

                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Delete Successful", $"URL {url} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The URL doesn't exist in the table provided or the table doesn't exist.", Constants.Constants.errorImageUrl, Context.User.Username, Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }
        
        [SlashCommand("addthirstevent", "Adds a scheduled job to send a photo for a user.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleThirstEventAdd(SocketGuildUser user, [MinLength(1)] string chatName)
        {
            await DeferAsync();

            try
            {
                StoredProcedure stored = new StoredProcedure();
                EmbedHelper embedHelper = new EmbedHelper();

                var tableName = chatName.Trim();

                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "AddEventScheduledTime", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(user.Id.ToString())),
                    new SqlParameter("@TableName", tableName.Trim())
                });

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Thirst User Added", $"{tableName.Trim()} was successfully added and they will start receiving this on {DateTime.Parse(dr["ScheduleTime"].ToString()).ToString("MM/dd/yyyy hh:mm t")} ET.", "", Context.User.Username, Color.Blue, "");
                        await FollowupAsync(embed: embed.Build());
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("delkeymulti", "Deletes a multi-keyword that was created.")]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleThirstDelete([MinLength(1)] string keyword)
        {
            await DeferAsync();

            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embedHelper = new EmbedHelper();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "CheckKeywordExistsThirstMapByServer", new List<SqlParameter>
            {
                new SqlParameter("@Keyword", keyword.Trim()),
                new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString()))
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteThirstCommand", new List<SqlParameter>
                    {
                        new SqlParameter("@ChatKeywordID", int.Parse(dr["ChatKeywordID"].ToString())),
                        new SqlParameter("@TableName", dr["TableName"].ToString())
                    });

                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Delete Successful", "The thirst/multi-keyword provided was removed successfully.", "", "", Color.Blue, "");
                    await FollowupAsync(embed: embed.Build());
                }
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The thirst/multi-keyword entered does not exist.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("announcement", "Broadcast a message to all server.")]
        [Discord.Interactions.RequireOwner]
        public async Task HandleAnnouncement([Remainder] string message)
        {
            await DeferAsync();
            try
            {
                StoredProcedure stored = new StoredProcedure();

                // GetServer ulong IDs
                // var test = Context.Client.GetGuild(id).Users.Where(s => s.IsBot == false).ToList();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServersNonNullDefaultChannel", new List<SqlParameter>());
                EmbedHelper embedHelper = new EmbedHelper();
                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        await Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).DefaultChannel.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Announcement", message, "", "BigBirdBot", Discord.Color.Gold, null, null).Build());
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
    }
}
