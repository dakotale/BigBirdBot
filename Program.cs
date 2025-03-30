// There is no need to implement IDisposable like before as we are
// using dependency injection, which handles calling Dispose for us.
using Discord.WebSocket;
using Discord;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Constants;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SqlClient;
using System.Data;
using System.Net;
using DiscordBot.Helper;
using KillersLibrary.Services;
using Fergun.Interactive;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Lavalink4NET.Extensions;
using Lavalink4NET;

internal class Program
{
    static DiscordSocketClient client = new DiscordSocketClient();
    internal readonly LoggingService loggingService;
    internal readonly IServiceProvider services;
    IAudioService audioService;

    public Program()
    {
        services = ConfigureServices();
        loggingService = services.GetRequiredService<LoggingService>();
        client = services.GetRequiredService<DiscordSocketClient>();

        //lavaNode = services.GetRequiredService<LavaNode>();
        loggingService.InfoAsync("Services Initialized");
    }
    static void Main(string[] args)
    {
        System.Timers.Timer eventTimer;
        eventTimer = new System.Timers.Timer(55000); // Check every 55 seconds
        eventTimer.Elapsed += OnTimedEvent;
        eventTimer.AutoReset = true;
        eventTimer.Enabled = true;
        eventTimer.Start();
        new Program().MainAsync().GetAwaiter().GetResult();
    }
    public async Task MainAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();

        _ = loggingService.InfoAsync("Starting Bot");

#if DEBUG
        await client.LoginAsync(TokenType.Bot, Constants.devBotToken);
#else
        await client.LoginAsync(TokenType.Bot, Constants.botToken);
#endif
        await client.StartAsync();

        client.ReactionAdded += HandleReactionAsync;
        client.JoinedGuild += JoinedGuild;
        client.UserJoined += UserJoined;
        client.UserLeft += UserLeft;
        client.ButtonExecuted += ButtonHandler;
        client.MessageReceived += MessageReceived;
        client.UserVoiceStateUpdated += UserVoiceStateUpdated;
        client.Log += LogMessage;

        await client.SetGameAsync("/reportbug");

        await Task.Delay(Timeout.Infinite);
    }

    #region DiscordSocketClient Events
    private async Task UserLeft(SocketGuild arg1, SocketUser arg2)
    {
        string title = "BigBirdBot - User Left";
        string desc = $"{arg2.Username} left the server.";
        string thumbnailUrl = arg2.GetAvatarUrl(ImageFormat.Png, 256);
        string createdBy = "BigBirdBot";
        string imageUrl = "";
        StoredProcedure stored = new StoredProcedure();

        if (!arg2.IsBot && !arg2.IsWebhook)
        {
            stored.UpdateCreate(Constants.discordBotConnStr, "DeleteUser", new List<SqlParameter>
            {
                new SqlParameter("@UserID", arg2.Id.ToString()),
                new SqlParameter("@ServerID", arg1.Id.ToString())
            });

            // Let's pull the first channel and hope for the best.....
            if (arg1.DefaultChannel != null)
            {
                var textChannels = arg1.DefaultChannel.Id;
                var firstTextChannel = arg1.GetTextChannel(textChannels);
                var channel = client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

                EmbedHelper embed = new EmbedHelper();
                if (channel != null && !arg2.IsBot)
                    await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
            }
            else
            {
                var textChannels = arg1.TextChannels.Where(s => s.Name.Contains("general") || s.Name.Contains("no-mic")).ToList();
                var firstTextChannel = arg1.GetTextChannel(textChannels[0].Id);
                var channel = client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

                EmbedHelper embed = new EmbedHelper();
                if (channel != null && !arg2.IsBot)
                    await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
            }
        }
    }
    private async Task UserJoined(SocketGuildUser arg)
    {
        StoredProcedure stored = new StoredProcedure();

        if (!arg.IsBot && !arg.IsWebhook)
        {
            stored.UpdateCreate(Constants.discordBotConnStr, "AddUser", new List<SqlParameter>
            {
                new SqlParameter("@UserID", arg.Id.ToString()),
                new SqlParameter("@Username", arg.Username),
                new SqlParameter("@JoinDate", arg.JoinedAt),
                new SqlParameter("@ServerUID", Int64.Parse(arg.Guild.Id.ToString())),
                new SqlParameter("@Nickname", arg.Nickname)
            });

            stored.UpdateCreate(Constants.discordBotConnStr, "AddUserByServer", new List<SqlParameter>
            {
                new SqlParameter("@UserID", arg.Id.ToString()),
                new SqlParameter("@Username", arg.Username),
                new SqlParameter("@JoinDate", arg.JoinedAt),
                new SqlParameter("@ServerUID", Int64.Parse(arg.Guild.Id.ToString())),
                new SqlParameter("@Nickname", arg.Nickname)
            });

            DataTable dt = stored.Select(Constants.discordBotConnStr, "CheckIfShowWelcomeMessage", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", Int64.Parse(arg.Guild.Id.ToString()))
            });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    if (bool.Parse(dr["ShowWelcomeMessage"].ToString()) == true)
                    {
                        string title = "BigBirdBot - Introductions";
                        string desc = $"Everyone welcome {arg.Mention} to the server!";
                        string thumbnailUrl = arg.GetAvatarUrl(ImageFormat.Png, 256);
                        string createdBy = "BigBirdBot";
                        string imageUrl = "";

                        // Let's pull the first channel and hope for the best.....
                        List<SocketTextChannel> textChannels = new List<SocketTextChannel>();
                        textChannels = arg.Guild.TextChannels.Where(s => s.Name.Contains("general") || s.Name.Contains("no-mic")).ToList();
                        SocketTextChannel? firstTextChannel;

                        if (textChannels.Count > 0)
                            firstTextChannel = arg.Guild.GetTextChannel(textChannels[0].Id) ?? arg.Guild.GetTextChannel(textChannels[1].Id);
                        else
                            firstTextChannel = arg.Guild.DefaultChannel;

                        if (firstTextChannel != null)
                        {
                            var channel = client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

                            EmbedHelper embed = new EmbedHelper();
                            if (channel != null && !arg.IsBot)
                                await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
                        }

                        string userId = arg.Id.ToString();
                        var getUser = await client.GetUserAsync(ulong.Parse(userId));
                        var dmChannel = await getUser.CreateDMChannelAsync();

                        EmbedHelper helper = new EmbedHelper();
                        string output = "Welcome to the server, I'm BigBirdBot!\nI wanted to give you a proper welcome and hope you have a great time!\nFeel free to type -help in the server to get more information on what I can offer!";

                        if (dmChannel != null)
                            await dmChannel.SendMessageAsync(embed: helper.BuildMessageEmbed("BigBirdBot - Help Commands", output, "", "BigBirdBot", Discord.Color.Gold, null, null).Build());
                    }
                }
            }
        }
    }
    private async Task ButtonHandler(SocketMessageComponent component)
    {
        // To prevent the Queue buttons from being picked up by the Handler
        if (!component.Data.CustomId.Contains("_"))
        {
            StoredProcedure stored = new StoredProcedure();
            string connStr = Constants.discordBotConnStr;
            DataTable dt = new DataTable();
            EmbedHelper embed = new EmbedHelper();

            var customId = component.Data.CustomId;
            var guildId = component.GuildId.Value.ToString() ?? "";
            // Need to check if it's a role, if not default to a pronoun for now
            dt = stored.Select(connStr, "GetRolesByID", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(guildId)),
                new SqlParameter("@RoleID", Int64.Parse(customId))
            });

            // It's a role
            if (dt.Rows.Count > 0)
            {
                dt = stored.Select(connStr, "GetRoleUsersByID", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                    new SqlParameter("@RoleID", Int64.Parse(customId))
                });

                DataTable dtRoles = new DataTable();
                dtRoles = stored.Select(connStr, "GetRoles", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(guildId)) });

                // They have the role and are deleting the role
                if (dt.Rows.Count > 0)
                {
                    string roleIdSelected = "";
                    string roleNameSelected = "";
                    foreach (DataRow dr in dtRoles.Rows)
                    {
                        string roleId = dr["RoleID"].ToString();
                        string roleName = dr["RoleName"].ToString();

                        if (roleId.Equals(component.Data.CustomId.ToString()))
                        {
                            roleIdSelected = roleId;
                            roleNameSelected = roleName;
                        }
                    }

                    var role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Id.ToString().Equals(roleIdSelected));

                    var guild = client.GetGuild(component.GuildId.Value);
                    var guildUser = guild.GetUser(component.User.Id);

                    await (guildUser as IGuildUser).RemoveRoleAsync(role);

                    // Remove the Pronoun from the table
                    stored.UpdateCreate(connStr, "DeleteRoleUsers", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                        new SqlParameter("@RoleID", Int64.Parse(component.Data.CustomId)),
                        new SqlParameter("@ServerID", Int64.Parse(component.GuildId.Value.ToString()))
                    });
                    await component.RespondAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", $"Role was successfully removed for {component.User.Username}", "", component.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
                }
                // They don't have the role and now are going to delete it
                else
                {
                    string roleIdSelected = "";
                    string roleNameSelected = "";
                    foreach (DataRow dr in dtRoles.Rows)
                    {
                        string roleId = dr["RoleID"].ToString();
                        string roleName = dr["RoleName"].ToString();

                        if (roleId.Equals(component.Data.CustomId.ToString()))
                        {
                            roleIdSelected = roleId;
                            roleNameSelected = roleName;
                        }
                    }

                    var role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Id.ToString().Equals(roleIdSelected));

                    var guild = client.GetGuild(component.GuildId.Value);
                    var guildUser = guild.GetUser(component.User.Id);

                    await (guildUser as IGuildUser).AddRoleAsync(role);

                    // Remove the Pronoun from the table
                    stored.UpdateCreate(connStr, "AddRoleUsers", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                        new SqlParameter("@RoleID", Int64.Parse(component.Data.CustomId)),
                        new SqlParameter("@ServerID", Int64.Parse(component.GuildId.Value.ToString()))
                    });

                    await component.RespondAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Role Selection", $"Role was successfully added for {component.User.Username}", "", component.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
                }
            }
            else
            {
                dt = stored.Select(connStr, "GetPronounUsersByID", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                    new SqlParameter("@PronounID", int.Parse(component.Data.CustomId))
                });

                DataTable dtPronouns = new DataTable();
                dtPronouns = stored.Select(connStr, "GetPronouns", new List<SqlParameter>());

                if (dt.Rows.Count > 0)
                {
                    string pronounSelected = "";
                    // Remove them from the role
                    foreach (DataRow dr in dtPronouns.Rows)
                    {
                        int pronounId = int.Parse(dr["ID"].ToString());
                        string pronounName = dr["Pronoun"].ToString();

                        if (client.GetGuild(component.GuildId.Value).Roles.Where(s => s.Name.Equals(pronounName)).Count() < 1)
                        {
                            // Create the role
                            await client.GetGuild(component.GuildId.Value).CreateRoleAsync(pronounName);
                        }

                        if (pronounId.ToString() == component.Data.CustomId)
                            pronounSelected = pronounName;
                    }

                    var role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Name.Equals(pronounSelected));

                    var guild = client.GetGuild(component.GuildId.Value);
                    var guildUser = guild.GetUser(component.User.Id);

                    await (guildUser as IGuildUser).RemoveRoleAsync(role);

                    // Remove the Pronoun from the table
                    stored.UpdateCreate(connStr, "DeletePronounUsers", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                        new SqlParameter("@PronounID", int.Parse(component.Data.CustomId))
                    });

                    await component.RespondAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Pronoun Selection", $"Pronouns were successfully removed for {component.User.Username}", "", component.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
                }
                else
                {
                    string pronounSelected = "";

                    foreach (DataRow dr in dtPronouns.Rows)
                    {
                        int pronounId = int.Parse(dr["ID"].ToString());
                        string pronounName = dr["Pronoun"].ToString();

                        if (client.GetGuild(component.GuildId.Value).Roles.Where(s => s.Name.Equals(pronounName)).Count() < 1)
                        {
                            // Create the role
                            await client.GetGuild(component.GuildId.Value).CreateRoleAsync(pronounName);
                        }

                        if (pronounId.ToString() == component.Data.CustomId)
                            pronounSelected = pronounName;
                    }

                    // Add them to the role
                    var role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Name.Equals(pronounSelected));

                    var guild = client.GetGuild(component.GuildId.Value);
                    var guildUser = guild.GetUser(component.User.Id);

                    await (guildUser as IGuildUser).AddRoleAsync(role);

                    // Add Pronoun for User
                    stored.UpdateCreate(connStr, "AddPronounUsers", new List<SqlParameter>
                    {
                        new SqlParameter("@UserID", Int64.Parse(component.User.Id.ToString())),
                        new SqlParameter("@PronounID", int.Parse(component.Data.CustomId))
                    });
                    await component.RespondAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Pronoun Selection", $"Pronouns were successfully added for {component.User.Username}.", "", component.User.Username, Discord.Color.Blue).Build(), ephemeral: true);
                }
            }
        }

    }
    private async Task JoinedGuild(SocketGuild arg)
    {
        StoredProcedure stored = new StoredProcedure();
        EmbedHelper embedHelper = new EmbedHelper();

        DataTable dt = stored.Select(Constants.discordBotConnStr, "GetServers", new List<SqlParameter>());
        List<string> serverIds = new List<string>();
        foreach (DataRow dr in dt.Rows)
        {
            serverIds.Add(dr["ServerUID"].ToString());
        }

        if (serverIds.Where(s => s.Equals(arg.Id.ToString())).Count() == 0)
        {
            stored.UpdateCreate(Constants.discordBotConnStr, "AddServer", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", Int64.Parse(arg.Id.ToString())),
                new SqlParameter("@ServerName", arg.Name),
                new SqlParameter("@DefaultChannelID", Int64.Parse(arg.DefaultChannel.Id.ToString())),
            });
        }

        using (SqlConnection conn = new SqlConnection(Constants.discordBotConnStr))
        {
            await arg.DownloadUsersAsync().ConfigureAwait(false);
            if (arg.Users.Count > 0)
            {
                foreach (var user in arg.Users)
                {
                    if (!user.IsBot && !user.IsWebhook)
                    {
                        stored.UpdateCreate(Constants.discordBotConnStr, "AddUser", new List<SqlParameter>
                        {
                            new SqlParameter("@UserID", user.Id.ToString()),
                            new SqlParameter("@Username", user.Username),
                            new SqlParameter("@JoinDate", user.JoinedAt),
                            new SqlParameter("@ServerUID", Int64.Parse(arg.Id.ToString())),
                            new SqlParameter("@Nickname", user.Nickname)
                        });
                    }
                }
                Console.WriteLine($"{arg.Users.Count} users were added successfully for {arg.Name}");
            }
            else
            {
                ulong guildId = ulong.Parse("880569055856185354");
                ulong textChannelId = ulong.Parse("1156625507840954369");
                await client.GetGuild(guildId).GetTextChannel(textChannelId).SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - New Server Added", $"Bot was added to {arg.Name} and no users were found on DownloadUsersAsync call.\nThe owner is {arg.Owner}", "", "BigBirdBot", Discord.Color.Red, null, null).Build()).ConfigureAwait(false);
            }
        }
    }
    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg != null && !msg.Author.IsBot && !msg.Author.IsWebhook && msg.Channel as SocketGuildChannel != null)
        {
            string message = msg.Content.Trim().ToLower();
            string connStr = Constants.discordBotConnStr;
            var msgChannel = msg.Channel as SocketGuildChannel;
            var serverId = msgChannel.Guild.Id.ToString();
            bool isActive = false;
            bool isServerActive = false;
            int totalActive = 0;
            StoredProcedure stored = new StoredProcedure();
            URLCleanup cleanup = new URLCleanup();

            DataTable serverActive = stored.Select(connStr, "GetServerPrefixByServerID", new List<SqlParameter>
            {
                new SqlParameter("ServerUID", Int64.Parse(serverId))
            });

            foreach (DataRow dr in serverActive.Rows)
            {
                isServerActive = bool.Parse(dr["IsActive"].ToString());
            }

            if (isServerActive)
            {
                DataTable dtActive = stored.Select(connStr, "CheckIfKeywordsAreActivePerServer", new List<SqlParameter>
                {
                    new SqlParameter("ServerUID", Int64.Parse(serverId))
                });

                if (dtActive.Rows.Count > 0)
                {
                    foreach (DataRow row in dtActive.Rows)
                    {
                        totalActive = int.Parse(row["TotalActive"].ToString());
                        if (totalActive > 0)
                            isActive = true;
                    }
                }

                if (isActive)
                {
                    string prefix = "";
                    DataTable dtPrefix = stored.Select(Constants.discordBotConnStr, "GetServerPrefixByServerID", new List<SqlParameter> { new SqlParameter("@ServerUID", Int64.Parse(serverId)) });
                    foreach (DataRow dr in dtPrefix.Rows)
                    {
                        prefix = dr["Prefix"].ToString();
                    }

                    // This should be okay
                    if ((message.Contains("https://twitter.com") || message.Contains("https://x.com") || message.Contains("https://tiktok.com") || message.Contains("https://instagram.com") || message.Contains("https://www.instagram.com")) && !message.Contains(prefix))
                    {
                        DataTable dtTwitter = stored.Select(connStr, "GetTwitterBroken", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(serverId)) });
                        bool isTwitterBroken = false;
                        foreach (DataRow dr in dtTwitter.Rows)
                            isTwitterBroken = bool.Parse(dr["TwitterBroken"].ToString());

                        if (isTwitterBroken)
                            await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));
                    }
                    else
                    {
                        if (message.StartsWith(prefix))
                        {
                            string keyword = "";
                            if (message.Split(' ').Count() == 1)
                                keyword = message.Replace(prefix, "");
                            if (message.Split(' ').Count() > 1)
                                keyword = message.Split(' ')[0].Replace(prefix, "");

                            // Check if it's in the ThirstMap and run the add command
                            List<SqlParameter> parameters = new List<SqlParameter>();
                            parameters.Add(new SqlParameter("@AddKeyword", keyword));

                            DataTable dt = stored.Select(connStr, "GetThirstTableByMap", parameters);
                            if (dt.Rows.Count > 0)
                            {
                                if (msg.Attachments.Count > 0)
                                {
                                    string userId = msg.Author.Id.ToString();
                                    foreach (DataRow dr in dt.Rows)
                                    {
                                        var attachments = msg.Attachments;
                                        foreach (var attachment in attachments)
                                        {
                                            string tablename = dr["TableName"].ToString();
                                            tablename = tablename.Replace("KeywordMulti.", "");
                                            string attachmentName = attachment.Filename;
                                            string withoutExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[0];
                                            string withExt = attachmentName.Split(".", StringSplitOptions.TrimEntries)[1];
                                            withoutExt = withoutExt + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfffff");

                                            string path = @"C:\Temp\DiscordBot\" + tablename + @"\" + withoutExt + "." + withExt;

                                            using (WebClient client = new WebClient())
                                            {
                                                client.DownloadFileAsync(new Uri(attachment.Url), path);
                                            }

                                            stored.UpdateCreate(connStr, "AddThirstByMap", new List<System.Data.SqlClient.SqlParameter>
                                            {
                                                new SqlParameter("@FilePath", path),
                                                new SqlParameter("@TableName", dr["TableName"].ToString()),
                                                new SqlParameter("@UserID", userId)
                                            });
                                        }
                                        var embed = new EmbedBuilder
                                        {
                                            Title = "BigBirdBot - Added Image",
                                            Color = Color.Blue,
                                            Description = "Added attachment(s) successfully."
                                        };

                                        await msg.Channel.SendMessageAsync(embed: embed.Build());
                                    }
                                }
                                if (message.Split(' ').Count() > 1)
                                {
                                    string content = message.Replace("-" + keyword, "").Trim();
                                    bool multiUrl = false;

                                    if (content.Contains(",") && content.Contains("http"))
                                        multiUrl = true;

                                    if (multiUrl)
                                    {
                                        string[] urls = content.Split(",", StringSplitOptions.TrimEntries);
                                        foreach (var u in urls)
                                        {
                                            bool result = u.Trim().StartsWith("http");

                                            if (!result)
                                            {
                                                var embed = new EmbedBuilder
                                                {
                                                    Title = "BigBirdBot - Error",
                                                    Color = Color.Red,
                                                    Description = $"The URL provided (*{u}*) for this command is invalid."
                                                }.WithCurrentTimestamp();

                                                await msg.Channel.SendMessageAsync(embed: embed.Build());
                                            }
                                            else
                                            {
                                                content = cleanup.CleanURLEmbed(u);

                                                // Check if link exists for thirst table
                                                DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                                {
                                                    new SqlParameter("@FilePath", content),
                                                    new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                                });

                                                if (dtExists.Rows.Count > 0)
                                                {
                                                    var embed = new EmbedBuilder
                                                    {
                                                        Title = "BigBirdBot - Error",
                                                        Color = Color.Red,
                                                        Description = $"The URL provided (*{content}*) was already added for this Multi-Keyword Command."
                                                    }.WithCurrentTimestamp();

                                                    await msg.Channel.SendMessageAsync(embed: embed.Build());
                                                }
                                                else
                                                {
                                                    string userId = msg.Author.Id.ToString();
                                                    foreach (DataRow dr in dt.Rows)
                                                    {
                                                        stored.UpdateCreate(connStr, "AddThirstByMap", new List<System.Data.SqlClient.SqlParameter>
                                                            {
                                                                new SqlParameter("@FilePath", content),
                                                                new SqlParameter("@TableName", dr["TableName"].ToString()),
                                                                new SqlParameter("@UserID", userId)
                                                            });
                                                    }
                                                }
                                            }
                                        }
                                        var embedSuccess = new EmbedBuilder
                                        {
                                            Title = "BigBirdBot - Added Image",
                                            Color = Color.Blue,
                                            Description = "Added link(s) successfully."
                                        };

                                        await msg.Channel.SendMessageAsync(embed: embedSuccess.Build());
                                    }
                                    else
                                    {
                                        content = cleanup.CleanURLEmbed(message);

                                        // Check if link exists for thirst table
                                        DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                        {
                                            new SqlParameter("@FilePath", content),
                                            new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                        });

                                        if (dtExists.Rows.Count > 0)
                                        {
                                            var embed = new EmbedBuilder
                                            {
                                                Title = "BigBirdBot - Error",
                                                Color = Color.Red,
                                                Description = $"The URL provided was already added for this Multi-Keyword Command."
                                            }.WithCurrentTimestamp();

                                            await msg.Channel.SendMessageAsync(embed: embed.Build());
                                        }
                                        else
                                        {
                                            string userId = msg.Author.Id.ToString();
                                            foreach (DataRow dr in dt.Rows)
                                            {
                                                stored.UpdateCreate(connStr, "AddThirstByMap", new List<System.Data.SqlClient.SqlParameter>
                                                {
                                                    new SqlParameter("@FilePath", content),
                                                    new SqlParameter("@TableName", dr["TableName"].ToString()),
                                                    new SqlParameter("@UserID", userId)
                                                });

                                                var embed = new EmbedBuilder
                                                {
                                                    Title = "BigBirdBot - Added Image",
                                                    Color = Color.Blue,
                                                    Description = "Added link(s) successfully."
                                                };

                                                await msg.Channel.SendMessageAsync(embed: embed.Build());
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Todo, check all the commands eventually but for now let's stop the accidently double triggering.
                        if (!message.StartsWith(prefix))
                        {
                            var channel = msg.Channel as SocketGuildChannel;
                            StoredProcedure storedProcedure = new StoredProcedure();
                            List<SqlParameter> parameters = new List<SqlParameter>();
                            parameters.Add(new SqlParameter("@UserID", msg.Author.Id.ToString()));
                            parameters.Add(new SqlParameter("@ServerID", Int64.Parse(channel.Guild.Id.ToString())));
                            DataTable dt = storedProcedure.Select(connStr, "GetChatKeywordExclusion", parameters);

                            if (dt.Rows.Count == 0)
                            {
                                parameters = new List<SqlParameter>();
                                parameters.Add(new SqlParameter("@ServerID", Int64.Parse(channel.Guild.Id.ToString())));
                                parameters.Add(new SqlParameter("@Message", message));
                                dt = storedProcedure.Select(connStr, "GetChatAction", parameters);

                                var sender = client.GetChannel(channel.Id) as IMessageChannel;

                                _ = Task.Run(async () =>
                                {
                                    if (dt.Rows.Count > 0 && sender != null)
                                    {
                                        foreach (DataRow dr in dt.Rows)
                                        {
                                            string chatAction = dr["ChatAction"].ToString();

                                            if (!string.IsNullOrEmpty(chatAction))
                                            {
                                                await msg.Channel.TriggerTypingAsync(new RequestOptions { Timeout = 30 });
                                                if (chatAction.Contains("C:\\"))
                                                    await msg.Channel.SendFileAsync(dr["ChatAction"].ToString()).ConfigureAwait(false);
                                                else
                                                    await sender.SendMessageAsync(dr["ChatAction"].ToString()).ConfigureAwait(false);

                                                parameters.Clear();
                                                parameters.Add(new SqlParameter("@ChatKeywordID", int.Parse(dr["ChatKeywordID"].ToString())));
                                                parameters.Add(new SqlParameter("@MessageText", message));
                                                parameters.Add(new SqlParameter("@CreatedBy", msg.Author.Id.ToString()));
                                                parameters.Add(new SqlParameter("@ServerID", Int64.Parse(serverId)));
                                                storedProcedure.UpdateCreate(connStr, "AddAuditKeyword", parameters);
                                            }
                                        }
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }
    }

    private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user.IsBot && after.VoiceChannel == null)
        {
            foreach (var u in before.VoiceChannel.ConnectedUsers)
                if (u.IsBot)
                    await u.VoiceChannel.DisconnectAsync();

            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(before.VoiceChannel.Guild.Id.ToString()))
            });
        }

        if (!user.IsBot)
        {
            // This should be the voice channel
            // Commenting out the last part to handle moves or disconnects
            if (before.VoiceChannel != null && before.VoiceChannel.ConnectedUsers.Where(s => !s.IsBot).ToList().Count == 0 && after.VoiceChannel == null) //&& after.VoiceChannel == null)
            {
                foreach (var u in before.VoiceChannel.ConnectedUsers)
                    if (u.IsBot)
                        await u.VoiceChannel.DisconnectAsync();

                StoredProcedure stored = new StoredProcedure();
                stored.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter>
                {
                    new SqlParameter("@ServerID", Int64.Parse(before.VoiceChannel.Guild.Id.ToString()))
                });
            }
        }
    }

    private async Task LogMessage(LogMessage message)
    {
        EmbedHelper embedHelper = new EmbedHelper();
        // Send an error to the specific server and channel
        ulong guildId = ulong.Parse("880569055856185354");
        ulong textChannelId = ulong.Parse("1156625507840954369");

        if (message.Exception != null)
        {
            string exception = message.Exception.Message;

            if (client.GetGuild(guildId) != null)
            {
                if (client.GetGuild(guildId).GetTextChannel(textChannelId) != null && message.Message.Length > 0)
                {
                    await client.GetGuild(guildId).GetTextChannel(textChannelId).SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Exception Thrown", $"Exception: {exception}\nMessage: {message.Message}", "", "BigBirdBot", Discord.Color.Red, null, null).Build());
                }
            }
        }
    }
    #endregion

    #region Services Configuration
    private ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton<CommandService>()
            .AddSingleton<HttpClient>()
            .AddSingleton<LoggingService>()
            .AddSingleton<InteractionHandlerService>()
            .AddSingleton<InteractionService>(p => new InteractionService(p.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                LogGatewayIntentWarnings = false,
                AlwaysDownloadUsers = true,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LogLevel = LogSeverity.Warning
            })
            .AddSingleton<EmbedPagesService>()
            .AddSingleton<MultiButtonsService>()
            .AddSingleton<InteractiveService>()
            .AddSingleton(new InteractiveConfig { DefaultTimeout = TimeSpan.FromMinutes(15), LogLevel = LogSeverity.Warning })
            .AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.BaseAddress = new Uri(Constants.lavalinkUrl);
                x.Passphrase = Constants.lavaLinkPwd;
                x.BufferSize = 2048;
                x.Label = "BigBirdBot";
                x.ReadyTimeout = TimeSpan.FromMinutes(15);
                x.ResumptionOptions = new(TimeSpan.Zero);
            })
            .AddLogging(x =>
            {
                x.ClearProviders();
                x.SetMinimumLevel(LogLevel.Trace);
            })
            .BuildServiceProvider();
    }
    #endregion

    #region Emojis and Timed Events
    private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        Emoji triviaA = new Emoji("🇦");
        Emoji triviaB = new Emoji("🇧");
        Emoji triviaC = new Emoji("🇨");
        Emoji triviaD = new Emoji("🇩");

        var embed = message.GetOrDownloadAsync().Result.Embeds;
        StoredProcedure stored = new StoredProcedure();
        if (client.GetUser(reaction.UserId).IsBot) return;

        if (reaction.Emote.Name == triviaA.Name || reaction.Emote.Name == triviaB.Name || reaction.Emote.Name == triviaC.Name || reaction.Emote.Name == triviaD.Name)
        {
            string connStr = Constants.discordBotConnStr;
            try
            {
                if (embed.Count > 0)
                {
                    var messageId = Int64.Parse(message.Id.ToString());
                    DataTable dt = stored.Select(Constants.discordBotConnStr, "GetTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                    var theReaction = reaction.Emote.Name;
                    if (dt.Rows.Count > 0)
                    {
                        string correctAnswer = "";
                        foreach (DataRow dr in dt.Rows)
                        {
                            correctAnswer = dr["CorrectAnswer"].ToString();
                        }

                        foreach (var e in embed)
                        {
                            foreach (var f in e.Fields.Where(s => s.Name.Contains(".")).ToList())
                            {
                                if (correctAnswer.Equals(f.Value))
                                {
                                    EmbedHelper embedHelper = new EmbedHelper();
                                    // We have the correct answer, so let's get the reaction that it equals
                                    if (f.Name == "A." && theReaction.Equals(triviaA.Name))
                                    {
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "B." && theReaction.Equals(triviaB.Name))
                                    {
                                        // Check if the reaction was 'B'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "C." && theReaction.Equals(triviaC.Name))
                                    {
                                        // Check if the reaction was 'C'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "D." && theReaction.Equals(triviaD.Name))
                                    {
                                        // Check if the reaction was 'D'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else
                                    {
                                        // Ya wrong
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Wrong", $"{reaction.User.Value.Mention} you didn't answer correctly, try again!", "", "BigBirdBot", Discord.Color.Red, null, null).Build());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EmbedHelper errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", ex.Message, Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
    }

    // When the timer is kicked off, call this function to pull the EventText or ReminderText from SPs.
    private static async void OnTimedEvent(object sender, EventArgs e)
    {
        StoredProcedure storedProcedure = new StoredProcedure();
        string connStr = Constants.discordBotConnStr;
        DataTable dt = new DataTable();

        // 1. Check Event falls into specific timeframe
        // 2. Send ping to person in the same channel they sent the event in
        // 3. Repeat
        dt = storedProcedure.Select(connStr, "GetEvent", new List<SqlParameter> { });
        if (dt.Rows.Count > 0)
        {
            foreach (DataRow dr in dt.Rows)
            {
                IMessageChannel channel = (IMessageChannel)client.GetChannel(ulong.Parse(dr["EventChannelSource"].ToString()));
                storedProcedure.UpdateCreate(Constants.discordBotConnStr, "DeleteEvent", new List<SqlParameter> { new SqlParameter("@EventID", dr["EventID"].ToString()) });

                await channel.SendMessageAsync(dr["EventText"].ToString());
            }
        }
        else
        {
            dt = storedProcedure.Select(connStr, "GetEventReminder", new List<SqlParameter> { });
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    IMessageChannel channel = (IMessageChannel)client.GetChannel(ulong.Parse(dr["EventChannelSource"].ToString()));
                    await channel.SendMessageAsync(dr["EventReminderText"].ToString());
                }
            }
        }

        dt = storedProcedure.Select(connStr, "GetEventScheduledTime", new List<SqlParameter>());
        if (dt.Rows.Count > 0)
        {
            EmbedHelper embed = new EmbedHelper();
            foreach (DataRow dr in dt.Rows)
            {
                string userId = dr["UserID"].ToString();
                string filePath = dr["FilePath"].ToString();
                string tableName = dr["ThirstTable"].ToString();
                tableName = string.Concat(tableName[0].ToString().ToUpper(), tableName.AsSpan(1));

                // Send the DM :)
                var user = await client.GetUserAsync(ulong.Parse(userId));

                if (dr["FilePath"].ToString().Contains("C:\\"))
                    await user.SendFileAsync(filePath, $"**{tableName} - {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET")}**");
                else
                    await user.SendMessageAsync($"**{tableName} - {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET")}**\n**URL:** {filePath}");

                storedProcedure.UpdateCreate(connStr, "AddUsersThirstTableLog", new List<SqlParameter>
                {
                    new SqlParameter("@UserID", userId),
                    new SqlParameter("@FilePath", filePath)
                });
            }
        }

    }
    #endregion
}

