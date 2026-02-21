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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.WebSockets;
using System.Text;

// IHost replaces the manual ServiceCollection.BuildServiceProvider() pattern.
// It provides structured startup, lifetime management, and logging out of the box.
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(ConfigureServices)
    .Build();

await host.Services.GetRequiredService<BotHost>().RunAsync();

/// <summary>
/// Registers all singleton services required by the bot.
/// Called once by <see cref="IHost"/> during startup.
/// Services are singletons because the Discord client, interaction handler,
/// and Lavalink node all maintain stateful connections that must persist for
/// the lifetime of the application.
/// </summary>
static void ConfigureServices(HostBuilderContext _, IServiceCollection services) =>
    services
        .AddSingleton(new DiscordSocketConfig
        {
            // AllUnprivileged covers most events; MessageContent and GuildMembers
            // are privileged intents that must also be enabled in the Discord portal.
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
            DefaultRetryMode = RetryMode.AlwaysRetry,
            LogGatewayIntentWarnings = false,
            LogLevel = LogSeverity.Verbose
        })
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton<CommandService>()
        .AddSingleton<HttpClient>()
        .AddSingleton<LoggingService>()
        .AddSingleton<InteractionHandlerService>()
        .AddSingleton<InteractionService>(p =>
            new InteractionService(p.GetRequiredService<DiscordSocketClient>()))
        .AddSingleton(new InteractiveConfig
        {
            DefaultTimeout = TimeSpan.FromMinutes(15),
            LogLevel = LogSeverity.Warning
        })
        .AddSingleton<InteractiveService>()
        .AddSingleton<EmbedPagesService>()
        .AddSingleton<MultiButtonsService>()
        .AddSingleton<BotHost>()
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
        .AddLogging(x => x.ClearProviders().SetMinimumLevel(LogLevel.Trace));


/// <summary>
/// Orchestrates the full bot lifecycle: connecting to Discord, registering
/// event handlers, and running the once-per-minute scheduled task loop.
/// </summary>
/// <remarks>
/// Uses a primary constructor (C# 12) to receive injected dependencies directly
/// instead of a constructor body, reducing boilerplate while keeping the class
/// testable and DI-friendly.
/// </remarks>
internal sealed class BotHost(
    DiscordSocketClient client,
    LoggingService logger,
    IServiceProvider services)
{
    // Destination for runtime error embeds — a private server/channel used
    // instead of a log file or database for quick visibility.
    private const ulong LogGuildId = 880569055856185354UL;
    private const ulong LogChannelId = 1156625507840954369UL;

    // Owner's Discord user ID, used for DM notifications when automated tasks fail.
    private const ulong OwnerId = 171369791486033920UL;

    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    // Static lookup: maps trivia emoji names to their letter labels (e.g. "🇦" -> "A.")
    // Defined once at class level to avoid re-allocation on every reaction event.
    private static readonly Dictionary<string, string> EmojiToLetter = new()
    {
        ["🇦"] = "A.",
        ["🇧"] = "B.",
        ["🇨"] = "C.",
        ["🇩"] = "D."
    };


    /// <summary>
    /// Entry point called by the DI container after all services are built.
    /// Initialises slash-command interactions, registers all Discord socket
    /// events, starts the background scheduler, connects to Discord, and then
    /// blocks indefinitely so the process stays alive.
    /// </summary>
    public async Task RunAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        RegisterEvents();

        // Fire-and-forget: the scheduler runs independently of the connection loop.
        _ = RunSchedulerAsync();

        await ConnectAsync();

        // Block the calling thread forever; the bot runs entirely on event callbacks.
        await Task.Delay(Timeout.Infinite);
    }


    /// <summary>
    /// Logs in with the bot token and starts the Discord gateway connection.
    /// On failure, delegates to <see cref="ReconnectAsync"/> which will retry
    /// after a short back-off.
    /// </summary>
    private async Task ConnectAsync()
    {
        try
        {
            await logger.InfoAsync("Starting Bot");
            await client.LoginAsync(TokenType.Bot, Constants.botToken);
            await client.StartAsync();
        }
        catch (Exception ex) when (ex is WebSocketException
                                    or WebSocketClosedException
                                    or GatewayReconnectException
                                    or Exception)
        {
            await ReconnectAsync(ex);
        }
    }

    /// <summary>
    /// Attempts a clean logout, waits 5 seconds to avoid hammering the gateway,
    /// then re-invokes <see cref="ConnectAsync"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="DiscordSocketClient"/> is NOT disposed here. Because it
    /// is registered as a singleton in DI, disposing it would permanently break
    /// the container — subsequent resolves would still return the disposed instance.
    /// Calling <c>LogoutAsync</c> is sufficient to reset the connection state.
    /// </remarks>
    /// <param name="ex">The exception that triggered the reconnect attempt.</param>
    private async Task ReconnectAsync(Exception ex)
    {
        await logger.InfoAsync($"{ex.GetType().Name}: {ex.Message}");
        try { await client.LogoutAsync(); }
        catch { /* Ignore logout errors — reconnecting regardless */ }

        await Task.Delay(TimeSpan.FromSeconds(5));
        await ConnectAsync();
    }


    /// <summary>
    /// Subscribes to all Discord gateway events exactly once at startup.
    /// </summary>
    /// <remarks>
    /// The original code unsubscribed then re-subscribed on every reconnect to
    /// prevent duplicate handlers. That pattern is no longer needed here because
    /// <see cref="ReconnectAsync"/> calls <see cref="ConnectAsync"/> directly —
    /// not <c>RegisterEvents</c> — so handlers can never be double-registered.
    /// </remarks>
    private void RegisterEvents()
    {
        client.Connected += OnConnectedAsync;
        client.Disconnected += OnDisconnectedAsync;
        client.Log += OnLogMessageAsync;
        client.JoinedGuild += OnJoinedGuildAsync;
        client.UserJoined += OnUserJoinedAsync;
        client.UserLeft += OnUserLeftAsync;
        client.ButtonExecuted += OnButtonExecutedAsync;
        client.MessageReceived += OnMessageReceivedAsync;
        client.ReactionAdded += OnReactionAddedAsync;
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
    }

    /// <summary>
    /// Fired when the bot successfully connects to the Discord gateway.
    /// Sets the bot's "Playing" status to the bug-report slash command
    /// so users know how to report issues.
    /// </summary>
    private async Task OnConnectedAsync()
    {
        await logger.InfoAsync("Bot connected");
        await client.SetGameAsync("/reportbug");
    }

    /// <summary>
    /// Fired when the gateway connection drops.
    /// Logs the current connection state for diagnostics; Discord.NET's
    /// built-in <c>AlwaysRetry</c> mode handles the low-level reconnect automatically.
    /// </summary>
    /// <param name="ex">The exception that caused the disconnect.</param>
    private async Task OnDisconnectedAsync(Exception ex) =>
        await logger.InfoAsync($"Bot disconnected ({client.ConnectionState}): {ex.Message}");

    /// <summary>
    /// Handles Discord.NET's internal log messages and forwards exceptions
    /// to a private Discord channel as embeds.
    /// </summary>
    /// <remarks>
    /// Sending errors to Discord rather than a log file or database was a
    /// deliberate design choice for quick visibility without needing server access.
    /// </remarks>
    /// <param name="msg">The log message emitted by the Discord.NET library.</param>
    private async Task OnLogMessageAsync(LogMessage msg)
    {
        if (msg.Exception is null || msg.Message.Length == 0) return;

        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;

        await channel.SendMessageAsync(embed: _embed.BuildMessageEmbed(
            "Exception Thrown",
            $"Exception: {msg.Exception.Message}\nMessage: {msg.Message}",
            "", "BigBirdBot", Color.Red).Build());
    }


    /// <summary>
    /// Fired when a non-bot user leaves (or is kicked/banned from) the server.
    /// Removes the user's record from the database scoped to this specific server.
    /// </summary>
    /// <remarks>
    /// The same user ID can exist across multiple servers, so the delete is
    /// intentionally scoped by both user ID and guild ID to avoid removing
    /// the user's data from other servers they share with the bot.
    /// </remarks>
    private Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        if (user.IsBot || user.IsWebhook) return Task.CompletedTask;

        _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteUser",
        [
            new SqlParameter("@UserID",   user.Id.ToString()),
            new SqlParameter("@ServerID", guild.Id.ToString())
        ]);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fired when a non-bot user joins the server.
    /// Inserts or updates the user's record in the database.
    /// </summary>
    /// <param name="user">The guild user who joined, including server-specific metadata.</param>
    private Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot || user.IsWebhook) return Task.CompletedTask;
        AddUserToDatabase(user, user.Guild.Id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fired when the bot is added to a new guild.
    /// Registers the server in the database (if not already present) and
    /// bulk-inserts all existing non-bot members.
    /// </summary>
    /// <remarks>
    /// <see cref="SocketGuild.DownloadUsersAsync"/> must be called explicitly here
    /// because <c>AlwaysDownloadUsers</c> in the socket config only pre-fills the
    /// member cache for guilds the bot was already connected to at startup —
    /// not for guilds it joins while running.
    ///
    /// If the download returns 0 users, a warning embed is sent to the log channel
    /// rather than silently succeeding with an empty table, which would make the
    /// bot appear to work but miss all existing members.
    /// </remarks>
    /// <param name="guild">The guild the bot was just added to.</param>
    private async Task OnJoinedGuildAsync(SocketGuild guild)
    {
        var existingServerIds = _sp
            .Select(Constants.discordBotConnStr, "GetServers", [])
            .AsEnumerable()
            .Select(r => r["ServerUID"].ToString())
            .ToHashSet();

        if (!existingServerIds.Contains(guild.Id.ToString()))
        {
            _sp.UpdateCreate(Constants.discordBotConnStr, "AddServer",
            [
                new SqlParameter("@ServerUID",        (long)guild.Id),
                new SqlParameter("@ServerName",       guild.Name),
                new SqlParameter("@DefaultChannelID", (long)guild.DefaultChannel.Id)
            ]);
        }

        await guild.DownloadUsersAsync();

        if (guild.Users.Count == 0)
        {
            await SendLogAsync(
                $"Bot joined **{guild.Name}** but DownloadUsersAsync returned 0 users. Owner: {guild.Owner}",
                Color.Red);
            return;
        }

        foreach (var user in guild.Users.Where(u => !u.IsBot && !u.IsWebhook))
            AddUserToDatabase(user, guild.Id);

        Console.WriteLine($"{guild.Users.Count} users added for {guild.Name}");
    }

    /// <summary>
    /// Shared helper that upserts a single guild member into the Users table.
    /// Extracted to eliminate the duplicated parameter list between
    /// <see cref="OnUserJoinedAsync"/> and <see cref="OnJoinedGuildAsync"/>.
    /// </summary>
    /// <param name="user">The guild member to persist.</param>
    /// <param name="guildId">The server ID this membership record belongs to.</param>
    private void AddUserToDatabase(SocketGuildUser user, ulong guildId) =>
        _sp.UpdateCreate(Constants.discordBotConnStr, "AddUser",
        [
            new SqlParameter("@UserID",    user.Id.ToString()),
            new SqlParameter("@Username",  user.Username),
            new SqlParameter("@JoinDate",  user.JoinedAt),
            new SqlParameter("@ServerUID", (long)guildId),
            new SqlParameter("@Nickname",  user.Nickname)
        ]);


    /// <summary>
    /// Handles Discord message component interactions (button clicks).
    /// Currently used exclusively for the pronoun role-selection panel.
    /// </summary>
    /// <remarks>
    /// Music queue buttons use an underscore in their <c>CustomId</c> (e.g. "queue_next")
    /// as a naming convention to distinguish them from pronoun buttons, which are plain
    /// integer IDs matching database rows. Any component with an underscore is
    /// silently ignored here and handled by the interaction service instead.
    ///
    /// Toggle logic: if the user already holds the selected pronoun role it is
    /// removed; otherwise it is added. A single button therefore acts as both
    /// assign and unassign without needing a separate "remove" button.
    /// </remarks>
    /// <param name="component">The component interaction context.</param>
    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        // Queue buttons contain '_' in their CustomId — skip them
        if (component.Data.CustomId.Contains('_')) return;

        var pronounTable = _sp.Select(Constants.discordBotConnStr, "GetPronouns", []);
        string pronounSelected = "";

        foreach (DataRow row in pronounTable.Rows)
        {
            string name = row["Pronoun"].ToString()!;
            string id = row["ID"].ToString()!;

            // Create the role on-demand if it doesn't exist yet on this guild
            if (!client.GetGuild(component.GuildId!.Value).Roles.Any(r => r.Name == name))
                await client.GetGuild(component.GuildId.Value).CreateRoleAsync(name);

            if (id == component.Data.CustomId)
                pronounSelected = name;
        }

        var guild = client.GetGuild(component.GuildId!.Value);
        var role = guild.Roles.FirstOrDefault(r => r.Name == pronounSelected);
        var guildUser = guild.GetUser(component.User.Id);

        if (role is null) return;

        bool hasRole = guildUser.Roles.Any(r => r.Name == role.Name);

        if (hasRole)
            await (guildUser as IGuildUser)!.RemoveRoleAsync(role);
        else
            await (guildUser as IGuildUser)!.AddRoleAsync(role);

        string action = hasRole ? "removed" : "added";
        await component.RespondAsync(
            embed: _embed.BuildMessageEmbed(
                "Pronoun Selection",
                $"Pronouns were successfully {action} for {component.User.Username}.",
                "", component.User.Username, Color.Blue).Build(),
            ephemeral: true);
    }


    /// <summary>
    /// Central message handler supporting three distinct behaviours:
    /// </summary>
    /// <remarks>
    /// <list type="number">
    ///   <item>
    ///     <term>Embed fixer</term>
    ///     <description>
    ///       Detects Twitter/X links whose Discord embed is broken and re-sends
    ///       a fixed fxtwitter/vxtwitter URL when the server has opted in to
    ///       this feature via the database.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Prefix commands (<c>-keyword</c>)</term>
    ///     <description>
    ///       Allows trusted users to add images, URLs, or text to a keyword's
    ///       response pool directly from chat. Supports single values,
    ///       comma-separated URL batches, and file attachments.
    ///       Delegated to <see cref="HandlePrefixCommandAsync"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Keyword auto-response</term>
    ///     <description>
    ///       Checks every non-command message against the server's keyword map
    ///       and automatically replies with associated media or text.
    ///       Dispatched via <c>Task.Run</c> so file I/O and HTTP checks inside
    ///       <see cref="SendChatActionsAsync"/> do not block the gateway event loop.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="msg">The incoming socket message.</param>
    private async Task OnMessageReceivedAsync(SocketMessage msg)
    {
        if (msg is not { Author.IsBot: false, Author.IsWebhook: false, Channel: SocketGuildChannel msgChannel })
            return;

        string message = msg.Content.Trim().ToLower();
        string serverId = msgChannel.Guild.Id.ToString();
        string userId = msg.Author.Id.ToString();
        const string prefix = "-";

        // Guard: do nothing if this server has been deactivated in the database
        var serverInfo = _sp.Select(Constants.discordBotConnStr, "GetServerByID",
            [new SqlParameter("ServerUID", long.Parse(serverId))]);

        if (!bool.TryParse(serverInfo.Rows[0]["IsActive"]?.ToString(), out bool active) || !active)
            return;

        var cleanup = new URLCleanup();

        if (cleanup.HasSocialMediaEmbed(message) && !message.StartsWith(prefix))
        {
            var embedSettings = _sp.Select(Constants.discordBotConnStr, "GetEmbedBroken",
                [new SqlParameter("@ServerID", long.Parse(serverId))]);

            if (bool.TryParse(embedSettings.Rows[0]["FixEmbed"]?.ToString(), out bool fix) && fix)
                await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));

            // Return regardless — don't keyword-match a raw Twitter URL
            return;
        }

        if (message.StartsWith(prefix))
        {
            await HandlePrefixCommandAsync(msg, message, serverId, userId, prefix, cleanup);
            return;
        }

        var actions = _sp.Select(Constants.discordBotConnStr, "GetChatAction",
        [
            new SqlParameter("@ServerID", long.Parse(serverId)),
            new SqlParameter("@Message",  message)
        ]);

        if (actions.Rows.Count > 0)
            _ = Task.Run(() => SendChatActionsAsync(msg, msgChannel, actions));
    }

    /// <summary>
    /// Processes prefix-style commands that add content to a keyword's response pool.
    /// </summary>
    /// <remarks>
    /// Supported input formats:
    /// <list type="bullet">
    ///   <item>File attachment — downloaded to disk and registered in the database.</item>
    ///   <item>Single URL or text value — cleaned via <see cref="URLCleanup"/> and stored.</item>
    ///   <item>
    ///     Comma-separated URLs (e.g. <c>-bird http://a.com, http://b.com</c>) —
    ///     each URL is validated, cleaned, and stored individually. Invalid entries
    ///     are reported inline without aborting the rest of the batch.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="msg">Original socket message, needed for channel replies and attachments.</param>
    /// <param name="message">Lower-cased, trimmed message content.</param>
    /// <param name="serverId">Guild ID as a string.</param>
    /// <param name="userId">Author's user ID as a string.</param>
    /// <param name="prefix">The command prefix character(s), currently <c>"-"</c>.</param>
    /// <param name="cleanup">URL normalisation helper instance.</param>
    private async Task HandlePrefixCommandAsync(
        SocketMessage msg, string message, string serverId,
        string userId, string prefix, URLCleanup cleanup)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string keyword = parts[0][prefix.Length..];
        var keywordMap = _sp.Select(Constants.discordBotConnStr, "GetChatKeywordMap",
            [new SqlParameter("@AddKeyword", keyword)]);

        if (keywordMap.Rows.Count == 0) return;

        if (msg.Attachments.Count > 0)
        {
            AddAttachments(msg, keywordMap.Rows[0]["Keyword"].ToString()!, Constants.discordBotConnStr, userId);
            await msg.Channel.SendMessageAsync(
                embed: BuildEmbed("Added Image", Color.Blue, "Added attachment(s) successfully.").Build());
        }

        if (parts.Length <= 1) return;

        string content = message[(prefix.Length + keyword.Length)..].Trim();
        bool isMultiUrl = content.Contains(',') && content.Contains("http");

        if (isMultiUrl)
        {
            foreach (string url in content.Split(',', StringSplitOptions.TrimEntries))
            {
                if (!url.StartsWith("http"))
                {
                    await msg.Channel.SendMessageAsync(
                        embed: BuildEmbed("Error", Color.Red, $"Invalid URL: *{url}*").Build());
                    continue;
                }
                StoreChatKeyword(keywordMap, cleanup.CleanURLEmbed(url), userId);
            }
            await msg.Channel.SendMessageAsync(
                embed: BuildEmbed("Added Image", Color.Blue, "Added link(s) successfully.").Build());
        }
        else
        {
            StoreChatKeyword(keywordMap, cleanup.CleanURLEmbed(content), userId);
            await msg.Channel.SendMessageAsync(
                embed: BuildEmbed("Added URL/Text", Color.Blue, "Added URL/Text successfully.").Build());
        }
    }

    /// <summary>
    /// Persists a single content value (URL or text) against every keyword row
    /// returned by the <c>GetChatKeywordMap</c> stored procedure.
    /// </summary>
    /// <remarks>
    /// A single keyword prefix (e.g. <c>-bird</c>) can map to multiple table entries,
    /// so this iterates all rows rather than only the first match.
    /// </remarks>
    /// <param name="keywordMap">DataTable returned by <c>GetChatKeywordMap</c>.</param>
    /// <param name="value">The cleaned URL or text content to store.</param>
    /// <param name="userId">Discord user ID of the person who submitted this content.</param>
    private void StoreChatKeyword(DataTable keywordMap, string value, string userId)
    {
        foreach (DataRow row in keywordMap.Rows)
        {
            _sp.UpdateCreate(Constants.discordBotConnStr, "AddChatKeyword",
            [
                new SqlParameter("@FilePath",  value),
                new SqlParameter("@TableName", row["Keyword"].ToString()),
                new SqlParameter("@UserID",    userId)
            ]);
        }
    }

    /// <summary>
    /// Sends all chat-action responses triggered by a keyword match.
    /// Intended to run inside <c>Task.Run</c> so file I/O and HTTP link
    /// checks do not block the Discord gateway event loop.
    /// </summary>
    /// <remarks>
    /// Three content types are handled in priority order:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Local file path (<c>C:\...</c>)</term>
    ///     <description>
    ///       Opened as a stream and sent via <c>SendFileAsync</c>.
    ///       NSFW files are sent as Discord spoilers unless the filename is
    ///       already prefixed with <c>SPOILER_</c> (which Discord handles natively).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>HTTP URL</term>
    ///     <description>
    ///       Link liveness is checked first via <see cref="IsLinkWorking"/>.
    ///       Dead links are deleted from the database and a notice is posted.
    ///       Live NSFW links are wrapped in Discord spoiler bars before sending.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Plain text</term>
    ///     <description>
    ///       Sent as a raw message. NSFW text is wrapped in spoiler bars.
    ///     </description>
    ///   </item>
    /// </list>
    /// All non-NSFW responses receive an ❌ reaction so users can flag content
    /// that should be marked NSFW via <see cref="OnReactionAddedAsync"/>.
    /// </remarks>
    private async Task SendChatActionsAsync(SocketMessage msg, SocketGuildChannel msgChannel, DataTable actions)
    {
        var sender = client.GetChannel(msgChannel.Id) as IMessageChannel;
        if (sender is null) return;

        foreach (DataRow row in actions.Rows)
        {
            string chatAction = row["ChatAction"].ToString()!;
            string keyword = row["Keyword"].ToString()!;
            bool isNsfw = bool.TryParse(row["NSFW"]?.ToString(), out bool n) && n;

            if (string.IsNullOrWhiteSpace(chatAction)) continue;

            await msg.Channel.TriggerTypingAsync();

            if (chatAction.StartsWith(@"C:\"))
            {
                keyword = char.ToUpper(keyword[0]) + keyword[1..];
                bool isSpoiler = isNsfw && !chatAction.Contains("SPOILER_");

                var embed = new EmbedBuilder()
                    .WithTitle(keyword)
                    .WithImageUrl("attachment://" + Path.GetFileName(chatAction))
                    .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                    .Build();

                // await using ensures the FileStream is disposed immediately after send
                await using var stream = File.OpenRead(chatAction);
                var output = await msg.Channel.SendFileAsync(
                    stream, Path.GetFileName(chatAction), embed: embed, isSpoiler: isSpoiler);

                if (!isSpoiler)
                    await output.AddReactionAsync(new Emoji("❌"));
            }
            else if (chatAction.Contains("http"))
            {
                if (IsLinkWorking(chatAction))
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(msg.Content)
                        .WithImageUrl(chatAction)
                        .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                        .Build();

                    var output = await msg.Channel.SendMessageAsync(embed: embed);
                    if (!isNsfw)
                        await output.AddReactionAsync(new Emoji("❌"));
                }
                else
                {
                    await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {chatAction}");
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteChatKeywordURL",
                    [
                        new SqlParameter("@FilePath", chatAction),
                        new SqlParameter("@Keyword",  "")
                    ]);
                }
            }
            else
            {
                string displayAction = isNsfw ? $"||{chatAction}||" : chatAction;
                var output = await sender.SendMessageAsync(displayAction);
                if (!isNsfw)
                    await output.AddReactionAsync(new Emoji("❌"));
            }
        }
    }


    /// <summary>
    /// Auto-disconnects the bot from voice when a channel becomes empty of
    /// human users, and cleans up the music queue and player state in the database.
    /// </summary>
    /// <remarks>
    /// Two distinct cases are handled:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Bot disconnected itself</term>
    ///     <description>
    ///       Any remaining bots in the same channel (e.g. a secondary bot instance)
    ///       are also disconnected and the player record is removed.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Human user left</term>
    ///     <description>
    ///       Only acts if the departing user was the last non-bot in the channel.
    ///       All bots are disconnected, and both the player state and the full
    ///       music queue are cleared so the next session starts fresh.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    private async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var guild = before.VoiceChannel?.Guild ?? after.VoiceChannel?.Guild;
        if (guild is null) return;

        var serverIdParam = new SqlParameter("@ServerID", guild.Id.ToString());

        // Local helper: disconnects every bot currently connected to a given channel
        async Task DisconnectBotsAsync(SocketVoiceChannel channel)
        {
            foreach (var bot in channel.ConnectedUsers.Where(u => u.IsBot))
                await bot.VoiceChannel.DisconnectAsync();
        }

        if (user.IsBot)
        {
            if (after.VoiceChannel is null && before.VoiceChannel is not null)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", [serverIdParam]);
            }
        }
        else if (before.VoiceChannel is not null && after.VoiceChannel is null)
        {
            bool anyNonBotRemaining = before.VoiceChannel.ConnectedUsers.Any(u => !u.IsBot);
            if (!anyNonBotRemaining)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", [serverIdParam]);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteMusicQueueAll", [serverIdParam]);
            }
        }
    }

    /// <summary>
    /// Top-level reaction handler that routes to NSFW flagging or trivia evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NSFW flagging (❌):</b>
    /// When a non-bot user reacts ❌ to a bot-authored message that has fewer than
    /// two existing reactions, the image filename is looked up and marked NSFW in
    /// the database via <see cref="TryMarkNsfwAsync"/>. The two-reaction guard
    /// prevents the flag from being triggered again if the bot itself already reacted.
    /// </para>
    /// <para>
    /// <b>Trivia (🇦–🇩):</b>
    /// Letter emoji reactions on trivia embeds are forwarded to
    /// <see cref="HandleTriviaReactionAsync"/> for answer evaluation.
    /// </para>
    /// </remarks>
    private async Task OnReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction)
    {
        var download = await cachedMsg.GetOrDownloadAsync();
        if (download is null || reaction is null) return;

        // Ignore reactions from bots (including the bot's own ❌ reaction added after posting)
        if (client.GetUser(reaction.UserId)?.IsBot == true) return;

        var nsfwMarker = new Emoji("❌");
        var imageUrl = download.Embeds.FirstOrDefault(e => e.Image.HasValue)?.Image?.Url;

        if (reaction.Emote.Name == nsfwMarker.Name && download.Author.IsBot && download.Reactions.Count < 2)
        {
            string? fileName = imageUrl is not null
                ? Path.GetFileName(new Uri(imageUrl).LocalPath)
                : null;

            if (!string.IsNullOrEmpty(fileName))
            {
                await TryMarkNsfwAsync(fileName, cachedChannel, reaction);
                return;
            }
        }

        if (IsTriiviaEmoji(reaction.Emote.Name))
            await HandleTriviaReactionAsync(cachedMsg, cachedChannel, reaction, download);
    }

    /// <summary>
    /// Returns <c>true</c> if the emoji name is one of the four trivia answer options (🇦–🇩).
    /// </summary>
    private static bool IsTriiviaEmoji(string name) =>
        name is "🇦" or "🇧" or "🇨" or "🇩";

    /// <summary>
    /// Marks a keyword's content as NSFW in the database if not already flagged,
    /// then sends a public confirmation message.
    /// </summary>
    /// <remarks>
    /// The idempotency check (<c>GetKeywordNSFW</c>) prevents the confirmation
    /// message from being sent repeatedly if multiple users react ❌ to the same
    /// message before the cache updates.
    /// </remarks>
    /// <param name="content">Image filename or message content to flag.</param>
    /// <param name="channel">Channel to post the confirmation embed in.</param>
    /// <param name="reaction">The reaction that triggered the flag, used for the user mention.</param>
    private async Task TryMarkNsfwAsync(
        string content,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        var existing = _sp.Select(Constants.discordBotConnStr, "GetKeywordNSFW",
            [new SqlParameter("@Message", content)]);

        // Already flagged — silently skip
        if (existing.AsEnumerable().Any(r => r["NSFW"].ToString() == "1")) return;

        var result = _sp.Select(Constants.discordBotConnStr, "MarkKeywordNSFW",
            [new SqlParameter("@Message", content)]);

        if (result.Rows.Count > 0)
        {
            await channel.Value.SendMessageAsync(embed: _embed.BuildMessageEmbed(
                "NSFW",
                $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)",
                "", "BigBirdBot", Color.Blue).Build());
        }
    }

    /// <summary>
    /// Evaluates a trivia answer reaction against the correct answer stored in
    /// the database and posts a coloured result embed.
    /// </summary>
    /// <remarks>
    /// The correct answer text is stored in the database and also rendered as an
    /// embed field <em>value</em>. The field <em>name</em> holds the letter label
    /// (e.g. "A."). The method identifies the correct field by matching its value,
    /// then compares the user's selected letter to that field's name.
    ///
    /// On a correct answer the trivia question is deleted from the database so it
    /// cannot be answered again. Incorrect answers leave the question active.
    /// </remarks>
    private async Task HandleTriviaReactionAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        IUserMessage download)
    {
        try
        {
            if (download.Embeds.Count == 0) return;

            long messageId = (long)cachedMsg.Id;
            string userMention = reaction.User.Value.Mention;

            var dt = _sp.Select(Constants.discordBotConnStr, "GetTriviaMessage",
                [new SqlParameter("@TriviaMessageID", messageId)]);

            if (dt.Rows.Count == 0) return;

            string correctAnswer = dt.Rows[0]["CorrectAnswer"].ToString()!;

            // Only consider fields whose names follow the "A." / "B." convention
            var fields = download.Embeds
                .SelectMany(e => e.Fields)
                .Where(f => f.Name.Contains('.'))
                .ToList();

            var correctField = fields.FirstOrDefault(f => f.Value == correctAnswer);
            if (correctField == null || !EmojiToLetter.TryGetValue(reaction.Emote.Name, out string? selectedLetter))
                return;

            bool isCorrect = selectedLetter == correctField.Name;

            await channel.Value.SendMessageAsync(embed: new EmbedHelper().BuildMessageEmbed(
                isCorrect ? "Correct" : "Wrong",
                isCorrect
                    ? $"{userMention} answered correctly with **{correctAnswer}**!"
                    : $"{userMention}, you didn't answer correctly. Try again!",
                "", "BigBirdBot",
                isCorrect ? Color.Green : Color.Red).Build());

            if (isCorrect)
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage",
                    [new SqlParameter("@TriviaMessageID", messageId)]);
        }
        catch (Exception ex)
        {
            await channel.Value.SendMessageAsync(embed: new EmbedHelper()
                .BuildMessageEmbed("Error", ex.Message, Constants.errorImageUrl, "", Color.Red).Build());
        }
    }

    /// <summary>
    /// Runs the once-per-minute scheduled-DM loop using <see cref="PeriodicTimer"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PeriodicTimer"/> is preferred over <c>System.Timers.Timer</c> because:
    /// <list type="bullet">
    ///   <item>It is async-native — no thread-pool callbacks or event delegates.</item>
    ///   <item>It will not fire overlapping ticks if the previous iteration is still running.</item>
    ///   <item>It supports <c>CancellationToken</c> for clean shutdown.</item>
    /// </list>
    /// </remarks>
    private async Task RunSchedulerAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync())
            await RunScheduledKeywordsAsync();
    }

    /// <summary>
    /// Fetches all pending scheduled keyword DMs from the database and delivers
    /// them to the corresponding Discord users via DM.
    /// </summary>
    /// <remarks>
    /// Each database row represents a user who opted in to receive a periodic
    /// DM containing a keyword's content (a local file, a URL, or text).
    ///
    /// Error handling strategy:
    /// <list type="bullet">
    ///   <item>
    ///     <term><see cref="HttpException"/></term>
    ///     <description>
    ///       Usually means the user has disabled DMs. The bot owner is notified
    ///       but the row is NOT re-queued — retrying a user who blocks DMs would
    ///       spam the owner with the same error every minute.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>All other exceptions</term>
    ///     <description>
    ///       Treated as transient failures. The row is re-queued for the next
    ///       minute and the owner receives the stack trace for investigation.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    private async Task RunScheduledKeywordsAsync()
    {
        var dt = _sp.Select(Constants.discordBotConnStr, "GetUsersScheduledKeyword", []);
        if (dt.Rows.Count == 0) return;

        foreach (DataRow row in dt.Rows)
        {
            string userId = row["UserID"].ToString()!;
            string filePath = row["FilePath"].ToString()!;
            string tableName = row["ThirstTable"].ToString()!;
            tableName = char.ToUpper(tableName[0]) + tableName[1..];
            string timestamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET");

            try
            {
                var user = await client.GetUserAsync(ulong.Parse(userId));

                if (filePath.StartsWith(@"C:\"))
                    await user.SendFileAsync(filePath, $"**{tableName} - {timestamp}**");
                else if (IsLinkWorking(filePath))
                    await user.SendMessageAsync($"**{tableName} - {timestamp}**\n**URL:** {filePath}");
                else
                {
                    // Proactively remove dead links so they do not appear in future sends
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteChatKeywordURL",
                    [
                        new SqlParameter("@FilePath", filePath),
                        new SqlParameter("@Keyword",  "")
                    ]);
                    await user.SendMessageAsync(
                        $"**{tableName} - {timestamp}**\n**URL:** {filePath} — dead link removed from future sends.");
                }
            }
            catch (HttpException ex)
            {
                await NotifyOwnerAsync(
                    $"DM failed for user {userId} — they may have DMs disabled.\n{ex.Message}");
            }
            catch (Exception ex)
            {
                _sp.UpdateCreate(Constants.discordBotConnStr, "UpdateUsersScheduledKeywordRequeue",
                    [new SqlParameter("@UserID", userId)]);
                await NotifyOwnerAsync(
                    $"Scheduled send failed for user {userId}.\n{ex.StackTrace}\n" +
                    $"Requeued for {DateTime.Now.AddMinutes(1):yyyy-MM-dd hh:mm tt}.");
            }
        }
    }
    /// <summary>
    /// Sends a plain-text embed to the bot's private log channel.
    /// Used for operational notices that are not exceptions
    /// (e.g. a guild joined with zero downloadable members).
    /// </summary>
    /// <param name="message">The message body for the embed description.</param>
    /// <param name="color">The embed accent colour. Use <see cref="Color.Red"/> for warnings.</param>
    private async Task SendLogAsync(string message, Color color)
    {
        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;
        await channel.SendMessageAsync(embed: _embed
            .BuildMessageEmbed("Log", message, "", "BigBirdBot", color).Build());
    }

    /// <summary>
    /// Sends a direct message to the bot owner.
    /// Used by the scheduled task runner to report delivery failures.
    /// </summary>
    /// <param name="message">The notification message text.</param>
    private async Task NotifyOwnerAsync(string message)
    {
        var owner = await client.GetUserAsync(OwnerId);
        await owner.SendMessageAsync(message);
    }

    /// <summary>
    /// Checks whether a URL is currently reachable.
    /// </summary>
    /// <remarks>
    /// Only fxtwitter and vxtwitter URLs are actively checked via HTTP because
    /// these services return HTTP 200 even for deleted posts — the only reliable
    /// indicator of a missing post is a specific phrase in the response body.
    /// All other URL types are assumed live to avoid the latency cost of
    /// checking every link on high-volume servers.
    ///
    /// A 15-second timeout is used to prevent false negatives caused by
    /// temporarily slow upstream servers.
    /// </remarks>
    /// <param name="url">The URL to verify.</param>
    /// <returns>
    /// <c>true</c> if the URL is reachable and the content exists;
    /// <c>false</c> if a 404 is returned or the body indicates a deleted post.
    /// </returns>
    public static bool IsLinkWorking(string url)
    {
        if (!url.Contains("fxtwitter") && !url.Contains("vxtwitter"))
            return true;

        try
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = true;
            request.Method = "GET";
            request.Timeout = 15_000;

            using var response = (HttpWebResponse)request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream(), Encoding.ASCII);
            return !reader.ReadToEnd().Contains("post doesn't exist");
        }
        catch (WebException ex) when (ex.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads message attachments to local disk and registers each path in
    /// the keyword database for future chat-action responses.
    /// </summary>
    /// <remarks>
    /// A high-precision timestamp suffix (<c>yyyyMMdd_HHmmssfffff</c>) is
    /// appended to each filename to prevent collisions when the same file is
    /// uploaded multiple times across different keywords or dates.
    ///
    /// Downloads are fire-and-forget: the database entry is written immediately
    /// with the expected path so the keyword is available the moment the file
    /// lands on disk. This avoids blocking the message handler on network I/O.
    ///
    /// <see cref="System.Net.WebClient"/> (used in the original) is obsolete
    /// since .NET 5. Downloads now use <see cref="HttpClient"/> via
    /// <see cref="DownloadAttachmentAsync"/>.
    /// </remarks>
    /// <param name="msg">The message containing the attachments.</param>
    /// <param name="tablename">The keyword table name to associate files with.</param>
    /// <param name="connStr">SQL Server connection string.</param>
    /// <param name="userId">Discord user ID of the person who uploaded the files.</param>
    private void AddAttachments(SocketMessage msg, string tablename, string connStr, string userId)
    {
        tablename = tablename.Replace("KeywordMulti.", "");

        foreach (var attachment in msg.Attachments)
        {
            string[] parts = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string uniqueName = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}";
            string path = $@"C:\Temp\DiscordBot\{tablename}\{uniqueName}.{parts[1]}";

            // Non-blocking: DB entry written immediately with the expected path
            _ = DownloadAttachmentAsync(attachment.Url, path);

            _sp.UpdateCreate(connStr, "AddChatKeyword",
            [
                new SqlParameter("@FilePath",  path),
                new SqlParameter("@TableName", tablename),
                new SqlParameter("@UserID",    userId)
            ]);
        }
    }

    /// <summary>
    /// Downloads a remote file and writes it to the local filesystem.
    /// Replaces the obsolete <c>WebClient.DownloadFileAsync</c>.
    /// </summary>
    /// <param name="url">The source URL to download from.</param>
    /// <param name="path">The full local path (including filename) to write to.</param>
    private static async Task DownloadAttachmentAsync(string url, string path)
    {
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(path, bytes);
    }

    /// <summary>
    /// Creates a simple titled embed with description text and an auto-timestamp.
    /// A convenience factory to avoid repeating <see cref="EmbedBuilder"/>
    /// initialisation throughout the event handlers.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="color">The embed accent colour.</param>
    /// <param name="description">The embed body text.</param>
    /// <returns>A pre-configured <see cref="EmbedBuilder"/> ready to call <c>.Build()</c> on.</returns>
    private static EmbedBuilder BuildEmbed(string title, Color color, string description) =>
        new EmbedBuilder { Title = title, Color = color, Description = description }
            .WithCurrentTimestamp();
}