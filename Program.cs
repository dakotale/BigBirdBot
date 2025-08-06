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
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static DiscordSocketClient client = new DiscordSocketClient();
    internal readonly LoggingService loggingService;
    internal readonly IServiceProvider services;

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
        // Start a timer for 60 seconds for scheduled events.
        eventTimer = new System.Timers.Timer(60000); 
        // Once the timer reaches 60 seconds, check if there are any scheduled events.
        eventTimer.Elapsed += OnTimedEvent;
        // Reset them and start it again.
        eventTimer.AutoReset = true;
        eventTimer.Enabled = true;
        eventTimer.Start();
        new Program().MainAsync().GetAwaiter().GetResult();
    }
    public async Task MainAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        // Setup the bot with the token
        await RunBot(client);
    }

    public async Task RunBot(DiscordSocketClient client)
    {
        try
        {
            await loggingService.InfoAsync("Starting Bot");

#if DEBUG
        var token = Constants.devBotToken;
#else
            var token = Constants.botToken;
#endif
            await client.LoginAsync(TokenType.Bot, token);

            RegisterEvents(client);

            if (client.ConnectionState != Discord.ConnectionState.Connected &&
                client.ConnectionState != Discord.ConnectionState.Connecting)
            {
                await client.StartAsync();
            }

            await Task.Delay(Timeout.Infinite);
        }
        // Common exceptions thrown by the Discord.NET API
        catch (Exception ex) when (ex is WebSocketException
                                 || ex is WebSocketClosedException
                                 || ex is GatewayReconnectException)
        {
            await HandleReconnectAsync(client, ex);
        }
        catch (Exception ex)
        {
            await HandleReconnectAsync(client, ex);
        }
    }

    // Supported event-driven events for the bot
    private void RegisterEvents(DiscordSocketClient client)
    {
        // Unsubscribe first to avoid multiple subscriptions on reconnects
        client.Disconnected -= OnDisconnectedAsync;
        client.Disconnected += OnDisconnectedAsync;

        client.Connected -= OnConnectedAsync;
        client.Connected += OnConnectedAsync;

        client.ReactionAdded -= HandleReactionAsync;
        client.ReactionAdded += HandleReactionAsync;

        client.JoinedGuild -= JoinedGuild;
        client.JoinedGuild += JoinedGuild;

        client.UserJoined -= UserJoined;
        client.UserJoined += UserJoined;

        client.UserLeft -= UserLeft;
        client.UserLeft += UserLeft;

        client.ButtonExecuted -= ButtonHandler;
        client.ButtonExecuted += ButtonHandler;

        client.MessageReceived -= MessageReceived;
        client.MessageReceived += MessageReceived;

        client.UserVoiceStateUpdated -= UserVoiceStateUpdated;
        client.UserVoiceStateUpdated += UserVoiceStateUpdated;

        client.Log -= LogMessage;
        client.Log += LogMessage;
    }

    // Console message when the bot disconnects to the Discord API
    private async Task OnDisconnectedAsync(Exception arg)
    {
        await loggingService.InfoAsync($"Bot disconnected - {client.ConnectionState}");
        if (client.ConnectionState == Discord.ConnectionState.Connecting)
            await loggingService.InfoAsync($"Bot connecting - {client.ConnectionState}");
    }

    // Console message when the bot connects to the Discord API
    private async Task OnConnectedAsync()
    {
        await loggingService.InfoAsync("Bot connected");
        await client.SetGameAsync("/reportbug");
    }

    // When the connection to discord was lost, try to reconnect without breaking everything.
    private async Task HandleReconnectAsync(DiscordSocketClient client, Exception ex)
    {
        await loggingService.InfoAsync($"{ex.GetType().Name}: {ex.Message}");
        try
        {
            await client.LogoutAsync();
        }
        catch
        {
            // ignore logout errors
        }
        client.Dispose();

        var newClient = services.GetRequiredService<DiscordSocketClient>();
        await RunBot(newClient);
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

    // This runs when a message comes in for the keyword handling
    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg is not { Author.IsBot: false, Author.IsWebhook: false, Channel: SocketGuildChannel msgChannel })
            return;

        string message = msg.Content.Trim().ToLower();
        string connStr = Constants.discordBotConnStr;
        string serverId = msgChannel.Guild.Id.ToString();
        string userId = msg.Author.Id.ToString();
        string prefix = "-";

        var stored = new StoredProcedure();
        var cleanup = new URLCleanup();
        var parameters = new List<SqlParameter>();

        // Get server status
        var dt = stored.Select(connStr, "GetServerPrefixByServerID", new List<SqlParameter> {
            new SqlParameter("ServerUID", long.Parse(serverId))
        });
        if (!bool.TryParse(dt.Rows[0]["IsActive"]?.ToString(), out var isServerActive) || !isServerActive)
            return;

        // Check if keyword system is active
        dt = stored.Select(connStr, "CheckIfKeywordsAreActivePerServer", new List<SqlParameter> {
            new SqlParameter("ServerUID", long.Parse(serverId))
        });
        if (dt.Rows.Count == 0 || int.Parse(dt.Rows[0]["TotalActive"].ToString()) <= 0)
            return;

        bool isCommand = message.StartsWith(prefix);

        if (cleanup.HasSocialMediaEmbed(message) && !isCommand)
        {
            dt = stored.Select(connStr, "GetTwitterBroken", new List<SqlParameter> {
                new SqlParameter("@ServerID", long.Parse(serverId))
            });

            if (bool.TryParse(dt.Rows[0]["TwitterBroken"]?.ToString(), out var isTwitterBroken) && isTwitterBroken)
            {
                await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));
            }

            return;
        }

        if (isCommand)
        {
            var splitMessage = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (splitMessage.Length == 0)
                return;

            string keyword = splitMessage[0].Replace(prefix, "");
            dt = stored.Select(connStr, "GetThirstTableByMap", new List<SqlParameter> {
                new SqlParameter("@AddKeyword", keyword)
            });

            if (dt.Rows.Count == 0)
                return;

            string tablename = dt.Rows[0]["TableName"].ToString();

            if (msg.Attachments.Count > 0)
            {
                AddAttachments(msg, tablename, connStr, userId);
                await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Added Image", Color.Blue, "Added attachment(s) successfully.").Build());
            }

            if (splitMessage.Length > 1)
            {
                string content = message[(prefix.Length + keyword.Length)..].Trim();
                bool multiUrl = content.Contains(",") && content.Contains("http");

                if (multiUrl)
                {
                    string[] urls = content.Split(",", StringSplitOptions.TrimEntries);
                    foreach (string url in urls)
                    {
                        if (!url.StartsWith("http"))
                        {
                            await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Error", Color.Red, $"The URL provided (*{url}*) is invalid.").Build());
                            continue;
                        }

                        string cleanedUrl = cleanup.CleanURLEmbed(url);
                        if (!IsUrlNew(cleanedUrl, tablename, connStr, stored))
                        {
                            await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Error", Color.Red, $"The URL provided (*{cleanedUrl}*) was already added.").Build());
                            continue;
                        }

                        foreach (DataRow row in dt.Rows)
                        {
                            stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
                            {
                                new SqlParameter("@FilePath", cleanedUrl),
                                new SqlParameter("@TableName", row["TableName"].ToString()),
                                new SqlParameter("@UserID", userId)
                            });
                        }
                    }

                    await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Added Image", Color.Blue, "Added link(s) successfully.").Build());
                }
                else
                {
                    string cleanedContent = cleanup.CleanURLEmbed(content);

                    if (!IsUrlNew(cleanedContent, tablename, connStr, stored))
                    {
                        await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Error", Color.Red, "The URL provided was already added.").Build());
                        return;
                    }

                    foreach (DataRow row in dt.Rows)
                    {
                        stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
                        {
                            new SqlParameter("@FilePath", cleanedContent),
                            new SqlParameter("@TableName", row["TableName"].ToString()),
                            new SqlParameter("@UserID", userId)
                        });
                    }

                    await msg.Channel.SendMessageAsync(embed: CreateMessageEmbed("Added URL/Text", Color.Blue, "Added URL/Text(s) successfully.").Build());
                }
            }

            return;
        }

        // Chat Keyword Actions (non-command)
        parameters = new List<SqlParameter> {
            new SqlParameter("@UserID", userId),
            new SqlParameter("@ServerID", long.Parse(serverId))
        };

        dt = stored.Select(connStr, "GetChatKeywordExclusion", parameters);
        if (dt.Rows.Count > 0)
            return;

        parameters = new List<SqlParameter> {
            new SqlParameter("@ServerID", long.Parse(serverId)),
            new SqlParameter("@Message", message)
        };

        dt = stored.Select(connStr, "GetChatAction", parameters);

        if (dt.Rows.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                var sender = client.GetChannel(msgChannel.Id) as IMessageChannel;
                if (sender == null) return;

                foreach (DataRow row in dt.Rows)
                {
                    string chatAction = row["ChatAction"].ToString();
                    bool isNsfw = bool.TryParse(row["NSFW"]?.ToString(), out var nsfw) && nsfw;

                    if (string.IsNullOrWhiteSpace(chatAction))
                        continue;

                    await msg.Channel.TriggerTypingAsync();

                    if (chatAction.Contains("C:\\"))
                    {
                        var isSpoiler = isNsfw && !chatAction.Contains("SPOILER_");
                        var output = await msg.Channel.SendFileAsync(chatAction, isSpoiler: isSpoiler);
                        if (!isSpoiler)
                            await output.AddReactionAsync(new Emoji("❌"));
                    }
                    else if (chatAction.Contains("http"))
                    {
                        if (IsLinkWorking(chatAction))
                        {
                            if (isNsfw)
                                chatAction = $"||{chatAction}||";

                            var output = await sender.SendMessageAsync(chatAction);
                            if (!isNsfw)
                                await output.AddReactionAsync(new Emoji("❌"));
                        }
                        else
                        {
                            await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {chatAction}");
                            stored.UpdateCreate(connStr, "DeleteThirstURL", new List<SqlParameter> {
                                new SqlParameter("@FilePath", chatAction),
                                new SqlParameter("@TableName", "")
                            });
                        }
                    }
                    else
                    {
                        if (isNsfw)
                            chatAction = $"||{chatAction}||";

                        var output = await sender.SendMessageAsync(chatAction);
                        if (!isNsfw)
                            await output.AddReactionAsync(new Emoji("❌"));
                    }

                    // Audit trail
                    parameters = new List<SqlParameter> {
                        new SqlParameter("@ChatKeywordID", int.Parse(row["ChatKeywordID"].ToString())),
                        new SqlParameter("@MessageText", message),
                        new SqlParameter("@CreatedBy", userId),
                        new SqlParameter("@ServerID", long.Parse(serverId))
                    };

                    stored.UpdateCreate(connStr, "AddAuditKeyword", parameters);
                }
            });
        }
    }

    // Auto leave if no one is in VC
    private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var guild = before.VoiceChannel?.Guild ?? after.VoiceChannel?.Guild;
        if (guild == null)
            return;

        var serverIdParam = new SqlParameter("@ServerID", guild.Id.ToString());

        // Helper method to disconnect all bots in a voice channel
        async Task DisconnectBotsAsync(SocketVoiceChannel channel)
        {
            foreach (var botUser in channel.ConnectedUsers.Where(u => u.IsBot))
            {
                await botUser.VoiceChannel.DisconnectAsync();
            }
        }

        if (user.IsBot)
        {
            // If bot left voice channel, disconnect all bots in the previous channel
            if (after.VoiceChannel == null && before.VoiceChannel != null)
            {
                await DisconnectBotsAsync(before.VoiceChannel);

                var stored = new StoredProcedure();
                stored.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter> { serverIdParam });
            }
        }
        else
        {
            // If a non-bot user leaves voice channel and no other non-bots remain
            if (before.VoiceChannel != null && after.VoiceChannel == null)
            {
                bool anyNonBotLeft = before.VoiceChannel.ConnectedUsers.Any(u => !u.IsBot);
                if (!anyNonBotLeft)
                {
                    await DisconnectBotsAsync(before.VoiceChannel);

                    var stored = new StoredProcedure();
                    stored.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter> { serverIdParam });
                    stored.UpdateCreate(Constants.discordBotConnStr, "DeleteMusicQueueAll", new List<SqlParameter> { serverIdParam });
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
    // Handle trivia and NSFW keyword stuff
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

        if (client.GetUser(reaction.UserId).IsBot) 
            return;

        // Mark the message as NSFW
        if (reaction.Emote.Name == nsfwMarker.Name && download.Author.IsBot && download.Reactions.Count < 2)
        {
            var connStr = Constants.discordBotConnStr;
            var userId = reaction.User.Value.Id.ToString();
            var messageId = download.Id.ToString();

            var existingReaction = stored.Select(connStr, "GetReactionMessage", new List<SqlParameter>
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@MessageID", messageId)
            });

            if (existingReaction.Rows.Count > 0)
                return;

            // Add initial reaction record
            stored.UpdateCreate(connStr, "AddReactionMessage", new List<SqlParameter>
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@MessageID", messageId)
            });

            async Task HandleNSFWMark(string messageContent)
            {
                var keywordRows = stored.Select(connStr, "GetKeywordNSFW", new List<SqlParameter>
                {
                    new SqlParameter("@Message", messageContent)
                });

                if (!keywordRows.AsEnumerable().Any(r => r["NSFW"].ToString() == "1"))
                {
                    var nsfwResult = stored.Select(connStr, "MarkKeywordNSFW", new List<SqlParameter>
                    {
                        new SqlParameter("@Message", messageContent)
                    });

                    if (nsfwResult.Rows.Count > 0)
                    {
                        await channel.Value.SendMessageAsync(embed:
                        embedNsfw.BuildMessageEmbed(
                            "BigBirdBot - NSFW",
                            $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)",
                            "",
                            "BigBirdBot",
                            Discord.Color.Blue
                        ).Build());
                    }
                }
            }

            // Handle message text
            if (!string.IsNullOrEmpty(msg))
            {
                await HandleNSFWMark(msg);
                return;
            }

            // Handle attachments
            if (attach != null && attach.Count > 0)
            {
                foreach (var a in attach)
                    await HandleNSFWMark(a.Filename);

                return;
            }
        }


        if (reaction.Emote.Name == triviaA.Name || reaction.Emote.Name == triviaB.Name || reaction.Emote.Name == triviaC.Name || reaction.Emote.Name == triviaD.Name)
        {
            try
            {
                if (embed.Count == 0)
                    return;

                var connStr = Constants.discordBotConnStr;
                ulong messageId = message.Id;
                string userMention = reaction.User.Value.Mention;
                string reactionName = reaction.Emote.Name;

                // Lookup tables for mapping letters and emojis
                var emojiMap = new Dictionary<string, string>
                {
                    { triviaA.Name, "A." },
                    { triviaB.Name, "B." },
                    { triviaC.Name, "C." },
                    { triviaD.Name, "D." }
                };

                var dt = stored.Select(connStr, "GetTriviaMessage", new List<SqlParameter>
                {
                    new SqlParameter("@TriviaMessageID", messageId)
                });

                if (dt.Rows.Count == 0)
                    return;

                string correctAnswer = dt.Rows[0]["CorrectAnswer"].ToString();
                var fields = embed.SelectMany(e => e.Fields).Where(f => f.Name.Contains(".")).ToList();

                var correctField = fields.FirstOrDefault(f => f.Value == correctAnswer);
                if (correctField == null || !emojiMap.TryGetValue(reactionName, out string selectedLetter))
                    return;

                var embedHelper = new EmbedHelper();

                if (selectedLetter == correctField.Name)
                {
                    await channel.Value.SendMessageAsync(embed:
                        embedHelper.BuildMessageEmbed(
                            "BigBirdBot - Correct",
                            $"{userMention} answered correctly with **{correctAnswer}**!",
                            "",
                            "BigBirdBot",
                            Discord.Color.Green
                        ).Build());

                    stored.UpdateCreate(connStr, "DeleteTriviaMessage", new List<SqlParameter>
                    {
                        new SqlParameter("@TriviaMessageID", messageId)
                    });
                }
                else
                {
                    await channel.Value.SendMessageAsync(embed:
                        embedHelper.BuildMessageEmbed(
                            "BigBirdBot - Wrong",
                            $"{userMention}, you didn't answer correctly. Try again!",
                            "",
                            "BigBirdBot",
                            Discord.Color.Red
                        ).Build());
                }
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed:
                    errorEmbed.BuildMessageEmbed(
                        "BigBirdBot - Error",
                        ex.Message,
                        Constants.errorImageUrl,
                        "",
                        Color.Red
                    ).Build());
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

                try
                {
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
                catch (HttpException ex)
                {
                    // If we reach here, means the user doesn't allow DMs
                    // Send a DM saying an issue happened
                    IUser user = await client.GetUserAsync(ulong.Parse("171369791486033920"));
                    await user.SendMessageAsync($"Something went wrong sending to this user: {userId}, might be an issue with allowing DMs.\nException Message: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    // If we reach here, something really went wrong and should handle it.
                    // Send a DM saying an issue happened
                    IUser user = await client.GetUserAsync(ulong.Parse("171369791486033920"));
                    await user.SendMessageAsync($"Something went wrong sending to this user: {userId}\nException Message: {ex.Message}");
                    return;
                }
            }
        }

    }
    #endregion

    #region Helpers
    public static bool IsLinkWorking(string url)
    {
        if (!url.Contains("fxtwitter") && !url.Contains("vxtwitter"))
            return true;

        try
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = true;
            request.Method = "GET";
            request.Timeout = 5000; // Set a timeout to avoid hanging

            using var response = (HttpWebResponse)request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream(), Encoding.ASCII);
            string responseText = reader.ReadToEnd();

            return !responseText.Contains("post doesn't exist");
        }
        catch (WebException ex)
        {
            // Handle specific WebException if needed, else assume link is not working or unreachable
            if (ex.Response is HttpWebResponse webResponse)
            {
                // For example, 404 means the link is broken
                if (webResponse.StatusCode == HttpStatusCode.NotFound)
                    return false;
            }

            // For other exceptions, return false or true based on your requirement
            return false;
        }
        catch
        {
            // Unexpected exceptions, return false for safety
            return false;
        }
    }

    private void AddAttachments(SocketMessage msg, string tablename, string connStr, string userId)
    {
        StoredProcedure stored = new StoredProcedure();
        IReadOnlyCollection<Attachment> attachments = msg.Attachments;
        foreach (Attachment? attachment in attachments)
        {
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

            stored.UpdateCreate(connStr, "AddThirstByMap", new List<SqlParameter>
            {
                new SqlParameter("@FilePath", path),
                new SqlParameter("@TableName", tablename),
                new SqlParameter("@UserID", userId)
            });
        }
    }

    private EmbedBuilder CreateMessageEmbed(string title, Color color, string description)
    {
        EmbedBuilder embed = new EmbedBuilder
        {
            Title = "BigBirdBot - " + title,
            Color = color,
            Description = description
        }.WithCurrentTimestamp();

        return embed;
    }

    private bool IsUrlNew(string url, string tableName, string connStr, StoredProcedure stored)
    {
        var checkDt = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter> {
                new SqlParameter("@FilePath", url),
                new SqlParameter("@TableName", tableName)
            });

        return checkDt.Rows.Count == 0;
    }
    #endregion
}

