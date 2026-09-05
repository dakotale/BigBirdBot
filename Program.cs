using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Services;
using Lavalink4NET.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Entry point: build the generic host, wire up DI, then hand control to BotHost.
var builder = Host.CreateApplicationBuilder(args);
ConfigureServices(builder.Services);
await builder.Build().Services
             .GetRequiredService<BotHost>()
             .RunAsync();

/// <summary>
/// Registers every service the bot needs with the DI container: the Discord socket
/// client, command/interaction handling, Lavalink (voice/music), Spotify, and the AI
/// chat service. Called once at startup before BotHost.RunAsync begins.
/// </summary>
static void ConfigureServices(IServiceCollection services) =>
    services
        // Gateway intents control which events Discord sends us — these three cover
        // guild/channel content plus member join/leave, without requesting privileged
        // intents we don't need.
        .AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
                                     | GatewayIntents.MessageContent
                                     | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
            DefaultRetryMode = RetryMode.AlwaysRetry,
            LogGatewayIntentWarnings = false,
            LogLevel = LogSeverity.Info
        })
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton<LoggingService>()
        .AddSingleton<InteractionHandlerService>()
        .AddSingleton<InteractionService>(p =>
            new InteractionService(p.GetRequiredService<DiscordSocketClient>()))
        .AddSingleton<BotHost>()
        .AddLavalink()
        .ConfigureLavalink(x =>
        {
            x.BaseAddress = new Uri(Constants.lavalinkUrl);
            x.Passphrase = Constants.lavaLinkPwd;
            x.BufferSize = 2048;
            x.Label = "BigBirdBot";
            x.ReadyTimeout = TimeSpan.FromMinutes(15);
            // Grace window for the Lavalink server to keep the session (and its players)
            // alive across a brief WebSocket drop, so a transient blip doesn't 404 every
            // subsequent player call with "Session not found". TimeSpan.Zero previously
            // used here still enables resumption but with a zero-second grace window,
            // which is indistinguishable from no protection at all.
            x.ResumptionOptions = new(TimeSpan.FromSeconds(60));
        })
        .AddHttpClient()
        .AddSingleton<ISpotifyService, SpotifyService>()
        .AddSingleton<IAIChatService, AIChatService>()
        // EF Core — every stored-procedure-backed feature area has now moved onto this.
        // A factory + singleton service: the bot has no request scope, and each unit of
        // work opens and disposes its own context.
        .AddDbContextFactory<BigBirdContext>(o => o.UseNpgsql(Constants.discordBotConnStr))
        .AddSingleton<KeywordService>()
        .AddSingleton<KeywordMaintenanceService>()
        .AddSingleton<AuditService>()
        .AddSingleton<AutoRoleService>()
        .AddSingleton<SchedulingService>()
        .AddSingleton<AIMessageService>()
        .AddSingleton<WordPuzzleService>()
        .AddSingleton<ServerService>()
        .AddSingleton<UserService>()
        .AddSingleton<PronounService>()
        .AddSingleton<MusicService>()
        .AddLogging(x => x.ClearProviders().SetMinimumLevel(LogLevel.Trace));


/// <summary>
/// Top-level orchestrator for the bot's lifetime: connects to Discord, wires up every
/// gateway event handler, and runs the background scheduler and stock-price timers.
/// Also hosts the message-based (non-slash-command) features — keyword triggers, the
/// hourly bonus word puzzle, pronoun buttons, and NSFW/dead-link cleanup —
/// since these react to raw events rather than slash commands.
/// </summary>
internal sealed class BotHost(
    DiscordSocketClient client,
    LoggingService logger,
    IServiceProvider services,
    IHttpClientFactory httpClientFactory,
    KeywordService keywords,
    KeywordMaintenanceService keywordMaintenance,
    AuditService audit,
    AutoRoleService autoRoles,
    SchedulingService scheduling,
    WordPuzzleService wordPuzzles,
    ServerService serverService,
    UserService userService,
    PronounService pronouns,
    MusicService music)
{
    private const ulong LogGuildId = 880569055856185354UL;
    private const ulong LogChannelId = 1156625507840954369UL;
    private const ulong OwnerId = 171369791486033920UL;
    private int _schedulerTick = 0;
    private Task? _schedulerTask;

    // Tracks messages we're waiting on Discord's own link crawler to embed,
    // keyed by message ID, so the /fixembed fallback only fires when Discord's
    // native embed genuinely failed to produce media.
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _pendingEmbedWatches = new();
    private static readonly TimeSpan NativeEmbedWaitTimeout = TimeSpan.FromSeconds(5);

    // ── Per-channel puzzle hint state ──────────────────────────────────────────
    // Shared between the scheduler (creation), T+30/T+50 reveal tasks, and
    // OnMessageReceivedAsync (guess tracking) so all reveals accumulate correctly.

    /// <summary>
    /// Tracks which letters of a bonus word puzzle have been revealed so far, and how many
    /// guesses have been made. One instance lives per active puzzle channel; the scheduler's
    /// T+30/T+50 reveal tasks and the every-20-guesses check in OnMessageReceivedAsync all
    /// read/write the same instance so hints only ever accumulate, never reset.
    /// </summary>
    private sealed class PuzzleHintState
    {
        public readonly string Word;
        public readonly IUserMessage Message;
        private readonly HashSet<int> _revealed = new();
        private int _guessCount;

        /// <summary>Creates the state for a new puzzle, with the first letter already revealed.</summary>
        public PuzzleHintState(string word, IUserMessage msg)
        {
            Word = word;
            Message = msg;
            _revealed.Add(0); // first letter shown from the start
        }

        /// <summary>
        /// Tries to reveal one new unrevealed letter.
        /// Returns true and sets <paramref name="hint"/> to the updated string when
        /// a new letter was revealed; returns false when all letters are already shown.
        /// </summary>
        public bool TryRevealNext(out string hint)
        {
            lock (_revealed)
            {
                // Pick uniformly at random among letters not yet shown, so reveals
                // don't always proceed left-to-right.
                var available = Enumerable.Range(1, Word.Length - 1)
                    .Where(i => !_revealed.Contains(i))
                    .ToList();

                if (available.Count == 0)
                {
                    hint = BuildHint();
                    return false;
                }

                int idx = available[Random.Shared.Next(available.Count)];
                _revealed.Add(idx);
                hint = BuildHint();
                return true;
            }
        }

        /// <summary>Returns the hint string reflecting whatever letters are currently revealed.</summary>
        public string GetCurrentHint()
        {
            lock (_revealed) return BuildHint();
        }

        /// <summary>Atomically increments and returns the new total guess count.</summary>
        public int IncrementGuesses() => Interlocked.Increment(ref _guessCount);

        /// <summary>Renders the word as underscores with only the revealed letters filled in.</summary>
        private string BuildHint()
        {
            char[] chars = new string('_', Word.Length).ToCharArray();
            foreach (int i in _revealed) chars[i] = Word[i];
            return new string(chars);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PuzzleHintState>
        _puzzleHintStates = new();

    private readonly EmbedHelper _embed = new();


    /// <summary>
    /// Starts the bot: initializes slash-command registration, wires up every gateway
    /// event handler, kicks off the background scheduler, then connects to Discord and
    /// blocks forever (the process exits only via host shutdown).
    /// </summary>
    public async Task RunAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        RegisterEvents();
        _schedulerTask = RunSchedulerAsync();
        await ConnectAsync();
        await Task.Delay(Timeout.Infinite);
    }

    /// <summary>Logs in and starts the Discord gateway connection; on failure, hands off to the reconnect loop.</summary>
    private async Task ConnectAsync()
    {
        try
        {
            await logger.InfoAsync("Starting Bot");
            await client.LoginAsync(TokenType.Bot, Constants.botToken);
            await client.StartAsync();
        }
        catch (Exception ex)
        {
            await ReconnectAsync(ex);
        }
    }

    /// <summary>Logs the failure, logs out to clear any half-open session, waits, then retries the connection.</summary>
    private async Task ReconnectAsync(Exception ex)
    {
        await logger.InfoAsync($"{ex.GetType().Name}: {ex.Message}");
        try { await client.LogoutAsync(); } catch { /* ignore */ }
        await Task.Delay(TimeSpan.FromSeconds(5));
        await ConnectAsync();
    }

    /// <summary>
    /// Subscribes to every Discord gateway event this bot reacts to. Each subscription below
    /// is event-driven — the corresponding handler fires whenever Discord.NET raises that
    /// event, not on any fixed schedule.
    /// </summary>
    private void RegisterEvents()
    {
        client.Connected += OnConnectedAsync;                      // gateway connection established
        client.Disconnected += OnDisconnectedAsync;                 // gateway connection dropped
        client.Log += OnLogMessageAsync;                            // Discord.NET internal log/exception messages
        client.JoinedGuild += OnJoinedGuildAsync;                   // bot added to a new server
        client.UserJoined += OnUserJoinedAsync;                     // member joined a server the bot is in
        client.UserLeft += OnUserLeftAsync;                         // member left/was removed from a server
        client.ButtonExecuted += OnButtonExecutedAsync;             // component (button) interaction, e.g. pronoun roles
        client.MessageReceived += OnMessageReceivedAsync;           // any message posted in a visible channel or DM
        client.MessageUpdated += OnMessageUpdatedAsync;             // message edited (used to detect late native embeds)
        client.ReactionAdded += OnReactionAddedAsync;                // emoji reaction added, e.g. trivia answers, NSFW flagging
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync; // voice channel join/leave/move
    }


    /// <summary>
    /// Fires each time the gateway connection is (re-)established. Sets the bot's status
    /// and restarts the scheduler if it died while disconnected.
    /// </summary>
    private async Task OnConnectedAsync()
    {
        await logger.InfoAsync("Bot connected");
        await client.SetGameAsync("/reportbug");

        // Restart scheduler if it died while Discord was disconnected.
        if (_schedulerTask is null || _schedulerTask.IsCompleted)
        {
            await logger.InfoAsync("[Scheduler] Restarting scheduler loop after reconnect.");
            _schedulerTask = RunSchedulerAsync();
        }
    }

    /// <summary>Fires when the gateway connection drops; just logs — reconnection is handled by Discord.NET/ConnectAsync.</summary>
    private async Task OnDisconnectedAsync(Exception ex) =>
        await logger.InfoAsync($"Bot disconnected ({client.ConnectionState}): {ex.Message}");

    /// <summary>
    /// Fires on every Discord.NET internal log line. Filters down to genuine exceptions
    /// (ignoring routine reconnect exceptions and non-exception log noise) and forwards
    /// those to the owner's private log channel.
    /// </summary>
    private async Task OnLogMessageAsync(LogMessage msg)
    {
        if (msg.Exception is null || msg.Message.Length == 0) return; // not an exception — nothing to report
        if (msg.Exception is Discord.WebSocket.GatewayReconnectException) return; // routine, expected — don't spam the log channel

        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;

        await channel.SendMessageAsync(embed: _embed.BuildMessageEmbed(
            "Exception Thrown",
            $"Exception: {msg.Exception.Message}\nMessage: {msg.Message}",
            "", "BigBirdBot", Color.Red).Build());
    }


    /// <summary>Fires when a member leaves (or is removed from) a guild: purges their DB row and audits the departure.</summary>
    private async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        if (user.IsBot || user.IsWebhook) return; // bots/webhooks aren't tracked in the user table

        await userService.DeleteUserAsync(user.Id.ToString(), guild.Id);
        await audit.InsertUserLeftAuditAsync(user.Id, guild.Id);
    }

    /// <summary>Fires when a member joins a guild: records them in the DB, audits the join, and assigns the guild's auto-role (if configured).</summary>
    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot || user.IsWebhook) return; // bots/webhooks aren't tracked in the user table
        await AddUserToDatabaseAsync(user, user.Guild.Id);
        await audit.InsertUserJoinedAuditAsync(user.Id, user.Guild.Id);
        await AssignAutoRoleAsync(user);
    }

    /// <summary>Grants the guild's configured auto-role to a newly-joined member, if one is set up.</summary>
    private async Task AssignAutoRoleAsync(SocketGuildUser user)
    {
        ulong? roleId = await autoRoles.GetRoleIdAsync(user.Guild.Id);
        if (roleId is null) return; // no auto-role configured for this guild

        var role = user.Guild.GetRole(roleId.Value);
        if (role is null) return; // role was deleted since being configured

        try { await user.AddRoleAsync(role); }
        catch { /* role may have been deleted or bot lacks permission */ }
    }

    /// <summary>
    /// Fires when the bot is added to a new guild (or when a full gateway reconnect
    /// repopulates an empty guild cache, which makes already-joined guilds look "new").
    /// Immediately hands off to a background task and returns, since the handler body
    /// below awaits <see cref="IGuild.DownloadUsersAsync"/> — that call needs the gateway
    /// receive loop to keep processing incoming GUILD_MEMBERS_CHUNK payloads to complete,
    /// so running it inline here would block that same receive loop on itself, starving
    /// heartbeat ACK processing and tripping "Server missed last heartbeat" disconnects.
    /// </summary>
    private Task OnJoinedGuildAsync(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            try { await ProcessJoinedGuildAsync(guild); }
            catch (Exception ex) { await logger.InfoAsync($"[JoinedGuild] Handler failed for {guild.Name}: {ex.Message}"); }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers the server in the DB (if not already present), downloads the member
    /// list, and backfills every existing member into the user table.
    /// </summary>
    private async Task ProcessJoinedGuildAsync(SocketGuild guild)
    {
        await audit.InsertGuildJoinedAuditAsync(guild.Id, guild.Name);

        // Build the set of server IDs we already know about, so re-adding the bot to a
        // guild it was previously in doesn't insert a duplicate row.
        var existingIds = (await serverService.GetActiveServersAsync())
            .Select(s => s.ServerUid.ToString())
            .ToHashSet();

        if (!existingIds.Contains(guild.Id.ToString()))
            await serverService.AddServerAsync(guild.Id, guild.Name, guild.DefaultChannel.Id);

        await guild.DownloadUsersAsync();

        if (guild.Users.Count == 0)
        {
            // DownloadUsersAsync can silently return nothing if the gateway member-list
            // request fails; flag it rather than proceeding as if the guild were empty.
            await SendLogAsync(
                $"Bot joined **{guild.Name}** but DownloadUsersAsync returned 0 users. Owner: {guild.Owner}",
                Color.Red);
            return;
        }

        // Backfill every existing human member — new joins after this point are
        // handled individually by OnUserJoinedAsync.
        foreach (var user in guild.Users.Where(u => !u.IsBot && !u.IsWebhook))
            await AddUserToDatabaseAsync(user, guild.Id);

        await logger.InfoAsync($"{guild.Users.Count} users added for {guild.Name}");
    }

    /// <summary>Registers a single member's row in the user table, if they aren't already known for this server.</summary>
    private Task AddUserToDatabaseAsync(SocketGuildUser user, ulong guildId) =>
        userService.AddUserIfMissingAsync(user.Id.ToString(), user.Username, user.JoinedAt?.UtcDateTime ?? DateTime.UtcNow, guildId, user.Nickname);


    /// <summary>
    /// Fires on every button click. Only handles pronoun-role buttons (identified by a
    /// plain numeric custom ID with no <c>_</c> or <c>:</c> — those separators mark buttons
    /// owned by other features). Toggles the matching pronoun role on the clicking user,
    /// creating the role on the guild if it doesn't exist yet.
    /// </summary>
    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        // Not a pronoun-button ID — another feature owns this button.
        if (component.Data.CustomId.Contains('_') || component.Data.CustomId.Contains(':'))
            return;

        var pronounList = await pronouns.GetAllAsync();
        string pronounSelected = "";
        var guild = client.GetGuild(component.GuildId!.Value);

        foreach (var (id, name) in pronounList)
        {
            // Lazily create the pronoun role on this guild the first time it's needed.
            if (!guild.Roles.Any(r => r.Name == name))
                await guild.CreateRoleAsync(name);

            if (id.ToString() == component.Data.CustomId)
                pronounSelected = name;
        }

        guild = client.GetGuild(component.GuildId!.Value); // re-fetch: role list above may have just changed
        var role = guild.Roles.FirstOrDefault(r => r.Name == pronounSelected);
        var guildUser = guild.GetUser(component.User.Id);

        if (role is null) return;

        bool hasRole = guildUser.Roles.Any(r => r.Name == role.Name);

        // Toggle: remove the role if the user already has it, otherwise add it.
        if (hasRole)
            await ((IGuildUser)guildUser).RemoveRoleAsync(role);
        else
            await ((IGuildUser)guildUser).AddRoleAsync(role);

        string action = hasRole ? "removed" : "added";
        await audit.InsertButtonAuditAsync(
            $"{pronounSelected} {action}",
            component.User.Id,
            component.GuildId!.Value);
        await component.RespondAsync(
            embed: _embed.BuildMessageEmbed(
                "Pronoun Selection",
                $"Pronouns were successfully {action} for {component.User.Username}.",
                "", component.User.Username, Color.Blue).Build(),
            ephemeral: true);
    }


    /// <summary>
    /// Gateway hook for <see cref="DiscordSocketClient.MessageReceived"/>. Does only the
    /// cheap synchronous filtering, then hands off to <see cref="ProcessMessageAsync"/> on a
    /// background task and returns immediately — a slow branch (DB round-trips, REST calls,
    /// the up-to-5s native-embed wait) must never block the gateway.
    /// </summary>
    private Task OnMessageReceivedAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg.Author.IsWebhook) return Task.CompletedTask; // never react to bots/webhooks (avoids feedback loops)
        if (msg.Channel is not SocketGuildChannel) return Task.CompletedTask;    // DMs and non-guild channels — nothing to do

        _ = Task.Run(() => ProcessMessageAsync(msg));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Central message router — runs off the gateway task. Order matters: each branch below
    /// returns as soon as it claims the message, so the social-media-embed fixer, the "-"
    /// keyword prefix, and the bonus word puzzle are mutually exclusive per message. Because
    /// this is fire-and-forget, everything is wrapped so exceptions reach the log rather than
    /// going unobserved.
    /// </summary>
    private async Task ProcessMessageAsync(SocketMessage msg)
    {
        try
        {
            var msgChannel = (SocketGuildChannel)msg.Channel;

            string message = msg.Content.Trim().ToLowerInvariant();
            ulong serverId = msgChannel.Guild.Id;
            ulong userId = msg.Author.Id;
            const string prefix = "-"; // marks a keyword-add/lookup command, e.g. "-cat http://..."

            await userService.UpdateLastSeenAsync(userId.ToString(), serverId);

            var serverInfo = await serverService.GetServerInfoAsync(serverId);

            // Server-wide kill switch — if the server record is missing/inactive, skip all
            // further processing (no keyword triggers, no word puzzle) for this message.
            if (serverInfo is null || !serverInfo.IsActive)
                return;

            var cleanup = new URLCleanup();

            // A raw social-media link (Twitter/X, Bluesky, etc.) whose native Discord embed may
            // be broken, and that isn't itself a "-" keyword command — try to fix its embed.
            if (cleanup.HasSocialMediaEmbed(message) && !message.StartsWith(prefix))
            {
                bool fix = await serverService.GetEmbedFixEnabledAsync(serverId);

                // Only step in if this server opted into the fix AND Discord's own crawler
                // didn't manage to attach a rich embed on its own within the wait window.
                if (fix && !await DiscordEmbedSucceededAsync(msg.Id))
                {
                    await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));
                }

                return;
            }

            // "-keyword ..." — add or manage a chat-triggered keyword; handled entirely elsewhere.
            if (message.StartsWith(prefix))
            {
                await HandlePrefixCommandAsync(msg, message, prefix, cleanup);
                return;
            }


            var petPuzzle = await wordPuzzles.GetActivePuzzleAsync(msg.Channel.Id.ToString());

            // A bonus word puzzle (posted hourly by the scheduler) is active in this channel.
            if (petPuzzle is not null)
            {
                string puzzleWord = petPuzzle.Word;
                int puzzleId = petPuzzle.PuzzleId;

                string puzzleChannelId = msg.Channel.Id.ToString();

                if (string.Equals(message.Trim(), puzzleWord, StringComparison.OrdinalIgnoreCase))
                {
                    // Clean up shared hint state for this channel
                    _puzzleHintStates.TryRemove(puzzleChannelId, out _);

                    // Only the first correct guess claims the puzzle; a concurrent second
                    // correct guess (now possible since handlers run in parallel) gets
                    // false back and stays silent instead of double-announcing.
                    if (await wordPuzzles.ClaimPuzzleAsync(puzzleId))
                    {
                        await audit.InsertGameTriggerAuditAsync("petpuzzle", userId, serverId);

                        await msg.Channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "🧩  Puzzle Solved!",
                            $"{msg.Author.Mention} solved the bonus word puzzle! 🎉",
                            Color.Green).Build());
                    }

                    return;
                }

                // ── Every-20-guesses letter reveal ────────────────────────────────
                // Count any single-word alphabetic attempt (wrong answers only —
                // correct answers are handled and returned above).
                string trimmedGuess = message.Trim();
                bool isWordAttempt  = trimmedGuess.Length > 0 && trimmedGuess.All(char.IsLetter);

                // Only channels with live hint-tracking state (i.e. the puzzle was posted by
                // the scheduler, not left over some other way) accumulate reveals.
                if (isWordAttempt && _puzzleHintStates.TryGetValue(puzzleChannelId, out var guessState))
                {
                    int totalGuesses = guessState.IncrementGuesses();
                    // Reveal one more letter every 20 wrong guesses, on top of the scheduler's
                    // own T+30/T+50 timed reveals — whichever fires first wins for that letter.
                    if (totalGuesses % 20 == 0 && guessState.TryRevealNext(out string guessHint))
                    {
                        try
                        {
                            await guessState.Message.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Bonus Word Puzzle!",
                                $"Type the secret word in this channel.\n\n" +
                                $"**Hint:** `{guessHint}`  ({guessState.Word.Length} letters)\n" +
                                $"*(A letter was revealed after {totalGuesses} guesses!)*\n\n" +
                                $"⏳ First correct answer wins!",
                                new Color(255, 179, 71)).Build());
                        }
                        catch { /* message may have been deleted */ }
                    }
                }
            }


            // Fall-through: check whether this message contains a registered chat-triggered
            // keyword (e.g. auto-replying with a saved image/link).
            if (await keywords.ResolveChatActionAsync(msgChannel.Guild.Id, message) is { } action)
                await SendChatActionAsync(msg, msgChannel, action);
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync(ex);
        }
    }

    /// <summary>
    /// Waits briefly to see whether Discord's own link crawler attaches a rich
    /// embed (image/video) to <paramref name="messageId"/> on its own. Returns
    /// true if it did, so callers can skip posting a redundant fixed-link message.
    /// </summary>
    private async Task<bool> DiscordEmbedSucceededAsync(ulong messageId)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingEmbedWatches.TryAdd(messageId, tcs))
            return false;

        try
        {
            var winner = await Task.WhenAny(tcs.Task, Task.Delay(NativeEmbedWaitTimeout));
            return winner == tcs.Task && await tcs.Task;
        }
        finally
        {
            _pendingEmbedWatches.TryRemove(messageId, out _);
        }
    }

    /// <summary>
    /// Fires when a message is edited. Used only to detect that Discord's own link crawler
    /// finally attached a rich embed to a message <see cref="DiscordEmbedSucceededAsync"/>
    /// is currently waiting on, so that waiter can resolve immediately instead of timing out.
    /// </summary>
    private Task OnMessageUpdatedAsync(
        Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
    {
        if (_pendingEmbedWatches.TryGetValue(after.Id, out var tcs) &&
            after.Embeds.Any(e => e.Image is not null || e.Video is not null))
        {
            tcs.TrySetResult(true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a "-keyword ..." message: registers an attachment, single URL, or comma-
    /// separated list of URLs against a chat-triggered keyword so it can later be replayed
    /// by <see cref="SendChatActionAsync"/>.
    /// </summary>
    private async Task HandlePrefixCommandAsync(
        SocketMessage msg, string message, string prefix, URLCleanup cleanup)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string keyword = parts[0][prefix.Length..];
        string? mappedKeyword = await keywords.ResolveAddKeywordAsync(keyword);

        if (mappedKeyword is null) return; // not a registered keyword — ignore silently

        if (msg.Attachments.Count > 0)
        {
            await AddAttachmentsAsync(msg, mappedKeyword);
            await msg.Channel.SendMessageAsync(
                embed: _embed.BuildSimpleEmbed("Added Image", "Added attachment(s) successfully.", Color.Blue).Build());
        }

        if (parts.Length <= 1) return; // attachment-only message with no URL/text argument — nothing more to add

        string content = message[(prefix.Length + keyword.Length)..].Trim();
        bool isMultiUrl = content.Contains(',') && content.Contains("http");

        if (isMultiUrl)
        {
            // Comma-separated list — register each URL as its own entry under the keyword.
            foreach (string url in content.Split(',', StringSplitOptions.TrimEntries))
            {
                if (!url.StartsWith("http"))
                {
                    await msg.Channel.SendMessageAsync(
                        embed: _embed.BuildSimpleEmbed("Error", $"Invalid URL: *{url}*", Color.Red).Build());
                    continue;
                }

                string storeValue = await TrySaveSocialImageAsync(url, mappedKeyword)
                                    ?? cleanup.CleanURLEmbed(url);
                await keywords.AddEntryAsync(mappedKeyword, storeValue);
            }

            await msg.Channel.SendMessageAsync(
                embed: _embed.BuildSimpleEmbed("Added Image", "Added link(s) successfully.", Color.Blue).Build());
        }
        else
        {
            string storeValue = await TrySaveSocialImageAsync(content, mappedKeyword)
                                ?? cleanup.CleanURLEmbed(content);

            await keywords.AddEntryAsync(mappedKeyword, storeValue);

            // Locally-downloaded social media images get a different confirmation message
            // than a plain URL/text entry.
            string confirmation = KeywordFiles.IsLocalFile(storeValue)
                ? "Image downloaded and saved locally."
                : "Added URL/Text successfully.";

            await msg.Channel.SendMessageAsync(
                embed: _embed.BuildSimpleEmbed("Added URL/Text", confirmation, Color.Blue).Build());
        }
    }

    /// <summary>
    /// Replays one registered response for a matched chat keyword: a locally-stored file,
    /// a live URL, or plain text. Dead links are auto-removed from the keyword's URL list;
    /// posted images/links get a ❌ reaction so any member can delete them (see
    /// <see cref="OnReactionAddedAsync"/>).
    /// </summary>
    private async Task SendChatActionAsync(SocketMessage msg, SocketGuildChannel msgChannel, ChatActionEntry action)
    {
        if (client.GetChannel(msgChannel.Id) is not IMessageChannel sender) return;

        string chatAction = action.FilePath;
        bool isNsfw = action.Nsfw;

        if (string.IsNullOrWhiteSpace(chatAction)) return;

        await msg.Channel.TriggerTypingAsync();

        string keyword = char.ToUpperInvariant(action.Keyword[0]) + action.Keyword[1..];

        // Three possible stored shapes for a keyword's value: a local file, a live URL,
        // or plain text — each is delivered differently.
        if (KeywordFiles.IsLocalFile(chatAction))
        {
            string localPath = KeywordFiles.Resolve(chatAction);

            if (!File.Exists(localPath))
            {
                // File was moved/deleted since it was registered — drop the entry like a dead link.
                await keywords.DeleteEntryByIdAsync(action.Id);
                await sender.SendMessageAsync($"File was missing so I removed that entry :) -> {Path.GetFileName(localPath)}");
                return;
            }

            string fileName = Path.GetFileName(localPath);
            bool isSpoiler = isNsfw && !fileName.Contains("SPOILER_");
            var embed = new EmbedBuilder()
                .WithTitle(keyword)
                .WithImageUrl("attachment://" + fileName)
                .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                .Build();

            await using var stream = File.OpenRead(localPath);
            var output = await msg.Channel.SendFileAsync(
                stream, fileName, embed: embed, isSpoiler: isSpoiler);

            if (!isSpoiler) // spoilered NSFW content isn't tagged for deletion — it's already hidden
                await output.AddReactionAsync(new Emoji("❌"));
        }
        else if (chatAction.Contains("http"))
        {
            if (await IsLinkWorkingAsync(chatAction))
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
                // Link is dead — remove it from the keyword's rotation so it isn't served again.
                await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {chatAction}");
                await keywords.DeleteEntryByIdAsync(action.Id);
            }
        }
        else
        {
            string display = isNsfw ? $"||{chatAction}||" : chatAction; // Discord spoiler markup
            var output = await sender.SendMessageAsync(display);
            if (!isNsfw)
                await output.AddReactionAsync(new Emoji("❌"));
        }
    }


    /// <summary>
    /// Fires when any user's voice state changes (join/leave/move). Its only job is cleanup:
    /// when the last human leaves a voice channel the bot is in, disconnect the bot and clear
    /// its music queue; if the bot itself gets disconnected, clear the "player connected" row too.
    /// </summary>
    private async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var guild = before.VoiceChannel?.Guild ?? after.VoiceChannel?.Guild;
        if (guild is null) return;

        // Local helper: forces every bot user out of a voice channel (used both when the
        // music bot itself disconnects and when the last human leaves).
        async Task DisconnectBotsAsync(SocketVoiceChannel channel)
        {
            foreach (var bot in channel.ConnectedUsers.Where(u => u.IsBot))
                await bot.VoiceChannel.DisconnectAsync();
        }

        if (user.IsBot)
        {
            // The bot itself left a channel — clear its "connected" DB row so future
            // commands know no player is active for this server.
            if (after.VoiceChannel is null && before.VoiceChannel is not null)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                await music.DeletePlayerConnectedAsync(guild.Id);
            }
        }
        else if (before.VoiceChannel is not null && after.VoiceChannel is null)
        {
            // A human left a channel — if no humans remain, there's no one to listen,
            // so disconnect the bot and clear the queue rather than playing to an empty room.
            bool anyNonBotRemaining = before.VoiceChannel.ConnectedUsers.Any(u => !u.IsBot);
            if (!anyNonBotRemaining)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                await music.DeletePlayerConnectedAsync(guild.Id);
                await music.ClearQueueAsync(guild.Id);
            }
        }
    }


    /// <summary>
    /// Fires on every reaction added anywhere the bot can see. A ❌ on one of the bot's own
    /// image posts deletes/NSFW-flags it. Runs as fire-and-forget so a slow download/DB call
    /// never blocks the gateway event loop.
    /// </summary>
    private Task OnReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction)
    {
        _ = Task.Run(async () =>
        {
            var download = await cachedMsg.GetOrDownloadAsync();
            if (download is null) return; // message was deleted before we could fetch it
            if (client.GetUser(reaction.UserId)?.IsBot == true) return; // ignore the bot's own reactions

            var imageUrl = download.Embeds.FirstOrDefault(e => e.Image.HasValue)?.Image?.Url;

            // ❌ on one of the bot's own posts, with fewer than 2 reactions so far (i.e. not
            // already actioned) — treat it as a "delete/flag this" request from a member.
            if (reaction.Emote.Name == "❌" && download.Author.IsBot && download.Reactions.Count < 2)
            {
                string? fileName = imageUrl is not null
                    ? Path.GetFileName(new Uri(imageUrl).LocalPath)
                    : null;

                if (!string.IsNullOrEmpty(fileName))
                {
                    await audit.InsertReactionAuditAsync(
                        reaction.Emote.Name,
                        download.Id,
                        reaction.UserId,
                        cachedChannel.Id);
                    await TryMarkNsfwAsync(fileName, cachedChannel, reaction);
                    return;
                }
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>Flags a chat-keyword file as NSFW the first time it's ❌-reacted, if it isn't already marked.</summary>
    private async Task TryMarkNsfwAsync(
        string content,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (await keywords.GetNsfwAsync(content) == true) return; // already flagged — nothing to do

        if (await keywords.MarkNsfwAsync(content))
        {
            await channel.Value.SendMessageAsync(embed: _embed.BuildMessageEmbed(
                "NSFW",
                $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)",
                "", "BigBirdBot", Color.Blue).Build());
        }
    }

    /// <summary>
    /// Resolves a guild's configured default text channel by its stored ID string.
    /// Returns null if the ID is missing/malformed or the channel no longer exists —
    /// callers treat either case as "nowhere to announce, skip." Shared by the several
    /// scheduler blocks below that each announce to a guild's default channel.
    /// </summary>
    private static ITextChannel? ResolveAnnouncementChannel(SocketGuild guild, string? defaultChannelId) =>
        ulong.TryParse(defaultChannelId, out ulong channelId) && channelId != 0
            ? guild.GetTextChannel(channelId)
            : null;


    /// <summary>
    /// Background loop that drives every time-based feature: reminders and birthday
    /// greetings (every tick), the hourly bonus word puzzle, and scheduled keyword
    /// deliveries. Runs for the lifetime of the process; <see cref="OnConnectedAsync"/>
    /// restarts it if it ever dies while disconnected.
    /// </summary>
    private async Task RunSchedulerAsync()
    {
        // Wait until the top of the next minute so every tick lands on a clock minute
        var now = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(60 - now.Second));

        // Seed the counter to the current UTC minute so modulo checks align to real
        // clock boundaries: % 15 → :00/:15/:30/:45, % 30 → :00/:30, % 60 → :00
        _schedulerTick = DateTime.UtcNow.Minute;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync())
        {
            _schedulerTick++;

            // ── Single outer try-catch: any unhandled exception in any block
            //    logs to the owner and lets the loop continue next tick. ──────
            try
            {
                await RunScheduledKeywordsAsync();

                // ── Reminders (every tick = every minute) ───────────────────
                var dueReminders = await scheduling.GetDueRemindersAsync();
                foreach (var reminderRow in dueReminders)
                {
                    try
                    {
                        ulong userId = ulong.Parse(reminderRow.UserId);
                        string message = reminderRow.Message;
                        var reminderUser = await client.GetUserAsync(userId)
                                        ?? await client.Rest.GetUserAsync(userId);
                        if (reminderUser is null) continue;

                        var dm = await reminderUser.CreateDMChannelAsync();
                        await dm.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "⏰  Reminder", message, Color.Gold,
                            footer: "You asked me to remind you at this time.").Build());
                    }
                    catch { /* DMs disabled or user not found */ }
                }

                // ── Birthdays (every tick = every minute) ────────────────────
                var todaysBirthdays = await scheduling.GetTodaysBirthdaysAsync();
                foreach (var birthdayRow in todaysBirthdays)
                {
                    try
                    {
                        string mention = birthdayRow.Mention;
                        ulong guildId = ulong.Parse(birthdayRow.GuildId);
                        string? overrideChannelId = birthdayRow.ChannelId;

                        var birthdayGuild = client.GetGuild(guildId);
                        if (birthdayGuild is null) continue;

                        ITextChannel? channel = null;

                        if (!string.IsNullOrWhiteSpace(overrideChannelId) && ulong.TryParse(overrideChannelId, out ulong overrideId))
                            channel = birthdayGuild.GetTextChannel(overrideId);

                        if (channel is null)
                        {
                            var serverDetails = await serverService.GetServerInfoAsync(guildId);
                            if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;

                            channel = ResolveAnnouncementChannel(birthdayGuild, serverDetails.DefaultChannelId);
                        }

                        if (channel is null) continue;

                        await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "🎂  Happy Birthday!",
                            $"Everyone wish {mention} a very happy birthday! 🎉🎈",
                            new Color(255, 105, 180)).Build());
                    }
                    catch { /* guild/channel may no longer exist */ }
                }

            // Runs once every 60 minutes — posts a new bonus word puzzle to every eligible guild.
            if (_schedulerTick % 60 == 0)
            {
                // Pull a random word from the Words table
                string? puzzleWord = (await wordPuzzles.GetRandomWordAsync())?.Trim();
                if (string.IsNullOrWhiteSpace(puzzleWord)) goto skipPuzzle;

                foreach (var guild in client.Guilds)
                {
                    var serverDetails = await serverService.GetServerInfoAsync(guild.Id);
                    if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;
                    var channel = ResolveAnnouncementChannel(guild, serverDetails.DefaultChannelId);
                    if (channel is null) continue;

                    await wordPuzzles.AddPuzzleAsync(serverDetails.DefaultChannelId, puzzleWord, DateTime.UtcNow.AddMinutes(55));

                    string blankHint = $"{puzzleWord[0]}{new string('_', puzzleWord.Length - 1)}";

                    var puzzleMsg = await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        "🧩  Bonus Word Puzzle!",
                        $"Type the secret word in this channel.\n\n" +
                        $"**Hint:** `{blankHint}`  ({puzzleWord.Length} letters)\n\n" +
                        $"⏳ Expires in 55 minutes — first correct answer wins!",
                        new Color(255, 179, 71)).Build());

                    // Register shared hint state — all reveal sources (T+30, T+50, every-20-guesses)
                    // accumulate into this so the hint only ever grows, never resets.
                    string capturedChannelId = serverDetails.DefaultChannelId;
                    var hintState = new PuzzleHintState(puzzleWord, puzzleMsg);
                    _puzzleHintStates[capturedChannelId] = hintState;

                    var capturedMsg  = puzzleMsg;
                    var capturedWord = puzzleWord;
                    var capturedCh   = channel;

                    // ── 30-min hint: reveal a second letter ──────────────────
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30));

                        var stillActive = await wordPuzzles.GetActivePuzzleAsync(capturedCh.Id.ToString());
                        if (stillActive is null) return;

                        if (!hintState.TryRevealNext(out string hint30)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Bonus Word Puzzle!",
                                $"Type the secret word in this channel.\n\n" +
                                $"**Hint:** `{hint30}`  ({capturedWord.Length} letters)\n" +
                                $"*(A letter has been revealed!)*\n\n" +
                                $"⏳ Expires in ~25 minutes — first correct answer wins!",
                                new Color(255, 179, 71)).Build());
                        }
                        catch { /* message may have been deleted */ }
                    });

                    // ── 50-min hint: reveal a third letter (5-min warning) ───
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(50));

                        var stillActive = await wordPuzzles.GetActivePuzzleAsync(capturedCh.Id.ToString());
                        if (stillActive is null) return;

                        if (!hintState.TryRevealNext(out string hint50)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Bonus Word Puzzle — Last Chance!",
                                $"Type the secret word in this channel.\n\n" +
                                $"**Hint:** `{hint50}`  ({capturedWord.Length} letters)\n" +
                                $"*(Another letter has been revealed!)*\n\n" +
                                $"⏳ Only **5 minutes** left — first correct answer wins!",
                                new Color(255, 120, 40)).Build());
                        }
                        catch { /* message may have been deleted */ }
                    });

                    // ── 55-min reveal: show the answer when the puzzle expires ─
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(55));

                        _puzzleHintStates.TryRemove(capturedChannelId, out _);

                        // GetActivePuzzleAsync filters ExpiresAt > NOW — use the claimed-status
                        // lookup instead so we can distinguish solved vs expired-unsolved.
                        bool? wasClaimed = await wordPuzzles.GetClaimedStatusAsync(capturedCh.Id.ToString());
                        if (wasClaimed == true)
                            return;

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Puzzle Expired — No One Got It!",
                                $"Time's up! Nobody guessed the word.\n\n" +
                                $"The answer was: **{capturedWord}**\n\n" +
                                $"Better luck next time! 🕐",
                                new Color(150, 150, 150)).Build());
                        }
                        catch { /* message may have been deleted */ }
                    });
                }

                skipPuzzle:;
            }

            // Twice a day — drop ChatKeyword rows whose local file has gone missing and
            // log any files left orphaned in a keyword folder (see KeywordMaintenanceService).
            if (_schedulerTick % 720 == 0)
                await keywordMaintenance.ReconcileAsync();
            } // end outer try
            catch (Exception ex)
            {
                await NotifyOwnerAsync($"[Scheduler] Tick {_schedulerTick} failed:\n{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Delivers each user's due "thirst" (scheduled DM keyword) send via DM: a local file
    /// (compressed first if over Discord's 8 MB limit), a live URL, or removes the entry if
    /// the link is dead. Failed sends are requeued for a minute later rather than dropped.
    /// </summary>
    private async Task RunScheduledKeywordsAsync()
    {
        IReadOnlyList<DueKeywordDelivery> due;
        try
        {
            due = await keywords.GetDueDeliveriesAsync();
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"[Keywords] lookup failed: {ex.Message}");
            return;
        }

        if (due.Count == 0) return;

        foreach (var delivery in due)
        {
            string userId = delivery.UserId;
            string filePath = delivery.FilePath;
            string tableName = char.ToUpperInvariant(delivery.Keyword[0]) + delivery.Keyword[1..];
            string timestamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET");

            try
            {
                // GetUserAsync only checks the socket cache; fall back to REST so
                // users who haven't recently interacted with the bot are still resolved.
                ulong uid = ulong.Parse(userId);
                IUser? user = client.GetUser(uid)
                           ?? (IUser?)await client.Rest.GetUserAsync(uid);

                if (user is null)
                {
                    await NotifyOwnerAsync($"[Keywords] Could not resolve user {userId} — skipping tick.");
                    continue;
                }

                // Stored value is either a local file or a URL — branch accordingly.
                if (KeywordFiles.IsLocalFile(filePath))
                {
                    string localPath = KeywordFiles.Resolve(filePath);
                    string localName = Path.GetFileName(localPath);

                    if (!File.Exists(localPath)) // file was moved/deleted since being registered
                    {
                        if (delivery.EntryId is { } missingId)
                            await keywords.DeleteEntryByIdAsync(missingId);
                        await keywords.RequeueScheduleAsync(userId);
                    }
                    else if (new FileInfo(localPath).Length > 8 * 1024 * 1024) // exceeds Discord's non-boosted upload limit
                    {
                        using var compressed = TryCompressImageUnder8Mb(localPath);
                        if (compressed is null)
                        {
                            await NotifyOwnerAsync($"[Keywords] Skipped {filePath} for user {userId} — file exceeds 8 MB Discord limit and could not be compressed.");
                        }
                        else
                        {
                            var fileEmbed = new EmbedBuilder()
                                .WithTitle(tableName)
                                .WithImageUrl("attachment://" + localName)
                                .WithColor(Color.Blue)
                                .WithFooter(timestamp)
                                .Build();
                            await user.SendFileAsync(compressed, localName, embed: fileEmbed);
                        }
                    }
                    else
                    {
                        var fileEmbed = new EmbedBuilder()
                            .WithTitle(tableName)
                            .WithImageUrl("attachment://" + localName)
                            .WithColor(Color.Blue)
                            .WithFooter(timestamp)
                            .Build();
                        await user.SendFileAsync(localPath, embed: fileEmbed);
                    }
                }

                else if (await IsLinkWorkingAsync(filePath))
                {
                    if (IsDirectImageUrl(filePath)) // embeddable image — build our own embed
                    {
                        var urlEmbed = new EmbedBuilder()
                            .WithTitle(tableName)
                            .WithUrl(filePath)
                            .WithImageUrl(filePath)
                            .WithColor(Color.Blue)
                            .WithFooter(timestamp)
                            .Build();
                        await user.SendMessageAsync(embed: urlEmbed);
                    }
                    else
                    {
                        // Let Discord natively unfurl social/video links (bsky, YouTube, etc.)
                        await user.SendMessageAsync($"**{tableName}** — {timestamp}\n{filePath}");
                    }
                }
                else // link no longer resolves — remove it rather than keep re-attempting a dead send
                {
                    if (delivery.EntryId is { } deadId)
                        await keywords.DeleteEntryByIdAsync(deadId);
                    var deadEmbed = new EmbedBuilder()
                        .WithTitle(tableName)
                        .WithColor(Color.Red)
                        .WithDescription($"~~{filePath}~~ — dead link removed.")
                        .WithFooter(timestamp)
                        .Build();
                    await user.SendMessageAsync(embed: deadEmbed);
                }
            }
            catch (HttpException ex)
            {
                await NotifyOwnerAsync(
                    $"DM failed for user {userId} — they may have DMs disabled.\n{ex.Message}");
            }
            catch (Exception ex)
            {
                await keywords.RequeueScheduleAsync(userId);
                await NotifyOwnerAsync(
                    $"Scheduled send failed for user {userId}.\n{ex.StackTrace}\n" +
                    $"Requeued for {DateTime.Now.AddMinutes(1):yyyy-MM-dd hh:mm tt}.");
            }
        }
    }


    /// <summary>
    /// For a recognized social-media URL (see <see cref="IsSupportedSocialUrl"/>), downloads
    /// the underlying image/video to local disk and returns its path — or the og:image/og:video
    /// meta tag's target if the URL itself isn't a direct media link. Returns null for anything
    /// unsupported or on any failure, so callers can fall back to storing the raw URL instead.
    /// </summary>
    private async Task<string?> TrySaveSocialImageAsync(string url, string keyword)
    {
        if (!IsSupportedSocialUrl(url)) return null;

        try
        {
            using var http = httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; BigBirdBot/1.0)");

            using var head = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            string? contentType = head.Content.Headers.ContentType?.MediaType;

            if (IsImageContentType(contentType)) // URL is already a direct image — download it as-is
                return await DownloadSocialImageAsync(http, url, keyword, ExtFromContentType(contentType!));

            // Not a direct image — fetch the page HTML and scrape its Open Graph tags instead.
            string html = await http.GetStringAsync(url);
            string? mediaUrl = ExtractOgTag(html, "og:image")
                            ?? ExtractOgTag(html, "og:video");

            if (mediaUrl is null) return null;

            string ext = Path.GetExtension(new Uri(mediaUrl).AbsolutePath).TrimStart('.');
            if (!IsSupportedExtension(ext)) return null;

            return await DownloadSocialImageAsync(http, mediaUrl, keyword, ext);
        }
        catch (Exception ex)
        {
            await logger.DebugAsync($"[TrySaveSocialImageAsync] {url} — {ex.Message}");
            return null;
        }
    }

    /// <summary>Downloads a media file to a keyword-specific folder under the bot's temp directory and returns the saved path.</summary>
    private async Task<string?> DownloadSocialImageAsync(
        HttpClient http, string imageUrl, string keyword, string ext)
    {
        if (!IsSupportedExtension(ext)) return null;

        string folder = keyword.Replace("KeywordMulti.", "");
        string fileName = $"social_{DateTime.Now:yyyyMMdd_HHmmssfffff}.{ext}";
        string dir = Path.Combine(Constants.keywordDirectory, folder);
        string fullPath = Path.Combine(dir, fileName);

        Directory.CreateDirectory(dir);

        var bytes = await http.GetByteArrayAsync(imageUrl);
        await File.WriteAllBytesAsync(fullPath, bytes);

        await logger.DebugAsync($"[SocialImage] Saved → {fullPath}");
        return KeywordFiles.ToStored(folder, fileName);
    }


    /// <summary>
    /// Iteratively re-encodes an image until it fits Discord's 8 MB upload limit: first by
    /// lowering JPEG quality in steps, then by shrinking dimensions once quality bottoms out
    /// (PNG/WebP skip straight to shrinking, since quality isn't meaningful for them here).
    /// Returns null if the format is unsupported or the image can't be shrunk further.
    /// </summary>
    private static MemoryStream? TryCompressImageUnder8Mb(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return null;

        const long limit = 8 * 1024 * 1024;
        var format = ext is ".png" ? SkiaSharp.SKEncodedImageFormat.Png : SkiaSharp.SKEncodedImageFormat.Jpeg;

        using var original = SkiaSharp.SKBitmap.Decode(filePath);
        if (original is null) return null;

        int width = original.Width;
        int height = original.Height;
        int quality = 85;

        while (true)
        {
            using var bitmap = original.Resize(new SkiaSharp.SKImageInfo(width, height), SkiaSharp.SKSamplingOptions.Default);
            if (bitmap is null) return null;

            using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(format, quality);

            if (encoded.Size <= limit)
            {
                var ms = new MemoryStream();
                encoded.SaveTo(ms);
                ms.Position = 0;
                return ms;
            }

            // Try reducing quality first (JPEG only), then scale down dimensions
            if (format == SkiaSharp.SKEncodedImageFormat.Jpeg && quality > 40)
            {
                quality -= 15;
            }
            else
            {
                width = (int)(width * 0.75);
                height = (int)(height * 0.75);
                quality = 85; // reset quality after a resize so the next pass starts from full quality again
                if (width < 100 || height < 100)
                    return null; // too small to shrink further — give up
            }
        }
    }

    /// <summary>True if the URL's path ends in a recognized image extension (ignoring any query string).</summary>
    private static bool IsDirectImageUrl(string url)
    {
        var path = url.Split('?')[0].ToLowerInvariant();
        return path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".png")
            || path.EndsWith(".gif") || path.EndsWith(".webp");
    }

    /// <summary>
    /// Checks whether a link still resolves. Only actually probes fx/vxtwitter mirror links
    /// (which are known to return a "post doesn't exist" page for deleted tweets); every other
    /// URL is assumed live without a network call, since not every host is checkable this way.
    /// </summary>
    private async Task<bool> IsLinkWorkingAsync(string url)
    {
        if (!url.Contains("fxtwitter") && !url.Contains("vxtwitter"))
            return true;

        try
        {
            using var http = httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var body = await http.GetStringAsync(url, cts.Token);
            return !body.Contains("post doesn't exist");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Downloads every attachment on a message and registers each as a value under the given keyword.</summary>
    private async Task AddAttachmentsAsync(SocketMessage msg, string tablename)
    {
        tablename = tablename.Replace("KeywordMulti.", "");

        foreach (var attachment in msg.Attachments)
        {
            string[] parts = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string fileName = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}.{parts[1]}";
            string absPath = Path.Combine(Constants.keywordDirectory, tablename, fileName);

            await keywords.AddEntryAsync(tablename, KeywordFiles.ToStored(tablename, fileName));

            using var http = httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(attachment.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            await File.WriteAllBytesAsync(absPath, bytes);
        }
    }


    /// <summary>Posts a message to the owner's private log channel, if it's still reachable.</summary>
    private async Task SendLogAsync(string message, Color color)
    {
        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;
        await channel.SendMessageAsync(embed: _embed
            .BuildMessageEmbed("Log", message, "", "BigBirdBot", color).Build());
    }

    /// <summary>Sends a plain DM to the bot owner — used for scheduler/keyword failures that need attention.</summary>
    private async Task NotifyOwnerAsync(string message)
    {
        var owner = await client.GetUserAsync(OwnerId);
        await owner.SendMessageAsync(message);
    }


    /// <summary>True for the specific social-media mirror hosts this bot knows how to scrape media from.</summary>
    private static bool IsSupportedSocialUrl(string url) =>
        url.Contains("dl.fxtwitter.com") || url.Contains("bskx.app");

    /// <summary>True if the HTTP Content-Type is one of the image formats this bot re-hosts.</summary>
    private static bool IsImageContentType(string? ct) =>
        ct is "image/png" or "image/gif" or "image/jpeg";

    /// <summary>Maps an image MIME type to the file extension used when saving it to disk.</summary>
    private static string ExtFromContentType(string ct) => ct switch
    {
        "image/png" => "png",
        "image/gif" => "gif",
        "image/jpeg" => "jpg",
        _ => ""
    };

    /// <summary>True if the extension is one of the image formats this bot re-hosts.</summary>
    private static bool IsSupportedExtension(string ext) =>
        ext.ToLowerInvariant() is "png" or "gif" or "jpeg" or "jpg";

    /// <summary>
    /// Extracts an Open Graph meta tag's content value (e.g. <c>og:image</c>) from raw HTML via
    /// string search rather than a full HTML parser, since we only need one attribute from a
    /// known tag shape. Returns null if the tag is missing or its content isn't a well-formed
    /// absolute URL.
    /// </summary>
    private static string? ExtractOgTag(string html, string property)
    {
        string marker = $"property=\"{property}\"";
        int idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null; // tag not present in this page

        // The content="..." attribute should appear shortly after the property attribute
        // within the same <meta> tag — cap the search window so a stray later match can't
        // be picked up.
        int searchEnd = Math.Min(idx + 300, html.Length);
        int cIdx = html.IndexOf("content=\"", idx, searchEnd - idx, StringComparison.OrdinalIgnoreCase);
        if (cIdx < 0) return null;

        int start = cIdx + "content=\"".Length;
        int end = html.IndexOf('"', start);
        if (end < 0) return null;

        string value = html[start..end];
        return Uri.IsWellFormedUriString(value, UriKind.Absolute) ? value : null;
    }
}