using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Figgle;
using OpenAI_API.Models;
using System.Data;
using System.Net;
using Discord;
using System.Data.SqlClient;

namespace DiscordBot.Modules
{
    public class ParameterCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();

        [Command("random")]
        [Alias("r")]
        [Discord.Commands.Summary("Random number out of a certain range.")]
        public async Task GenerateRandomNumber([Remainder] int number)
        {
            audit.InsertAudit("random", Context.User.Username, Constants.Constants.discordBotConnStr);

            Random r = new Random();
            int i = r.Next(1, number + 1);

            string title = "BigBirdBot - Random";
            string desc = $"{Context.User.Mention} rolled a **{i}**";
            string thumbnailUrl = "https://media.hswstatic.com/eyJidWNrZXQiOiJjb250ZW50Lmhzd3N0YXRpYy5jb20iLCJrZXkiOiJnaWZcL2Nhc2luby1kaWNlLXRlc3RpbmctMS5qcGciLCJlZGl0cyI6eyJyZXNpemUiOnsid2lkdGgiOjI5MH0sInRvRm9ybWF0IjoiYXZpZiJ9fQ==";
            string createdBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [Command("etext")]
        [Alias("em")]
        [Discord.Commands.Summary("Turn your message into emojis.")]
        public async Task HandleEmojiTextCommand([Remainder] string message)
        {
            audit.InsertAudit("etext", Context.User.Username, Constants.Constants.discordBotConnStr);

            EmojiText emoji= new EmojiText();
            await ReplyAsync(emoji.GetEmojiString(message));
        }

        [Command("8ball")]
        [Alias("8b")]
        [Discord.Commands.Summary("Shake the figurative Eight Ball.")]
        public async Task HandleEightBallCommand([Remainder] string message)
        {
            audit.InsertAudit("8ball", Context.User.Username, Constants.Constants.discordBotConnStr);

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
            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [Command("image")]
        [Alias("i")]
        [Discord.Commands.Summary("Generate an image off a prompt.")]
        public async Task HandleImageGen([Remainder] string prompt)
        {
            audit.InsertAudit("image", Context.User.Username, Constants.Constants.discordBotConnStr);
            CommandHelper helper = new CommandHelper();
            var image = helper.GetImage(prompt).Result;
            if (image.Data != null && image.Data.Count > 0)
            {
                string title = "BigBirdBot - Image";
                string desc = $"{prompt}";
                string thumbnailUrl = "";
                string imageUrl = image.Data[0].Url;
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green, imageUrl).Build());
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"**There were no images found or the prompt was inappropriate.**";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("ka")]
        public async Task HandleKeywordAdd([Remainder] string keyword)
        {
            audit.InsertAudit("ka", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (keyword.Trim().Length> 0 && keyword.Contains(","))
            {
                var chatKeywordAction = keyword.Split(",");

                if (chatKeywordAction[1].Trim().Length > 0)
                {
                    if (chatKeywordAction[0].Length <= 50 && chatKeywordAction[1].Length <= 2000)
                    {
                        string word = chatKeywordAction[0].Trim();
                        string action = chatKeywordAction[1].Trim();
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
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                    }
                    else
                    {
                        string title = "BigBirdBot - Error";
                        string desc = $"Maximum number of characters for the action is 50 characters and the action is 2000 characters.";
                        string thumbnailUrl = Constants.Constants.errorImageUrl;
                        string imageUrl = "";
                        string createdBy = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
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
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
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
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("kaedit")]
        [Alias("kae")]
        public async Task HandleKeywordUpdate([Remainder] string keyword)
        {
            audit.InsertAudit("kae", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (keyword.Trim().Length > 0 && keyword.Contains(","))
            {
                var chatKeywordAction = keyword.Split(",");

                if (chatKeywordAction[1].Trim().Length > 0)
                {
                    if (chatKeywordAction[0].Length <= 50 && chatKeywordAction[1].Length <= 2000)
                    {
                        string word = chatKeywordAction[0].Trim();
                        string action = chatKeywordAction[1].Trim();
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
                            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"This keyword does not exist for this server.";
                            string thumbnailUrl = Constants.Constants.errorImageUrl;
                            string imageUrl = "";
                            string createdByMsg = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
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
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
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
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
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
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("kadelete")]
        [Alias("kad")]
        public async Task HandleKeywordDelete([Remainder] string keyword)
        {
            audit.InsertAudit("kad", Context.User.Username, Constants.Constants.discordBotConnStr);
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
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"This keyword does not exist for this server.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("mcmod")]
        public async Task MCAddJar()
        {
            audit.InsertAudit("mcmod", Context.User.Username, Constants.Constants.discordBotConnStr);
            WebClient wc = new WebClient();
            var attachments = Context.Message.Attachments;
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    try
                    {
                        wc.DownloadFile(attachment.Url, Constants.Constants.minecraftModsLocation + attachment.Filename);

                        string title = "BigBirdBot - Minecraft Mod";
                        string desc = $"Added {attachment.Filename} to the mods directory of the server, this will be enabled on next server restart.";
                        string thumbnailUrl = "";
                        string createdBy = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
                    }
                    catch (Exception e)
                    {
                        EmbedHelper embed = new EmbedHelper();
                        await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "").Build());
                    }
                }
            }
        }

        [Command("math")]
        public async Task HandleMath([Remainder] int number)
        {
            audit.InsertAudit("math", Context.User.Username, Constants.Constants.discordBotConnStr);
            var royalroad = number * 1.25;
            var normal = number * 1.5;
            var whoknows = number * 1.75;
            var blackjack = number * 2.0;

            await ReplyAsync("Original Number: " + number.ToString("N") + "\n**125% -> " + royalroad.ToString("N") + "\n150% -> " + normal.ToString("N") + "\n175% -> " + whoknows.ToString("N") + "\n200% -> " + blackjack.ToString("N") + "**");
        }

        [Command("ascii")]
        public async Task HandleAscii([Remainder] string message)
        {
            audit.InsertAudit("ascii", Context.User.Username, Constants.Constants.discordBotConnStr);
            await ReplyAsync($"```{FiggleFonts.Standard.Render(message.Trim())}```");
        }

        [Command("choose")]
        public async Task HandleChoose([Remainder] string message)
        {
            audit.InsertAudit("choose", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (message.Contains(","))
            {
                var resultSplit = message.Split(",");
                Random r = new Random();
                int choice = r.Next(0, resultSplit.Length);
                string title = "BigBirdBot - Choice";
                string desc = $"I'm going with **{resultSplit[choice]}**";
                string thumbnailUrl = "";
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Green, imageUrl).Build());
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"There must be more than one choice specified.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("animal")]
        [Alias("a")]
        public async Task HandleChat([Remainder] string message)
        {
            audit.InsertAudit("animal", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (message.Contains(","))
            {
                var resultSplit = message.Split(",");
                string prompt = "Provide a response like a " + resultSplit[0].Trim() + " to the following: \"" + resultSplit[1].Trim() + "\"";
                var api = new OpenAI_API.OpenAIAPI(Constants.Constants.openAiSecret);
                var result = await api.Completions.CreateCompletionAsync(new OpenAI_API.Completions.CompletionRequest(message, model: Model.DavinciText, max_tokens: 1000, temperature: 0.9, null, null, 1, null, null));
                var response = result.ToString();

                int length = response.Length;

                if (response.Length > 2000)
                {
                    await ReplyAsync(response.Substring(0, 2000));
                    await ReplyAsync(response.Substring(2000, length - 2000));
                }
                else
                    await ReplyAsync(response);

                await ReplyAsync("---END RESPONSE---");
                // Starting point for conversation of ChatGPT, need to research this more when more invested.
                //var responseRequest = await Interactive.NextMessageAsync(x => x.Channel.Id == Context.Channel.Id, timeout: TimeSpan.FromMinutes(1));

                //if (responseRequest != null && responseRequest.IsSuccess)
                //{

                //}
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"There must be more than one choose specified.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("delete")]
        [Alias("del")]
        public async Task HandleDelete([Remainder] int numToDelete)
        {
            if (numToDelete < 1 || numToDelete > 20) 
            {
                string title = "BigBirdBot - Error";
                string desc = $"The number must be between 1 - 20.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
            else
            {
                var messages = await Context.Channel.GetMessagesAsync(numToDelete, Discord.CacheMode.AllowDownload, null).FlattenAsync();

                foreach (var message in messages)
                {
                    await message.DeleteAsync();
                }
            }
        }

        [Command("poll")]
        [Alias("p")]
        public async Task HandlePoll([Remainder] string args = "")
        {
            audit.InsertAudit("poll", Context.User.Username, Constants.Constants.discordBotConnStr);
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

            if (args.Length > 0 && args.Split(',').Count() > 2)
            {
                if (args.Split(',').Count() > 11)
                {
                    // Error handling, let's be reasonable with the number of choices
                    string title = "BigBirdBot - Error";
                    string desc = $"The maximum number of poll choices is 10.";
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                }
                else
                {
                    List<string> items = args.Split(',').Select(s => s.Trim()).ToList();
                    EmbedHelper embed = new EmbedHelper();
                    string title = "BigBirdBot - Poll";
                    string desc = $"Poll Item: **{items[0]}**\n\nChoices:";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    for (int i = 1; i < items.Count; i++)
                        desc += "\n" + i.ToString() + ". **" + items[i] + "**";

                    IUserMessage msg = await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue).Build());
                    
                    for (int i = 0; i < items.Count - 1; i++)
                        await msg.AddReactionAsync(emojis[i]);
                }
            }
            else
            {
                // Error hnadling
                string title = "BigBirdBot - Error";
                string desc = $"There must be more than one choice specified.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("ericadd")]
        [Alias("tbadd", "tba")]
        public async Task HandleEricAdd([Remainder] string args = "")
        {
            if (args.Length > 0)
            {
                StoredProcedure stored = new StoredProcedure();

                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddEric", new List<System.Data.SqlClient.SqlParameter>
                {
                    new System.Data.SqlClient.SqlParameter("@URL", args)
                });

                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Added Image";
                string desc = $"Added {args} successfully.";
                string createdByMsg = "Command from: " + Context.User.Username;

                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
            }
            else if (Context.Message.Attachments.Count > 0)
            {
                var attachments = Context.Message.Attachments;
                try
                {
                    StoredProcedure stored = new StoredProcedure();
                    foreach (var attachment in attachments)
                    {
                        string path = @"C:\Users\Unmolded\Desktop\DiscordBot\Eric\" + attachment.Filename;
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFileAsync(new Uri(attachment.Url), path);
                        }

                        stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddEric", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new SqlParameter("@URL", path)
                        });
                    }

                    EmbedHelper embed = new EmbedHelper();
                    string title = "BigBirdBot - Added Image";
                    string desc = $"Added attachment(s) successfully.";
                    string createdByMsg = "Command from: " + Context.User.Username;
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }
                catch (Exception ex)
                {
                    // Error handling
                    string title = "BigBirdBot - Error";
                    string desc = ex.Message;
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                }
            }
            else
            {
                // Error hnadling
                string title = "BigBirdBot - Error";
                string desc = $"Please provide a URL or image of Eric.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("ericdelete")]
        [Alias("tbdelete", "tbd")]
        public async Task HandleEricDelete([Remainder] string args = "")
        {
            if (args.Length > 0)
            {
                StoredProcedure stored = new StoredProcedure();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "DeleteEric", new List<System.Data.SqlClient.SqlParameter>
                {
                    new System.Data.SqlClient.SqlParameter("@URL", args)
                });
                string result = "";

                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString();

                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Delete Image";
                string desc = result;
                string createdByMsg = "Command from: " + Context.User.Username;

                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
            }
            else
            {
                // Error hnadling
                string title = "BigBirdBot - Error";
                string desc = $"Please provide a URL of Eric to delete.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("emetadd")]
        public async Task HandleEmetAdd([Remainder] string args = "")
        {
            var attachments = Context.Message.Attachments;
            if (attachments.Count > 0)
            {
                try
                {
                    StoredProcedure stored = new StoredProcedure();
                    foreach (var attachment in attachments)
                    {
                        string path = @"C:\Users\Unmolded\Desktop\DiscordBot\Emet\" + attachment.Filename;
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFileAsync(new Uri(attachment.Url), path);
                        }

                        stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddEmet", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new SqlParameter("@FilePath", path)
                        });
                    }

                    EmbedHelper embed = new EmbedHelper();
                    string title = "BigBirdBot - Added Image";
                    string desc = $"Added attachment(s) successfully.";
                    string createdByMsg = "Command from: " + Context.User.Username;
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }
                catch (Exception ex)
                {
                    // Error handling
                    string title = "BigBirdBot - Error";
                    string desc = ex.Message;
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                }
            }
            else
            {
                // Error handling
                string title = "BigBirdBot - Error";
                string desc = $"Please provide an image.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("themisadd")]
        public async Task HandleThemisAdd([Remainder] string args = "")
        {
            if (args.Length > 0)
            {
                StoredProcedure stored = new StoredProcedure();

                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddThemis", new List<System.Data.SqlClient.SqlParameter>
                {
                    new System.Data.SqlClient.SqlParameter("@FilePath", args)
                });

                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Added Image";
                string desc = $"Added {args} successfully.";
                string createdByMsg = "Command from: " + Context.User.Username;

                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
            }
            else if (Context.Message.Attachments.Count > 0)
            {
                var attachments = Context.Message.Attachments;
                try
                {
                    StoredProcedure stored = new StoredProcedure();
                    foreach (var attachment in attachments)
                    {
                        string path = @"C:\Users\Unmolded\Desktop\DiscordBot\Themis\" + attachment.Filename;
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFileAsync(new Uri(attachment.Url), path);
                        }

                        stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddThemis", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new SqlParameter("@FilePath", path)
                        });
                    }

                    EmbedHelper embed = new EmbedHelper();
                    string title = "BigBirdBot - Added Image";
                    string desc = $"Added attachment(s) successfully.";
                    string createdByMsg = "Command from: " + Context.User.Username;
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                }
                catch (Exception ex)
                {
                    // Error handling
                    string title = "BigBirdBot - Error";
                    string desc = ex.Message;
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                }
            }
            else
            {
                // Error hnadling
                string title = "BigBirdBot - Error";
                string desc = $"Please provide a URL or image of Themis.";
                string thumbnailUrl = Constants.Constants.errorImageUrl;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
            }
        }
    }
}
