using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Figgle;
using Microsoft.VisualBasic;
using OpenAI_API.Images;
using OpenAI_API.Models;
using System;
using System.Data;
using System.Net;
using Fergun.Interactive;
using Discord;
using Newtonsoft.Json;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Modules
{
    public class ParameterCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();
        public InteractiveService Interactive { get; set; }

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
                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateChatKeyword", new List<System.Data.SqlClient.SqlParameter>
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

        [Command("tts")]
        public async Task HandleTTS([Remainder] string msg)
        {
            audit.InsertAudit("tts", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (msg.Length > 0)
            {
                await ReplyAsync(msg, true);
            }
            else
            {
                EmbedHelper embed = new EmbedHelper();
                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error", "Please provide a message to use this command.", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "").Build());
            }
        }

        [Command("timestamp")]
        [Alias("ts")]
        public async Task HandleTimeStamp([Remainder] DateTime dateTime)
        {
            audit.InsertAudit("timestamp", Context.User.Username, Constants.Constants.discordBotConnStr);
            EmbedHelper embed = new EmbedHelper();
            await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Timestamp", Constants.Constants.ToDiscordUnixTimeestampFormat(dateTime), "", Context.Message.Author.ToString(), Discord.Color.Green, "").Build());
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
                string desc = $"There must be more than one choose specified.";
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

        [Command("xiv")]
        public async Task HandleXIVAPI([Remainder] string query)
        {
            string apiUrl = $"https://xivapi.com/search?string={query}";
            var result = await apiUrl.GetAsync();
            string test = JsonConvert.DeserializeObject(result.ResponseMessage.Content.ReadAsStringAsync().Result).ToString();
            var jsonObject = JObject.Parse(test);   
        }
    }
}
