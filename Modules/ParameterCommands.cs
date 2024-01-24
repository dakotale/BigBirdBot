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
using Discord.WebSocket;
using System.Runtime.CompilerServices;
using Flurl;

namespace DiscordBot.Modules
{
    public class ParameterCommands : ModuleBase<SocketCommandContext>
    {
        [Command("random")]
        [Alias("r")]
        [Discord.Commands.Summary("Random number out of a certain range.")]
        public async Task GenerateRandomNumber([Remainder] int number)
        {
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
            EmojiText emoji= new EmojiText();
            await ReplyAsync(emoji.GetEmojiString(message));
        }

        [Command("8ball")]
        [Alias("8b")]
        [Discord.Commands.Summary("Shake the figurative Eight Ball.")]
        public async Task HandleEightBallCommand([Remainder] string message)
        {
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

        [Command("ka")]
        public async Task HandleKeywordAdd([Remainder] string keyword)
        {
            try
            {
                if (keyword.Trim().Length > 0 && keyword.Contains(","))
                {
                    if (Context.Message.Attachments.Count == 1)
                    {
                        var attachments = Context.Message.Attachments;
                        foreach (var attachment in attachments)
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

                                string word = keyword.Split(',')[0].Trim();
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
                                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                            }
                        }
                    }
                    else
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
            catch (Exception e)
            {
                string title = "BigBirdBot - Error";
                string desc = $"Keyword added resulted in an error.\n" + e.Message;
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
            try
            {
                if (keyword.Trim().Length > 0 && keyword.Contains(","))
                {
                    var chatKeywordAction = keyword.Split(",");

                    if (Context.Message.Attachments.Count == 1)
                    {
                        string word = chatKeywordAction[0].Trim();
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
                            var attachments = Context.Message.Attachments;

                            foreach (var attachment in attachments)
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
                                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
                                }
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
                            await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build());
                        }
                    }
                    else
                    {
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
                    string desc = $"To add a chat keyword action, enter a word and action.  Ex: -ka laugh, LOL";
                    string thumbnailUrl = Constants.Constants.errorImageUrl;
                    string imageUrl = "";
                    string createdBy = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
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
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build());
            }
        }

        [Command("kadelete")]
        [Alias("kad")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordDelete([Remainder] string keyword)
        {
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

        //[Command("mcmod")]
        //public async Task MCAddJar()
        //{
        //    audit.InsertAudit("mcmod", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
        //    WebClient wc = new WebClient();
        //    var attachments = Context.Message.Attachments;
        //    if (attachments != null)
        //    {
        //        foreach (var attachment in attachments)
        //        {
        //            try
        //            {
        //                wc.DownloadFile(attachment.Url, Constants.Constants.minecraftModsLocation + attachment.Filename);

        //                string title = "BigBirdBot - Minecraft Mod";
        //                string desc = $"Added {attachment.Filename} to the mods directory of the server, this will be enabled on next server restart.";
        //                string thumbnailUrl = "";
        //                string createdBy = "Command from: " + Context.User.Username;

        //                EmbedHelper embed = new EmbedHelper();
        //                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        //            }
        //            catch (Exception e)
        //            {
        //                EmbedHelper embed = new EmbedHelper();
        //                await ReplyAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "").Build());
        //            }
        //        }
        //    }
        //}

        [Command("math")]
        public async Task HandleMath([Remainder] int number)
        {
            var royalroad = number * 1.25;
            var normal = number * 1.5;
            var whoknows = number * 1.75;
            var blackjack = number * 2.0;

            await ReplyAsync("Original Number: " + number.ToString("N") + "\n**125% -> " + royalroad.ToString("N") + "\n150% -> " + normal.ToString("N") + "\n175% -> " + whoknows.ToString("N") + "\n200% -> " + blackjack.ToString("N") + "**");
        }

        [Command("ascii")]
        public async Task HandleAscii([Remainder] string message)
        {
            await ReplyAsync($"```{FiggleFonts.Standard.Render(message.Trim())}```");
        }

        [Command("choose")]
        public async Task HandleChoose([Remainder] string message)
        {
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

        [Command("delete")]
        [Alias("del")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageMessages)]
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
                var channel = Context.Channel as SocketTextChannel;
                var messages = await channel.GetMessagesAsync(numToDelete + 1).FlattenAsync();
                await channel.DeleteMessagesAsync(messages);
            }
        }

        [Command("poll")]
        [Alias("p")]
        public async Task HandlePoll([Remainder] string args = "")
        {
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

        [Command("addkeymulti")]
        [Alias("addthirst")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task HandleAddThirst([Remainder] string args = "")
        {
            /*
             * Steps to dynamically add for a character
             * 1. Take the following parameters
             * - base keyword, table name, addCommand keyword
             * 2. Dynamically create table with a different name but same columns
             * 3. Add base keyword to the ChatKeyword, and default '' to ChatAction
             * 4. Mapping table for add command to the table created
             * 5. Run insert based on the -command
             * 6. Create the table name as a folder on the server in a hardcoded directory
             * 7. Check if the -command portion of the string is in the thirst map, if it is then perform the insert logic with a returned value
             * 8. Assuming bot has permission, create the text channel using the name of the table and append the first message in it
             * 
             * Command Ex: -addthirst <addtest>, <test>, <testKeyword>, <if channel is created - optional>
             * TODO: Handle if the keyword exists to create channel in another server or return the keyword and textchannel already exists
             */
            if (args.Length > 0)
            {
                try
                {
                    StoredProcedure stored = new StoredProcedure();
                    var thirstParams = args.Split(',', StringSplitOptions.TrimEntries);

                    string createdBy = Context.User.Username;
                    var serverId = Int64.Parse(Context.Guild.Id.ToString());
                    string addKeyword = thirstParams[0];
                    string tableName = thirstParams[1].ToLower();
                    string keyword = thirstParams[2].ToLower();

                    EmbedHelper embed = new EmbedHelper();
                    string title = "";
                    string desc = $"Added Command Successfully.";
                    string createdByMsg = "Command from: " + Context.User.Username;

                    // Create text channel in a specific category
                    string textChannelName = tableName;

                    var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
                    var categoryId = guild.CategoryChannels.First(c => c.Name == "thirsting").Id; // prod: thirsting

                    if (categoryId == default(ulong) && (thirstParams.Length == 4 || thirstParams[3].Equals("no", StringComparison.OrdinalIgnoreCase)))
                    {
                        await guild.CreateCategoryChannelAsync("thirsting");
                        title = "BigBirdBot - Keyword Multi Information";
                        desc = "The channel category does not exist, creating one for you to store the thirsting channels.\n**NOTE**: For a channel to not be created, pass 'no' into the command arguments.\n**Example: -addkeymulti addword, word, work, no**";
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
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
                            new SqlParameter("@AddKeyword", addKeyword),
                            new SqlParameter("@CreatedBy", createdBy),
                            new SqlParameter("@TableName", tableName)
                        });

                        // Create directory on the server
                        Directory.CreateDirectory(@"C:\Users\Unmolded\Desktop\DiscordBot\" + tableName + "_Thirst");
                    }
                    
                    // Have it not create a channel
                    if (thirstParams.Length == 4 && thirstParams[3].Equals("no", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Output when we are all good
                        title = "BigBirdBot - " + tableName + " Information";
                        desc = $"Keyword Added: **{keyword}**\nAdd Command: **-{addKeyword}**";
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                    }
                    else
                    {
                        title = "BigBirdBot - " + tableName + " Information";
                        desc = $"Keyword Added: **{keyword}**\nAdd Command: **-{addKeyword}**";
                        await guild.CreateTextChannelAsync(textChannelName, tcp => tcp.CategoryId = categoryId).Result.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build()).Result.PinAsync();

                        // Output when we are all good
                        title = "BigBirdBot - Added Keyword Multi Command";
                        desc = "Added command successfully, please check the **" + tableName + "** channel created.";
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build());
                    }
                }
                catch (Exception ex)
                {
                    EmbedHelper embed = new EmbedHelper();
                    string title = "BigBirdBot - Keyword Multi Error";
                    string desc = ex.Message;
                    string createdByMsg = "Command from: " + Context.User.Username;
                    await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build());
                }
            }
        }

        // Requirements: Need a role called Birthday
        [Command("addbirthday")]
        public async Task HandleBirthday([Remainder] string args = "")
        {
            // Format: -addbirthday <date>, <name>
            // 1142683492447174656 is birthdays role
            StoredProcedure storedProcedure = new StoredProcedure();
            string[] objects = args.Split(',');
            try
            {
                if (objects.Length == 2)
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
                        new SqlParameter("@EventDateTime", DateTime.Parse(objects[0])),
                        new SqlParameter("@EventName", objects[1]),
                        new SqlParameter("@EventDescription", objects[1]),
                        new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(objects[0]), TimeZoneInfo.Local)),
                        new SqlParameter("@EventChannelSource", Context.Message.Channel.Id.ToString()),
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
                        await ReplyAsync(embed: embed.Build());
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Birthday Error";
                string desc = e.Message;
                string createdByMsg = "Command from: " + Context.User.Username;
                await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build());
            }
        }

        [Command("avatar")]
        [Discord.Commands.Summary("See you or someone else's avatar in high quality.")]
        public async Task HandleAvatarCommand([Remainder] SocketGuildUser user = null)
        {
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
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("delthirsturl")]
        [Summary("Delete a thirst/multi-keyword URL with a given table and link.")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstURLDelete([Remainder] string args = "")
        {
            if (args.Trim().Length > 0 && args.Contains(","))
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var param = args.Split(",").Select(s => s.Trim()).ToArray();
                string tableName = param[0];
                string url = param[1];

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
                    await ReplyAsync(embed: embed.Build());
                }
                else
                {
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The URL doesn't exist in the table provided or the table doesn't exist.", Constants.Constants.errorImageUrl, Context.User.Username, Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }
        }

        [Command("addthirstevent")]
        [Summary("Adds a scheduled job to send a photo from the thirst library for a user with a provided mention and table name.")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstEventAdd(SocketGuildUser user, [Remainder] string args)        
        {
            try
            {
                if (args.Length > 0)
                {
                    StoredProcedure stored = new StoredProcedure();
                    EmbedHelper embedHelper = new EmbedHelper();

                    var tableName = args.Split(",")[1].Trim();

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
                            await ReplyAsync(embed: embed.Build());
                        }
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

        [Command("delthirst")]
        [Summary("Delete a thirst/multi-key word that was created.")]
        [Discord.Commands.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstDelete([Remainder] string keyword)
        {
            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embedHelper = new EmbedHelper();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "CheckKeywordExistsThirstMapByServer", new List<SqlParameter>
            {
                new SqlParameter("@Keyword", keyword),
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
                    await ReplyAsync(embed: embed.Build());
                }
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The thirst/multi-keyword entered does not exist.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
