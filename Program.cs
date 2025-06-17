// There is no need to implement IDisposable like before as we are
// using dependency injection, which handles calling Dispose for us.
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Services;
using Fergun.Interactive;
using KillersLibrary.Services;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static DiscordSocketClient client = new DiscordSocketClient();
    internal readonly LoggingService loggingService;
    internal readonly IServiceProvider services;
    private readonly IAudioService audioService;

    public Program()
    {
        services = ConfigureServices();
        loggingService = services.GetRequiredService<LoggingService>();
        client = services.GetRequiredService<DiscordSocketClient>();

        //lavaNode = services.GetRequiredService<LavaNode>();
        loggingService.InfoAsync("Services Initialized");
    }
    private static void Main(string[] args)
    {
        System.Timers.Timer eventTimer;
        eventTimer = new System.Timers.Timer(59000); // Check every 55 seconds
        eventTimer.Elapsed += OnTimedEvent;
        eventTimer.AutoReset = true;
        eventTimer.Enabled = true;
        eventTimer.Start();
        new Program().MainAsync().GetAwaiter().GetResult();
    }
    public async Task MainAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        await RunBot(client);
    }

    public async Task RunBot(DiscordSocketClient client)
    {
        try
        {
            _ = loggingService.InfoAsync("Starting Bot");

#if DEBUG
            await client.LoginAsync(TokenType.Bot, Constants.devBotToken);
#else
            await client.LoginAsync(TokenType.Bot, Constants.botToken);
#endif

            client.Disconnected += async (e) =>
            {
                await loggingService.InfoAsync("Bot disconnected");

                if (client.ConnectionState == Discord.ConnectionState.Connecting)
                    await loggingService.InfoAsync("Bot connecting");
            };

            client.Connected += async () =>
            {
                await loggingService.InfoAsync("Bot connected");
                await client.SetGameAsync("/reportbug");
            };

            if (client.ConnectionState != Discord.ConnectionState.Connected && client.ConnectionState != Discord.ConnectionState.Connecting)
                await client.StartAsync();

            client.ReactionAdded += HandleReactionAsync;
            client.JoinedGuild += JoinedGuild;
            client.UserJoined += UserJoined;
            client.UserLeft += UserLeft;
            client.ButtonExecuted += ButtonHandler;
            client.MessageReceived += MessageReceived;
            client.UserVoiceStateUpdated += UserVoiceStateUpdated;
            client.Log += LogMessage;

            await Task.Delay(Timeout.Infinite);
        }
        catch (WebSocketException wse)
        {
            await loggingService.InfoAsync("WebSocketException: " + wse.Message);
            await client.LogoutAsync();
            client = new DiscordSocketClient();
            client = services.GetRequiredService<DiscordSocketClient>();
            await RunBot(client);
        }
        catch (WebSocketClosedException wsce)
        {
            await loggingService.InfoAsync("WebSocketClosedException: " + wsce.Message);
            await client.LogoutAsync();
            client = new DiscordSocketClient();
            client = services.GetRequiredService<DiscordSocketClient>();
            await RunBot(client);
        }
        catch (GatewayReconnectException gre)
        {
            await loggingService.InfoAsync("GatewayReconnectException: " + gre.Message);
            await client.LogoutAsync();
            client = new DiscordSocketClient();
            client = services.GetRequiredService<DiscordSocketClient>();
            await RunBot(client);
        }
        catch (Exception ex)
        {
            await loggingService.InfoAsync("Exception: " + ex.Message);
            await client.LogoutAsync();
            client = new DiscordSocketClient();
            client = services.GetRequiredService<DiscordSocketClient>();
            await RunBot(client);
        }
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
                ulong textChannels = arg1.DefaultChannel.Id;
                SocketTextChannel firstTextChannel = arg1.GetTextChannel(textChannels);
                SocketTextChannel? channel = client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

                EmbedHelper embed = new EmbedHelper();
                if (channel != null && !arg2.IsBot)
                    await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
            }
            else
            {
                List<SocketTextChannel> textChannels = arg1.TextChannels.Where(s => s.Name.Contains("general") || s.Name.Contains("no-mic")).ToList();
                SocketTextChannel firstTextChannel = arg1.GetTextChannel(textChannels[0].Id);
                SocketTextChannel? channel = client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

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

            string customId = component.Data.CustomId;
            string guildId = component.GuildId.Value.ToString() ?? "";
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

                    SocketRole? role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Id.ToString().Equals(roleIdSelected));

                    SocketGuild guild = client.GetGuild(component.GuildId.Value);
                    SocketGuildUser guildUser = guild.GetUser(component.User.Id);

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

                    SocketRole? role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Id.ToString().Equals(roleIdSelected));

                    SocketGuild guild = client.GetGuild(component.GuildId.Value);
                    SocketGuildUser guildUser = guild.GetUser(component.User.Id);

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

                    SocketRole? role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Name.Equals(pronounSelected));

                    SocketGuild guild = client.GetGuild(component.GuildId.Value);
                    SocketGuildUser guildUser = guild.GetUser(component.User.Id);

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
                    SocketRole? role = client.GetGuild(component.GuildId.Value).Roles.FirstOrDefault(s => s.Name.Equals(pronounSelected));

                    SocketGuild guild = client.GetGuild(component.GuildId.Value);
                    SocketGuildUser guildUser = guild.GetUser(component.User.Id);

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
                foreach (SocketGuildUser? user in arg.Users)
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
        if (msg != null && !msg.Author.IsBot && !msg.Author.IsWebhook && (msg.Channel as SocketGuildChannel) != null)
        {
            string message = msg.Content.Trim().ToLower();
            string connStr = Constants.discordBotConnStr;
            SocketGuildChannel? msgChannel = msg.Channel as SocketGuildChannel;
            string serverId = msgChannel.Guild.Id.ToString();
            bool isActive = false;
            bool isServerActive = false;
            int totalActive = 0;
            StoredProcedure stored = new StoredProcedure();
            URLCleanup cleanup = new URLCleanup();

            // If the valorant role is mentioned, auto increment the counter
            if (msg.MentionedRoleIds.Count > 0 && msg.MentionedRoleIds.Where(s => s.ToString().Equals("1313502437201543168")).Count() > 0)
            {
                EmbedHelper embed = new EmbedHelper();
                DataTable dt = new DataTable();
                string userId = msg.Author.Id.ToString();

                dt = stored.Select(connStr, "AddToCounter", new List<SqlParameter>
                {
                    new SqlParameter("@CreatedBy", userId),
                    new SqlParameter("@Counter", 1),
                    new SqlParameter("@CounterName", "Valorant")
                });

                string counterHistory = "Here are the most recent times *people were asked to play Valorant*.\n";
                string currentCounter = "";
                string currentDateTime = "";
                string averagePerDay = "";

                foreach (DataRow dr in dt.Rows)
                {
                    currentCounter = dr["CurrentCounter"].ToString();
                    currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy hh:mm tt ET");
                    counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy hh:mm tt ET")}\n";
                    averagePerDay = dr["AveragePerDay"].ToString();
                }
                var result = embed.BuildMessageEmbed("BigBirdBot -  Times people were asked to Play Valorant**",  $" {currentCounter} on {currentDateTime}**\n**Average Per Day: {averagePerDay}**\n{counterHistory}", "", msg.Author.Username, Discord.Color.Green);

                await msg.Channel.SendMessageAsync(embed: result.Build());
            }

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

                    if ((message.Contains("https://twitter.com") || message.Contains("https://x.com") || message.Contains("https://tiktok.com") || message.Contains("https://instagram.com")) && !message.Contains(prefix))
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
                                        IReadOnlyCollection<Attachment> attachments = msg.Attachments;
                                        foreach (Attachment? attachment in attachments)
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
                                        EmbedBuilder embed = new EmbedBuilder
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
                                    string content = message.Replace(keyword, "").Trim();
                                    bool multiUrl = false;

                                    if (content.Contains(",") && content.Contains("http"))
                                        multiUrl = true;

                                    if (multiUrl)
                                    {
                                        string[] urls = content.Split(",", StringSplitOptions.TrimEntries);
                                        foreach (string u in urls)
                                        {
                                            bool result = u.Trim().StartsWith("http");

                                            if (!result)
                                            {
                                                EmbedBuilder embed = new EmbedBuilder
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
                                                    EmbedBuilder embed = new EmbedBuilder
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
                                        EmbedBuilder embedSuccess = new EmbedBuilder
                                        {
                                            Title = "BigBirdBot - Added Image",
                                            Color = Color.Blue,
                                            Description = "Added link(s) successfully."
                                        };

                                        await msg.Channel.SendMessageAsync(embed: embedSuccess.Build());
                                    }
                                    else
                                    {
                                        content = cleanup.CleanURLEmbed(content);

                                        // Check if link exists for thirst table
                                        DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                        {
                                            new SqlParameter("@FilePath", content),
                                            new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                        });

                                        if (dtExists.Rows.Count > 0)
                                        {
                                            EmbedBuilder embed = new EmbedBuilder
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

                                                EmbedBuilder embed = new EmbedBuilder
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
                            SocketGuildChannel? channel = msg.Channel as SocketGuildChannel;
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

                                IUserMessage output;
                                Emoji nsfwMarker = new Emoji("❌");
                                IMessageChannel? sender = client.GetChannel(channel.Id) as IMessageChannel;

                                _ = Task.Run(async () =>
                                {
                                    if (dt.Rows.Count > 0 && sender != null)
                                    {
                                        foreach (DataRow dr in dt.Rows)
                                        {
                                            string chatAction = dr["ChatAction"].ToString();
                                            bool isNsfw = bool.Parse(dr["NSFW"].ToString());

                                            if (!string.IsNullOrEmpty(chatAction))
                                            {
                                                await msg.Channel.TriggerTypingAsync(new RequestOptions { Timeout = 30 });
                                                if (chatAction.Contains("C:\\"))
                                                {
                                                    if (isNsfw && !chatAction.Contains("SPOILER_"))
                                                        await msg.Channel.SendFileAsync(dr["ChatAction"].ToString(), isSpoiler: true).ConfigureAwait(false);
                                                    else
                                                    {
                                                        output = await msg.Channel.SendFileAsync(dr["ChatAction"].ToString()).ConfigureAwait(false);
                                                        await output.AddReactionAsync(nsfwMarker).ConfigureAwait(false);
                                                    }
                                                }
                                                else
                                                {
                                                    if (dr["ChatAction"].ToString().Contains("http") && IsLinkWorking(dr["ChatAction"].ToString()))
                                                    {
                                                        if (isNsfw)
                                                        {
                                                            chatAction = "||" + chatAction + "||";
                                                            await sender.SendMessageAsync(chatAction).ConfigureAwait(false);
                                                        }
                                                        else
                                                        {
                                                            output = await sender.SendMessageAsync(dr["ChatAction"].ToString()).ConfigureAwait(false);
                                                            await output.AddReactionAsync(nsfwMarker).ConfigureAwait(false);
                                                        }
                                                    }
                                                    else if (dr["ChatAction"].ToString().Contains("http") && !IsLinkWorking(dr["ChatAction"].ToString()))
                                                    {
                                                        await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {dr["ChatAction"].ToString()}").ConfigureAwait(false);
                                                        storedProcedure.UpdateCreate(Constants.discordBotConnStr, "DeleteThirstURL", new List<SqlParameter> { new SqlParameter("@FilePath", dr["ChatAction"].ToString()), new SqlParameter("@TableName", "") });
                                                    }
                                                    else
                                                    {
                                                        if (isNsfw)
                                                        {
                                                            chatAction = "||" + chatAction + "||";
                                                            await sender.SendMessageAsync(chatAction).ConfigureAwait(false);
                                                        }
                                                        else
                                                        {
                                                            output = await sender.SendMessageAsync(dr["ChatAction"].ToString()).ConfigureAwait(false);
                                                            await output.AddReactionAsync(nsfwMarker).ConfigureAwait(false);
                                                        }
                                                    }
                                                        
                                                }

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
            foreach (SocketGuildUser? u in before.VoiceChannel.ConnectedUsers)
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
                foreach (SocketGuildUser? u in before.VoiceChannel.ConnectedUsers)
                    if (u.IsBot)
                        await u.VoiceChannel.DisconnectAsync();

                StoredProcedure stored = new StoredProcedure();

                if (before.VoiceChannel != null)
                {
                    stored.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", Int64.Parse(before.VoiceChannel.Guild.Id.ToString()))
                    });

                    stored.UpdateCreate(Constants.discordBotConnStr, "DeleteMusicQueueAll", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", Int64.Parse(before.VoiceChannel.Guild.Id.ToString()))
                    });
                }
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

    public static bool IsLinkWorking(string url)
    {
        if (url.Contains("fxtwitter") || url.Contains("vxtwitter"))
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

            //You can set some parameters in the "request" object...
            request.AllowAutoRedirect = true;

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                var encoding = ASCIIEncoding.ASCII;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                    if (responseText.Contains("post doesn't exist"))
                        return false;
                }
                return true;
            }
            catch
            { //TODO: Check for the right exception here
                return false;
            }
        }
        else
            return true;
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
                LogLevel = LogSeverity.Verbose
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
        Emoji nsfwMarker = new Emoji("❌");
        EmbedHelper embedNsfw = new EmbedHelper();

        var download = message.GetOrDownloadAsync().Result;
        IReadOnlyCollection<IEmbed> embed = download.Embeds;
        var msg = download.ToString();
        var attach = download.Attachments;
        StoredProcedure stored = new StoredProcedure();
        if (client.GetUser(reaction.UserId).IsBot) return;

        // Mark the message as NSFW
        if (reaction.Emote.Name == nsfwMarker.Name)
        {
            DataTable dt = new DataTable();

            if (msg != null)
            {
                dt = stored.Select(Constants.discordBotConnStr, "GetKeywordNSFW", new List<SqlParameter>
                {
                    new SqlParameter("@Message", msg)
                });

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (dr["NSFW"].ToString() != "1")
                        {
                            dt = stored.Select(Constants.discordBotConnStr, "MarkKeywordNSFW", new List<SqlParameter>
                            {
                                new SqlParameter("@Message", msg)
                            });

                            await channel.Value.SendMessageAsync(embed: embedNsfw.BuildMessageEmbed("BigBirdBot - NSFW", $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)", "", "BigBirdBot", Discord.Color.Blue, null, null).Build());
                        }
                    }
                }
                return;
            }
            if (attach != null && attach.Count > 0)
            {
                foreach (var a in attach)
                {
                    dt = stored.Select(Constants.discordBotConnStr, "GetKeywordNSFW", new List<SqlParameter>
                    {
                        new SqlParameter("@Message", a.Filename)
                    });

                    if (dt.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            if (dr["NSFW"].ToString() != "1")
                            {
                                dt = stored.Select(Constants.discordBotConnStr, "MarkKeywordNSFW", new List<SqlParameter>
                                {
                                    new SqlParameter("@Message", a.Filename)
                                });

                                await channel.Value.SendMessageAsync(embed: embedNsfw.BuildMessageEmbed("BigBirdBot - NSFW", $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)", "", "BigBirdBot", Discord.Color.Blue, null, null).Build());
                            }
                        }
                    }
                }
                
                return;
            }
        }

        if (reaction.Emote.Name == triviaA.Name || reaction.Emote.Name == triviaB.Name || reaction.Emote.Name == triviaC.Name || reaction.Emote.Name == triviaD.Name)
        {
            try
            {
                if (embed.Count > 0)
                {
                    long messageId = Int64.Parse(message.Id.ToString());
                    DataTable dt = stored.Select(Constants.discordBotConnStr, "GetTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                    string theReaction = reaction.Emote.Name;
                    if (dt.Rows.Count > 0)
                    {
                        string correctAnswer = "";
                        foreach (DataRow dr in dt.Rows)
                        {
                            correctAnswer = dr["CorrectAnswer"].ToString();
                        }

                        foreach (IEmbed? e in embed)
                        {
                            foreach (EmbedField f in e.Fields.Where(s => s.Name.Contains(".")).ToList())
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
                IUser user = await client.GetUserAsync(ulong.Parse(userId));

                if (dr["FilePath"].ToString().Contains("C:\\"))
                    await user.SendFileAsync(filePath, $"**{tableName} - {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET")}**");
                else
                {
                    if (IsLinkWorking(filePath))
                        await user.SendMessageAsync($"**{tableName} - {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET")}**\n**URL:** {filePath}");
                    else
                    {
                        storedProcedure.UpdateCreate(Constants.discordBotConnStr, "DeleteThirstURL", new List<SqlParameter> { new SqlParameter("@FilePath", filePath), new SqlParameter("@TableName", "") });
                        await user.SendMessageAsync($"**{tableName} - {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET")}**\n**URL:** {filePath} - This was a dead link and was removed from future postings");
                    }
                }

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

