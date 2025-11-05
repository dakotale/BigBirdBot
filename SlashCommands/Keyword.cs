using System.Data;
using Microsoft.Data.SqlClient;
using System.Net;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    public class Keyword : InteractionModuleBase<SocketInteractionContext>
    {
        #region Add Keywords
        [SlashCommand("addkeyword", "Adds a keyword to the server.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordAdd([MinLength(1), MaxLength(50)] string keyword, string action = null, Attachment attachment = null)
        {
            await DeferAsync(ephemeral: true);
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
                            string path = @"C:\Temp\DiscordBot\KeywordAttachment\" + attachment.Filename;
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFileAsync(new Uri(attachment.Url), path);
                            }

                            string word = keyword.Trim();
                            string createdBy = Context.User.Username;
                            long serverId = Int64.Parse(Context.Guild.Id.ToString());

                            StoredProcedure procedure = new StoredProcedure();

                            procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "AddChatKeywordAction", new List<SqlParameter>
                            {
                                new SqlParameter("@ServerID", serverId),
                                new SqlParameter("@Keyword", word),
                                new SqlParameter("@CreatedBy", createdBy),
                                new SqlParameter("@Action", path)
                            });

                            string title = "BigBirdBot - Keyword Added";
                            string desc = $"Successfully added Keyword -> **{word}** \nAction -> Attachment added";
                            string thumbnailUrl = "";
                            string imageUrl = "";
                            string embedCreatedBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
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
                                long serverId = Int64.Parse(Context.Guild.Id.ToString());

                                StoredProcedure procedure = new StoredProcedure();

                                procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "AddChatKeywordAction", new List<SqlParameter>
                                {
                                    new SqlParameter("@ServerID", serverId),
                                    new SqlParameter("@Keyword", word),
                                    new SqlParameter("@CreatedBy", createdBy),
                                    new SqlParameter("@Action", action)
                                });

                                string title = "BigBirdBot - Keyword Added";
                                string desc = $"Successfully added Keyword -> **{word}** \nAction -> {action}";
                                string thumbnailUrl = "";
                                string imageUrl = "";
                                string embedCreatedBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
                            }
                            else
                            {
                                string title = "BigBirdBot - Error";
                                string desc = $"Maximum number of characters for the action is 50 characters and the action is 2000 characters.";
                                string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                                string imageUrl = "";
                                string createdBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"To add a chat keyword action, enter a word and action/attachment.";
                            string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                            string imageUrl = "";
                            string createdBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                        }
                    }
                }
                else
                {
                    string title = "BigBirdBot - Error";
                    string desc = $"To add a chat keyword action, enter a word and action.";
                    string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                    string imageUrl = "";
                    string createdBy = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                }
            }
            catch (Exception e)
            {
                string title = "BigBirdBot - Error";
                string desc = $"Keyword added resulted in an error.\n" + e.Message;
                string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                string imageUrl = "";
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
            }
        }

        [SlashCommand("addmultikeyword", "Adds a keyword that can access multiple actions.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleAddKeyMulti([MinLength(1), MaxLength(50)] string keyword, [Choice("Yes", "Yes"), Choice("No", "No"), MinLength(1)] string createChannel)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                StoredProcedure stored = new StoredProcedure();
                keyword = keyword.Trim();
                string addCommand = "add" + keyword;
                string chatName = string.Concat(keyword[0].ToString().ToUpper(), keyword.AsSpan(1));
                createChannel = createChannel.Trim();

                string createdBy = Context.User.Username;
                long serverId = Int64.Parse(Context.Guild.Id.ToString());
                EmbedHelper embed = new EmbedHelper();
                string title = "";
                string desc = $"Added Command Successfully.";
                string createdByMsg = "Command from: " + Context.User.Username;

                // Create text channel in a specific category
                string textChannelName = chatName;

                SocketGuild guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));
                List<SocketCategoryChannel> categoryIdList = guild.CategoryChannels.Where(s => s.Name.ToLower() == "thirsting" || s.Name.ToLower() == "stanning" || s.Name.ToLower() == "keyword multi").ToList();
                ulong categoryId = default(ulong);

                if (categoryIdList.Any())
                {
                    foreach (SocketCategoryChannel? category in categoryIdList)
                    {
                        categoryId = category.Id;
                    }
                }

                if (categoryIdList.Count == 0 && createChannel.Equals("Yes"))
                {
                    await guild.CreateCategoryChannelAsync("Keyword Multi");
                    title = "BigBirdBot - Keyword Multi Information";
                    desc = "The channel category does not exist, creating one for you to store the multiple action keyword channels.\n**NOTE**: For a channel to not be created, pass 'no' into the command arguments.\n";
                    categoryIdList = guild.CategoryChannels.Where(s => s.Name.ToLower() == "thirsting" || s.Name.ToLower() == "stanning" || s.Name.ToLower() == "keyword multi").ToList(); // prod: thirsting
                }

                DataTable dtCheck = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "CheckKeywordExistsThirstMap", new List<SqlParameter>
                {
                    new SqlParameter("@Keyword", keyword)
                });

                if (dtCheck.Rows.Count > 0)
                {
                    // Keyword exists already so we just need to create the channel and map to the server
                    foreach (DataRow drCheck in dtCheck.Rows)
                    {
                        if (drCheck["ServerID"].ToString().Equals(serverId.ToString()))
                            throw new Exception("Keyword exists for this server");
                    }

                    DataRow dr = dtCheck.Rows[0];

                    stored.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "AddThirstCommand", new List<SqlParameter>
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
                    stored.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "AddThirstCommand", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", serverId),
                        new SqlParameter("@Keyword", keyword),
                        new SqlParameter("@AddKeyword", addCommand),
                        new SqlParameter("@CreatedBy", createdBy),
                        new SqlParameter("@TableName", chatName)
                    });

                    // Create directory on the server
                    Directory.CreateDirectory(@"C:\Temp\DiscordBot\" + chatName);
                }

                // Have it not create a channel
                if (createChannel.Equals("No"))
                {
                    // Output when we are all good
                    title = "BigBirdBot - " + chatName + " Information";
                    desc = $"Keyword Added: **{keyword}**\nAdd Command: **-{addCommand}**";
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Blue).Build(), ephemeral: true);
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
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Color.Red).Build(), ephemeral: true);
            }
        }

        [SlashCommand("addmultieventadmin", "Adds a scheduled job to send a photo for a user.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstEventAdminAdd(SocketGuildUser user, [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                StoredProcedure stored = new StoredProcedure();
                EmbedHelper embedHelper = new EmbedHelper();

                string tableName = keyword.Trim();

                DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "AddEventScheduledTime", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(user.Id.ToString())),
                    new SqlParameter("@TableName", tableName)
                });

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Multiple Keyword User Added", $"{tableName} was successfully added and **{user.Username}** will start receiving this on {DateTime.Parse(dr["ScheduleTime"].ToString()).ToString("MM/dd/yyyy hh:mm t")} ET.\nThe current list of multi people/characters for this user are; *{dr["ScheduledEventTable"].ToString()}*", "", Context.User.Username, Color.Blue, "");
                        await FollowupAsync(embed: embed.Build(), ephemeral: true);
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.ERROR_IMAGE_URL, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }

        [SlashCommand("addmultievent", "Adds a scheduled job based on the current channel you're in.")]
        [EnabledInDm(false)]
        public async Task HandleThirstEventAdd()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                StoredProcedure stored = new StoredProcedure();
                EmbedHelper embedHelper = new EmbedHelper();

                string tableName = Context.Channel.Name.Trim();

                DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "AddEventScheduledTime", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(Context.User.Id.ToString())),
                    new SqlParameter("@TableName", tableName)
                });

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Multiple Keyword User Added", $"{tableName} was successfully added and **{Context.User.Username}** will start receiving this on {DateTime.Parse(dr["ScheduleTime"].ToString()).ToString("MM/dd/yyyy hh:mm t")} ET.\nThe current list of multi people/characters for you are; *{dr["ScheduledEventTable"].ToString()}*", "", Context.User.Username, Color.Blue, "");
                        await FollowupAsync(embed: embed.Build(), ephemeral: true);
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.ERROR_IMAGE_URL, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }

        [SlashCommand("addkeywordurl", "Add the same link to multiple comma-separated keywords in your server.")]
        [EnabledInDm(false)]
        public async Task HandleMultipleLink([MinLength(1), MaxLength(50)] string keyword, string url)
        {
            await DeferAsync(ephemeral: true);

            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.DISCORD_BOT_CONN_STR;
            List<string> multipleKeywords = new List<string>();
            URLCleanup cleanup = new URLCleanup();

            if (keyword.Contains(","))
                multipleKeywords = keyword.Split(',', StringSplitOptions.TrimEntries).ToList();
            else
                multipleKeywords.Add(keyword);

            foreach (string m in multipleKeywords)
            {
                url = cleanup.CleanURLEmbed(url);

                // Check if it's in the ThirstMap and run the add command
                List<SqlParameter> parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("@AddKeyword", "add" + m));

                DataTable dt = stored.Select(connStr, "GetThirstTableByMap", parameters);
                if (dt.Rows.Count > 0)
                {
                    bool multiUrl = false;

                    if (url.Contains(","))
                        multiUrl = true;

                    if (multiUrl)
                    {
                        string[] urls = url.Split(",", StringSplitOptions.TrimEntries);
                        foreach (string u in urls)
                        {
                            bool result = u.Trim().StartsWith("http");

                            if (result)
                            {
                                // Check if link exists for thirst table
                                DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                {
                                    new SqlParameter("@FilePath", u),
                                    new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                });

                                if (dtExists.Rows.Count == 0)
                                {
                                    string userId = Context.User.Id.ToString();
                                    foreach (DataRow dr in dt.Rows)
                                    {
                                        stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
                                        {
                                            new SqlParameter("@FilePath", u),
                                            new SqlParameter("@TableName", dr["TableName"].ToString()),
                                            new SqlParameter("@UserID", userId)
                                        });
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        bool result = Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult)
                            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                        if (result)
                        {
                            // Check if link exists for thirst table
                            DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                            {
                                new SqlParameter("@FilePath", url),
                                new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                            });

                            if (dtExists.Rows.Count == 0)
                            {
                                string userId = Context.User.Id.ToString();
                                foreach (DataRow dr in dt.Rows)
                                {
                                    stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
                                    {
                                        new SqlParameter("@FilePath", url),
                                        new SqlParameter("@TableName", dr["TableName"].ToString()),
                                        new SqlParameter("@UserID", userId)
                                    });
                                }
                            }
                        }
                    }
                }
            }

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "BigBirdBot - Added Links",
                Color = Color.Blue,
                Description = $"Added link(s) successfully for **{keyword}**."
            };

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }

        [SlashCommand("addkeywordimage", "Add the same attachment to multiple comma-separated keywords in your server.")]
        [EnabledInDm(false)]
        public async Task HandleMultipleImage([MinLength(1), MaxLength(50)] string keyword, IAttachment attachment, IAttachment? attachment2 = null, IAttachment? attachment3 = null, IAttachment? attachment4 = null, IAttachment? attachment5 = null, IAttachment? attachment6 = null, IAttachment? attachment7 = null, IAttachment? attachment8 = null, IAttachment? attachment9 = null, IAttachment? attachment10 = null)
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.DISCORD_BOT_CONN_STR;
            List<string> multipleKeywords = new List<string>();

            List<IAttachment?> attachments = new List<IAttachment>();
            attachments.Add(attachment);
            attachments.Add(attachment2);
            attachments.Add(attachment3);
            attachments.Add(attachment4);
            attachments.Add(attachment5);
            attachments.Add(attachment6);
            attachments.Add(attachment7);
            attachments.Add(attachment8);
            attachments.Add(attachment9);
            attachments.Add(attachment10);

            attachments = attachments.Where(s => s != null).ToList();

            if (keyword.Contains(","))
                multipleKeywords = keyword.Split(',', StringSplitOptions.TrimEntries).ToList();
            else
                multipleKeywords.Add(keyword);

            foreach (string m in multipleKeywords)
            {
                // Check if it's in the ThirstMap and run the add command
                List<SqlParameter> parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("@AddKeyword", "add" + m));

                DataTable dt = stored.Select(connStr, "GetThirstTableByMap", parameters);
                if (dt.Rows.Count > 0)
                {
                    string userId = Context.User.Id.ToString();
                    foreach (DataRow dr in dt.Rows)
                    {
                        foreach (IAttachment? a in attachments)
                        {
                            string tablename = dr["TableName"].ToString();
                            tablename = tablename.Replace("KeywordMulti.", "");
                            string attachmentName = a.Filename;
                            string withoutExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[0];
                            string withExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[1];
                            withoutExt = withoutExt + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfffff");

                            string path = @"C:\Temp\DiscordBot\" + tablename + @"\" + withoutExt + "." + withExt;

                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFileAsync(new Uri(attachment.Url), path);
                            }

                            stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
                            {
                                new SqlParameter("@FilePath", path),
                                new SqlParameter("@TableName", dr["TableName"].ToString()),
                                new SqlParameter("@UserID", userId)
                            });
                        }
                    }
                }
            }

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "BigBirdBot - Added Image",
                Color = Color.Blue,
                Description = $"Added attachment(s) successfully for **{keyword}**."
            };

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        #endregion

        #region Get Keywords
        [SlashCommand("getkeywords", "List of all keywords in the server.")]
        [EnabledInDm(false)]
        public async Task HandleKeywordList()
        {
            await DeferAsync();
            string connStr = Constants.Constants.DISCORD_BOT_CONN_STR;
            long serverId = Int64.Parse(Context.Guild.Id.ToString());
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

        [SlashCommand("getmultikeywords", "List of all available multiple keyword characters/people available by the server.")]
        [EnabledInDm(false)]
        public async Task GetThirstList()
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();
            string description = "";

            DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "GetThirstMapByServerID", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString()))
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    description += "- " + dr["TableList"].ToString() + Environment.NewLine;
                }

                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Multi-Keyword List", "List of available multi-keyword tables:\n" + description, "", Context.User.Username, Discord.Color.Blue).Build());
            }
        }
        #endregion

        #region Update Keywords
        [SlashCommand("editkeyword", "Edits a keyword to the server.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordUpdate([MinLength(1), MaxLength(50)] string keyword, string action = null, Attachment attachment = null)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                if (keyword.Trim().Length > 0 && (!string.IsNullOrEmpty(action) || attachment != null))
                {
                    if (attachment != null)
                    {
                        string word = keyword.Trim();
                        string createdBy = Context.User.Username;
                        long serverId = Int64.Parse(Context.Guild.Id.ToString());

                        StoredProcedure procedure = new StoredProcedure();

                        DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "GetChatAction", new List<SqlParameter>
                        {
                            new SqlParameter("@ServerID", serverId),
                            new SqlParameter("@Message", word)
                        });

                        if (dt.Rows.Count > 0)
                        {
                            if (attachment.Size > 52428800)
                            {
                                throw new Exception("The attachment provided is too large, please create the keyword using an attachment that is 50mb or less.");
                            }
                            else
                            {
                                string path = @"C:\Temp\DiscordBot\KeywordAttachment\" + attachment.Filename;
                                using (WebClient client = new WebClient())
                                {
                                    client.DownloadFileAsync(new Uri(attachment.Url), path);
                                }

                                procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "UpdateChatAction", new List<SqlParameter>
                                {
                                    new SqlParameter("@ServerID", serverId),
                                    new SqlParameter("@Keyword", word),
                                    new SqlParameter("@Action", path)
                                });

                                string title = "BigBirdBot - Keyword Updated";
                                string desc = $"Successfully updated Keyword -> **{word}**\n Action -> Attachment added.";
                                string thumbnailUrl = "";
                                string imageUrl = "";
                                string embedCreatedBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"This keyword does not exist for this server.";
                            string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                            string imageUrl = "";
                            string createdByMsg = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
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
                                long serverId = Int64.Parse(Context.Guild.Id.ToString());

                                StoredProcedure procedure = new StoredProcedure();

                                DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "GetChatAction", new List<SqlParameter>
                                {
                                    new SqlParameter("@ServerID", serverId),
                                    new SqlParameter("@Message", word)
                                });

                                if (dt.Rows.Count > 0)
                                {
                                    procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "UpdateChatAction", new List<SqlParameter>
                                    {
                                        new SqlParameter("@ServerID", serverId),
                                        new SqlParameter("@Keyword", word),
                                        new SqlParameter("@Action", action)
                                    });

                                    string title = "BigBirdBot - Keyword Updated";
                                    string desc = $"Successfully updated Keyword -> **{word}**\n Action -> {action}";
                                    string thumbnailUrl = "";
                                    string imageUrl = "";
                                    string embedCreatedBy = "Command from: " + Context.User.Username;

                                    EmbedHelper embed = new EmbedHelper();
                                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
                                }
                                else
                                {
                                    string title = "BigBirdBot - Error";
                                    string desc = $"This keyword does not exist for this server.";
                                    string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                                    string imageUrl = "";
                                    string createdByMsg = "Command from: " + Context.User.Username;

                                    EmbedHelper embed = new EmbedHelper();
                                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                                }
                            }
                            else
                            {
                                string title = "BigBirdBot - Error";
                                string desc = $"Maximum number of characters for the action is 50 characters and the action is 2000 characters.";
                                string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                                string imageUrl = "";
                                string createdBy = "Command from: " + Context.User.Username;

                                EmbedHelper embed = new EmbedHelper();
                                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                            }
                        }
                        else
                        {
                            string title = "BigBirdBot - Error";
                            string desc = $"To add a chat keyword action, enter a word and action.";
                            string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                            string imageUrl = "";
                            string createdBy = "Command from: " + Context.User.Username;

                            EmbedHelper embed = new EmbedHelper();
                            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                        }
                    }
                }
                else if (keyword.Trim().Length > 0)
                {
                    string word = keyword.Trim();
                    string createdBy = Context.User.Username;
                    long serverId = Int64.Parse(Context.Guild.Id.ToString());

                    StoredProcedure procedure = new StoredProcedure();

                    DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "GetChatAction", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", serverId),
                        new SqlParameter("@Message", word)
                    });

                    if (dt.Rows.Count > 0)
                    {
                        procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "UpdateChatAction", new List<SqlParameter>
                        {
                            new SqlParameter("@ServerID", serverId),
                            new SqlParameter("@Keyword", word),
                            new SqlParameter("@Action", "")
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
                        string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                        string imageUrl = "";
                        string createdByMsg = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                    }
                }
                else
                {
                    string title = "BigBirdBot - Error";
                    string desc = $"To add a chat keyword action, enter a word and action.";
                    string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                    string imageUrl = "";
                    string createdBy = "Command from: " + Context.User.Username;

                    EmbedHelper embed = new EmbedHelper();
                    await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
                }
            }
            catch (Exception e)
            {
                string title = "BigBirdBot - Error";
                string desc = $"Keyword added resulted in an error.\n" + e.Message;
                string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                string imageUrl = "";
                string createdBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
            }
        }
        #endregion

        #region Delete Keywords
        [SlashCommand("disablekeywords", "Disables all keywords for the server.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordOnOff()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure procedure = new StoredProcedure();
            long serverId = Int64.Parse(Context.Guild.Id.ToString());
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "TurnAllOnOffKeywordsByServer", new List<SqlParameter>
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
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
        }

        [SlashCommand("deletekeyword", "Deletes a keyword from the server.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleKeywordDelete([MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);
            long serverId = Int64.Parse(Context.Guild.Id.ToString());

            StoredProcedure procedure = new StoredProcedure();

            DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "GetChatAction", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", serverId),
                new SqlParameter("@Message", keyword.Trim())
            });

            if (dt.Rows.Count > 0)
            {
                procedure.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "DisableChatKeyword", new List<SqlParameter>
                {
                    new SqlParameter("@ServerID", serverId),
                    new SqlParameter("@Keyword", keyword.Trim())
                });

                string title = "BigBirdBot - Keyword Disabled";
                string desc = $"Successfully disabled Keyword -> **{keyword.Trim()}**";
                string thumbnailUrl = "";
                string imageUrl = "";
                string embedCreatedBy = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
            }
            else
            {
                string title = "BigBirdBot - Error";
                string desc = $"This keyword does not exist for this server.";
                string thumbnailUrl = Constants.Constants.ERROR_IMAGE_URL;
                string imageUrl = "";
                string createdByMsg = "Command from: " + Context.User.Username;

                EmbedHelper embed = new EmbedHelper();
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdByMsg, Discord.Color.Red, imageUrl).Build(), ephemeral: true);
            }
        }

        [SlashCommand("deletemultiurl", "Deletes a multi-keyword URL with a given table and link.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstURLDelete([MinLength(1)] string url, [MinLength(1), MaxLength(50)] string chatName)
        {
            await DeferAsync(ephemeral: true);

            EmbedHelper embedHelper = new EmbedHelper();
            string tableName = chatName.Trim();
            url = url.Trim();

            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "CheckIfThirstURLExists", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", url),
                new SqlParameter("TableName", tableName)
            });

            if (dt.Rows.Count > 0)
            {
                stored.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "DeleteThirstURL", new List<SqlParameter>
                    {
                        new SqlParameter("@FilePath", url),
                        new SqlParameter("TableName", tableName)
                    });

                EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Delete Successful", $"URL {url} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            else
            {
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The URL doesn't exist in the table provided or the table doesn't exist.", Constants.Constants.ERROR_IMAGE_URL, Context.User.Username, Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }

        [SlashCommand("deletemultikeyword", "Deletes a multi-keyword that was created.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleThirstDelete([MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            StoredProcedure stored = new StoredProcedure();
            EmbedHelper embedHelper = new EmbedHelper();
            DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "CheckKeywordExistsThirstMapByServer", new List<SqlParameter>
            {
                new SqlParameter("@Keyword", keyword.Trim()),
                new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString()))
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    stored.UpdateCreate(Constants.Constants.DISCORD_BOT_CONN_STR, "DeleteThirstCommand", new List<SqlParameter>
                    {
                        new SqlParameter("@ChatKeywordID", int.Parse(dr["ChatKeywordID"].ToString())),
                        new SqlParameter("@TableName", dr["TableName"].ToString())
                    });

                    EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Delete Successful", "The multi-keyword provided was removed successfully.", "", "", Color.Blue, "");
                    await FollowupAsync(embed: embed.Build(), ephemeral: true);
                }
            }
            else
            {
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The multi-keyword entered does not exist.", Constants.Constants.ERROR_IMAGE_URL, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
        }

        [SlashCommand("deletemultievent", "Removes a user's scheduled event for a user.")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task HandleThirstEventDelete(SocketGuildUser user, string chatName = null)
        {
            await DeferAsync(ephemeral: true);
            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = new DataTable();

            if (string.IsNullOrEmpty(chatName))
            {
                dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "DeleteEventScheduledTime", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", user.Id.ToString())
                });
            }
            else
            {
                dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "DeleteEventScheduledTime", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", user.Id.ToString()),
                    new SqlParameter("@TableName", chatName.Trim())
                });
            }

            foreach (DataRow dr in dt.Rows)
            {
                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Multi-Keyword User Removed", dr["Message"].ToString(), "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
            }
        }

        [SlashCommand("deleteexcludefromkeyword", "Delete from exclude from keywords activating when typing.")]
        [EnabledInDm(false)]
        public async Task HandleDeleteExcludeFromKeyword()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.DISCORD_BOT_CONN_STR;
            string userId = Context.User.Id.ToString();
            long serverId = Int64.Parse(Context.Guild.Id.ToString());

            stored.UpdateCreate(connStr, "DeleteChatKeywordExclusion", new List<SqlParameter>
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@ServerID", serverId)
            });

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "BigBirdBot - Exclude from Keyword",
                Color = Color.Blue,
                Description = $"You are removed from being excluded on keywords."
            };

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        #endregion

        #region Misc Keyword Functions
        [SlashCommand("requeuemulti", "Requeue a keyword event if something goes wrong when sending the scheduled action.")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task RequeueThirst(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);
            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "UpdateEventScheduleTimeRequeue", new List<SqlParameter>
            {
                new SqlParameter("@UserID", user.Id.ToString())
            });

            foreach (DataRow dr in dt.Rows)
            {
                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Event Requeue", dr["Message"].ToString(), "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
            }
        }

        [SlashCommand("excludefromkeyword", "Exclude from keywords activating when typing.")]
        [EnabledInDm(false)]
        public async Task HandleExcludeFromKeyword()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.Constants.DISCORD_BOT_CONN_STR;
            string userId = Context.User.Id.ToString();
            long serverId = Int64.Parse(Context.Guild.Id.ToString());

            stored.UpdateCreate(connStr, "AddChatKeywordExclusion", new List<SqlParameter>
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@ServerID", serverId)
            });

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "BigBirdBot - Exclude from Keyword",
                Color = Color.Blue,
                Description = $"You have been excluded from keywords."
            };

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        #endregion
    }
}
