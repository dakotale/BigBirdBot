using System.Collections.Concurrent;
using System.Data;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using DiscordBot.Services;
using Fergun.Interactive;
using KillersLibrary.Services;
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
            LogLevel = LogSeverity.Verbose
        })
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton<CommandService>()
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
        // Postgres/EF Core (see Constants.postgresConnStr) — registered Scoped (AddDbContext's
        // default), NOT Singleton: this is a long-running bot process, and one shared DbContext across
        // every command would accumulate tracked entities and cause stale-data/concurrency
        // errors. A DI scope is created per Discord interaction in
        // InteractionHandlerService.HandleInteractionAsync so each command gets its own
        // fresh context, matching the per-request-scope pattern EF Core expects.
        .AddDbContext<DiscordbotContext>(options => options.UseNpgsql(Constants.postgresConnStr))
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
        .AddLogging(x => x.ClearProviders().SetMinimumLevel(LogLevel.Trace));


/// <summary>
/// Top-level orchestrator for the bot's lifetime: connects to Discord, wires up every
/// gateway event handler, and runs the background scheduler and stock-price timers.
/// Also hosts the message-based (non-slash-command) features — keyword triggers, mini
/// games (Scramble/Wordle/pet word puzzles), pronoun buttons, and NSFW/dead-link cleanup —
/// since these react to raw events rather than slash commands.
/// </summary>
internal sealed class BotHost(
    DiscordSocketClient client,
    LoggingService logger,
    IServiceProvider services,
    IHttpClientFactory httpClientFactory)
{
    private const ulong LogGuildId = 880569055856185354UL;
    private const ulong LogChannelId = 1156625507840954369UL;
    private const ulong OwnerId = 171369791486033920UL;
    private System.Timers.Timer? _stockTimer;
    private System.Timers.Timer? _stockDayResetTimer;
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

    private static readonly Dictionary<string, string> EmojiToLetter = new()
    {
        ["🇦"] = "A.",
        ["🇧"] = "B.",
        ["🇨"] = "C.",
        ["🇩"] = "D."
    };


    /// <summary>
    /// Starts the bot: initializes slash-command registration, wires up every gateway
    /// event handler, kicks off the background scheduler and stock-price timers, then
    /// connects to Discord and blocks forever (the process exits only via host shutdown).
    /// </summary>
    public async Task RunAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        RegisterEvents();
        _schedulerTask = RunSchedulerAsync();
        StartStockTimer();
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
    /// and restarts the scheduler/stock timers if they died while disconnected.
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

        // Restart stock timers if they stopped.
        if (_stockTimer is null || !_stockTimer.Enabled)
        {
            await logger.InfoAsync("[StockMarket] Restarting stock timers after reconnect.");
            _stockTimer?.Dispose();
            _stockDayResetTimer?.Dispose();
            StartStockTimer();
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

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        string userId = user.Id.ToString();

        // Source (DeleteUser) counts this user's rows across ALL servers before deleting;
        // if this was their only server, it also purges their global (not per-server)
        // UsersScheduledKeyword/BotAIMessage rows. Preserved exactly.
        int totalCount = await db.Users.CountAsync(u => u.UserId == userId);

        db.Users.RemoveRange(db.Users.Where(u => u.UserId == userId && u.ServerUid == (long)guild.Id));

        if (totalCount == 1)
        {
            // BUG FIX: UsersScheduledKeyword has no real primary key in the schema (verified —
            // just UserID/ChatKeyword/ScheduledDateTime columns, no unique constraint), but the
            // EF model declares HasKey(UserId, ChatKeyword) since something has to be configured
            // for Add() to work. A user CAN legitimately have 2+ rows sharing that "key" (nothing
            // prevents duplicate /keyword schedule add calls), so a tracked RemoveRange/SaveChanges
            // here throws DbUpdateConcurrencyException ("expected to affect 1 row(s), but actually
            // affected 0") whenever that happens. ExecuteDeleteAsync bypasses key-based tracking
            // entirely — it just runs DELETE ... WHERE UserID = @userId directly.
            await db.UsersScheduledKeywords.Where(u => u.UserId == userId).ExecuteDeleteAsync();
            db.BotAimessages.RemoveRange(db.BotAimessages.Where(m => m.UserId == userId));
        }

        await db.SaveChangesAsync();

        await AuditService.InsertUserLeftAuditAsync(db, userId, guild.Id.ToString());
    }

    /// <summary>Fires when a member joins a guild: records them in the DB, audits the join, and assigns the guild's auto-role (if configured).</summary>
    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot || user.IsWebhook) return; // bots/webhooks aren't tracked in the user table
        await AddUserToDatabase(user, user.Guild.Id);
        using (var auditScope = services.CreateScope())
        {
            var auditDb = auditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            await AuditService.InsertUserJoinedAuditAsync(auditDb, user.Id.ToString(), user.Guild.Id.ToString());
        }
        await AssignAutoRoleAsync(user);
    }

    /// <summary>Grants the guild's configured auto-role to a newly-joined member, if one is set up.</summary>
    private async Task AssignAutoRoleAsync(SocketGuildUser user)
    {
        // BotHost is a singleton, so it can't hold a scoped DbContext directly (that would
        // be a captive dependency living for the whole process). Create a short-lived scope
        // for just this lookup instead, same as InteractionHandlerService does per interaction.
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        var autoRole = await db.GuildAutoRoles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == (long)user.Guild.Id);

        if (autoRole is null) return; // no auto-role configured for this guild

        var role = user.Guild.GetRole((ulong)autoRole.RoleId);
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
        // Source layered a C#-side existence pre-check (GetServers, IsActive-filtered) in
        // front of AddServer's own IF NOT EXISTS guard (unfiltered by IsActive) — since the
        // proc's own guard is what actually determines whether a row gets inserted, the two
        // checks compose to a single "does a Servers row with this ServerUID exist" test,
        // replicated directly as that one check. The caller (OnJoinedGuildAsync) already runs
        // this whole method inside a background Task.Run to stay off the gateway thread, so no
        // additional Task.Run wrapping is needed here now that these calls are properly async.
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            await AuditService.InsertGuildJoinedAuditAsync(db, guild.Id.ToString(), guild.Name);
            bool exists = await db.Servers.AnyAsync(s => s.ServerUid == (long)guild.Id);
            if (!exists)
            {
                db.Servers.Add(new Server
                {
                    ServerUid = (long)guild.Id,
                    ServerName = guild.Name,
                    DefaultChannelId = (long)guild.DefaultChannel.Id,
                    Volume = 100,
                    FixEmbed = false,
                    IsPlayerConnected = false,
                    IsActive = true,
                    CreatedOn = DateTime.Now.ToUniversalTime()
                });
                await db.SaveChangesAsync();
            }
        }

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
            await AddUserToDatabase(user, guild.Id);

        await logger.InfoAsync($"{guild.Users.Count} users added for {guild.Name}");
    }

    /// <summary>Inserts a single member's row in the user table, if not already present.</summary>
    private async Task AddUserToDatabase(SocketGuildUser user, ulong guildId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        // Source's AddUser proc guards with IF NOT EXISTS (UserID, ServerUID) before inserting.
        bool exists = await db.Users.AnyAsync(x => x.UserId == user.Id.ToString() && x.ServerUid == (long)guildId);
        if (exists) return;

        // Source's @JoinDate param was a DateTimeOffset passed into a `datetime` column;
        // SQL Server's implicit datetimeoffset→datetime cast converts to UTC first —
        // .UtcDateTime matches that exactly (see OwnerCommands.HandlePopulateAllUserCommand,
        // which has the identical proc conversion).
        db.Users.Add(new User
        {
            UserId = user.Id.ToString(),
            Username = user.Username,
            JoinDate = user.JoinedAt!.Value.UtcDateTime,
            ServerUid = (long)guildId,
            Nickname = user.Nickname,
            CreatedOn = DateTime.Now.ToUniversalTime()
        });
        await db.SaveChangesAsync();
    }


    /// <summary>
    /// Fires on every button click. Only handles pronoun-role buttons (identified by a
    /// plain numeric custom ID with no <c>_</c> or <c>:</c> — those separators mark buttons
    /// owned by other features, e.g. gambling's double-or-nothing). Toggles the matching
    /// pronoun role on the clicking user, creating the role on the guild if it doesn't exist yet.
    /// </summary>
    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        // Not a pronoun-button ID — some other feature (e.g. Duel, Gambling) owns this button.
        if (component.Data.CustomId.Contains('_') || component.Data.CustomId.Contains(':'))
            return;

        List<Pronoun> pronouns;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            pronouns = await db.Pronouns.AsNoTracking().ToListAsync();
        }
        string pronounSelected = "";
        var guild = client.GetGuild(component.GuildId!.Value);

        foreach (var p in pronouns)
        {
            string name = p.Pronoun1;
            string id = p.Id.ToString();

            // Lazily create the pronoun role on this guild the first time it's needed.
            if (!guild.Roles.Any(r => r.Name == name))
                await guild.CreateRoleAsync(name);

            if (id == component.Data.CustomId)
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
        using (var auditScope = services.CreateScope())
        {
            var auditDb = auditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            await AuditService.InsertButtonAuditAsync(
                auditDb,
                $"{pronounSelected} {action}",
                component.User.Id.ToString(),
                component.GuildId!.Value.ToString());
        }
        await component.RespondAsync(
            embed: _embed.BuildMessageEmbed(
                "Pronoun Selection",
                $"Pronouns were successfully {action} for {component.User.Username}.",
                "", component.User.Username, Color.Blue).Build(),
            ephemeral: true);
    }


    /// <summary>
    /// Handles a DM as a possible guess for a scramble or Wordle game the author has
    /// active in this DM channel. DMs have no guild/server context, so unlike the guild-channel
    /// path in <see cref="OnMessageReceivedAsync"/> these guesses are not audit-logged.
    /// </summary>
    private async Task HandleDmGameResponseAsync(SocketMessage msg, SocketDMChannel dmChannel)
    {
        string message   = msg.Content.Trim().ToLowerInvariant();
        string channelId = dmChannel.Id.ToString();

        if (await TryHandleScrambleGuessAsync(msg.Channel, channelId, message, msg.Author, onSolved: null))
            return;

        await TryHandleWordleGuessAsync(msg.Channel, channelId, message);
    }

    /// <summary>
    /// Checks <paramref name="message"/> against an active scramble game for the channel.
    /// A non-expired game is always "consumed" (returns true) whether or not the guess was
    /// correct, so callers should stop further message processing; a missing or expired game
    /// returns false so the message can fall through to other checks (e.g. Wordle). Shared by
    /// the DM path and the guild-channel path in <see cref="OnMessageReceivedAsync"/>.
    /// <paramref name="onSolved"/> lets callers add context-specific side effects (e.g. audit
    /// logging) — DMs have no server ID to log against, so the DM caller passes null.
    /// </summary>
    private async Task<bool> TryHandleScrambleGuessAsync(
        IMessageChannel channel, string channelId, string message, IUser author, Func<DiscordbotContext, Task>? onSolved)
    {
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        var scramble = await scopedDb.ScrambleGames.FirstOrDefaultAsync(g => g.ChannelId == channelId);

        if (scramble is null) return false;

        bool expired = DateTime.UtcNow > scramble.ExpiresAt;
        if (expired) return false;

        string correctAnswer = scramble.Answer;

        if (string.Equals(message, correctAnswer, StringComparison.OrdinalIgnoreCase))
        {
            scopedDb.ScrambleGames.Remove(scramble);
            await scopedDb.SaveChangesAsync();

            if (onSolved is not null) await onSolved(scopedDb);

            await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                "🎉  Correct!", $"{author.Mention} solved it! The word was **{correctAnswer}**.",
                Color.Green, footer: $"Solved by {author.Username}").Build());
        }

        return true;
    }

    /// <summary>
    /// Records <paramref name="message"/> as a guess against an active Wordle game for the
    /// channel, updates/ends the game in the database, and refreshes the game's embed message.
    /// Returns false (no-op) if the message isn't 5 letters or no game is active for the channel.
    /// Shared by the DM path and the guild-channel path in <see cref="OnMessageReceivedAsync"/>.
    /// <paramref name="onGuessed"/> fires with the win/loss result before the DB update, letting
    /// callers add context-specific side effects (e.g. audit-logging a guild win).
    /// </summary>
    private async Task<bool> TryHandleWordleGuessAsync(
        IMessageChannel channel, string channelId, string message, Func<bool, DiscordbotContext, Task>? onGuessed = null)
    {
        if (message.Length != 5 || !message.All(char.IsLetter)) return false;

        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        var wordle = await scopedDb.WordleGames.FirstOrDefaultAsync(g => g.ChannelId == channelId);

        if (wordle is null) return false;

        string answer       = wordle.Answer;
        string messageIdStr = wordle.MessageId;
        string guessesRaw   = wordle.Guesses;

        var guesses = string.IsNullOrEmpty(guessesRaw)
            ? new List<string>()
            : guessesRaw.Split(',').ToList();

        guesses.Add(message);

        bool won      = message.Equals(answer, StringComparison.OrdinalIgnoreCase);
        bool gameOver = won || guesses.Count >= 6;

        if (onGuessed is not null) await onGuessed(won, scopedDb);

        string newGuesses = string.Join(",", guesses);

        if (gameOver)
            scopedDb.WordleGames.Remove(wordle);
        else
            wordle.Guesses = newGuesses;
        await scopedDb.SaveChangesAsync();

        if (ulong.TryParse(messageIdStr, out ulong messageId) &&
            await channel.GetMessageAsync(messageId) is IUserMessage gameMsg)
        {
            await gameMsg.ModifyAsync(m =>
                m.Embed = DiscordBot.SlashCommands.Games
                    .BuildWordleEmbed(answer, guesses, gameOver).Build());
        }

        return true;
    }


    /// <summary>
    /// Central message router — fires on every message the bot can see. Order matters: each
    /// branch below returns as soon as it claims the message, so DM game-guesses, the social-
    /// media-embed fixer, the "-" keyword prefix, and the various mini-games are mutually
    /// exclusive per message. Also drives passive credit income and pet XP-from-chatting,
    /// which apply to ordinary conversation and don't return early.
    /// </summary>
    private async Task OnMessageReceivedAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg.Author.IsWebhook) return; // never react to bots/webhooks (avoids feedback loops)

        // DMs have no guild/economy context, so they're routed to a separate,
        // games-only handler rather than falling through the guild logic below.
        if (msg.Channel is SocketDMChannel dmChannel)
        {
            await HandleDmGameResponseAsync(msg, dmChannel);
            return;
        }

        if (msg.Channel is not SocketGuildChannel msgChannel) return; // not a DM and not a guild channel — nothing to do

        string message = msg.Content.Trim().ToLowerInvariant();
        string serverId = msgChannel.Guild.Id.ToString();
        string userId = msg.Author.Id.ToString();
        const string prefix = "-"; // marks a keyword-add/lookup command, e.g. "-cat http://..."

        using (var scope = services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            // Source's UPDATE affects 0 rows (no error) if the user row doesn't exist yet —
            // matched by only updating when found, rather than upserting.
            var userRow = await scopedDb.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.ServerUid == long.Parse(serverId));
            if (userRow is not null)
            {
                userRow.LastSeen = DateTime.UtcNow; // source used GETUTCDATE() — already UTC, no conversion needed
                await scopedDb.SaveChangesAsync();
            }
        }

        // Passive credits — pass serverId explicitly (Context.Guild is null outside slash commands)
        using (var creditScope = services.CreateScope())
        {
            var creditDb = creditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            await CreditService.AddCreditsAsync(creditDb, userId, serverId, CreditHelper.PassiveMessageAmount, "message");
        }

        // Source called two separate procs (GetServerByID for IsActive, GetEmbedBroken for
        // FixEmbed) against the same Servers row — fetched once here instead since nothing
        // between these two checks can change that row. Source indexed Rows[0] on both without
        // an empty-result check (would throw if the server isn't registered — Discord.NET's
        // AsyncEvent wrapper catches that and routes it to OnLogMessageAsync); .First() below
        // preserves that same throw-if-missing behavior rather than silently treating a
        // missing server as inactive.
        Server server;
        using (var scope = services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            server = await scopedDb.Servers.AsNoTracking().FirstAsync(s => s.ServerUid == long.Parse(serverId));
        }

        // Server-wide kill switch — if the server record is inactive, skip all further
        // processing (no keyword triggers, games, or pet XP) for this message.
        if (!server.IsActive)
            return;

        var cleanup = new URLCleanup();

        // A raw social-media link (Twitter/X, Bluesky, etc.) whose native Discord embed may
        // be broken, and that isn't itself a "-" keyword command — try to fix its embed.
        if (cleanup.HasSocialMediaEmbed(message) && !message.StartsWith(prefix))
        {
            // Only step in if this server opted into the fix AND Discord's own crawler
            // didn't manage to attach a rich embed on its own within the wait window.
            if (server.FixEmbed && !await DiscordEmbedSucceededAsync(msg.Id))
            {
                await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));
            }

            return;
        }

        // "-keyword ..." — add or manage a chat-triggered keyword; handled entirely elsewhere.
        if (message.StartsWith(prefix))
        {
            await HandlePrefixCommandAsync(msg, message, serverId, userId, prefix, cleanup);
            return;
        }


        // Award passive pet XP for ordinary chatting — only if the user has an active,
        // non-hibernating pet. This block doesn't return early: games/keywords below still run.
        using (var scope = services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            var activePet = await scopedDb.Pets.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);

            if (activePet is not null && !activePet.IsHibernating)
            {
                int xpGain = DiscordBot.Helper.PetHelper.XpMessage;

                if (msg.Attachments.Count > 0)      // bonus XP for posting an image/file
                    xpGain += DiscordBot.Helper.PetHelper.XpAttachment;

                if (message.Contains("http://") || message.Contains("https://")) // bonus XP for sharing a link
                    xpGain += DiscordBot.Helper.PetHelper.XpLink;

                int oldXp = activePet.Xp;
                activePet.Xp += xpGain;
                await scopedDb.SaveChangesAsync();
                int newXp = activePet.Xp;

                int oldLevel = DiscordBot.Helper.PetHelper.LevelFromXp(oldXp);
                int newLevel = DiscordBot.Helper.PetHelper.LevelFromXp(newXp);

                if (newLevel > oldLevel) // crossed a level threshold — announce it and pay out the level-up bonus
                {
                    string petName = activePet.Name;
                    string species = activePet.Species;
                    string? unlock = DiscordBot.Helper.PetHelper.LevelUpUnlock(newLevel);
                    string emoji = DiscordBot.Helper.PetHelper.PetEmoji(
                        species, 100, 100, false, newLevel >= 50);

                    decimal lvlBonus = CreditHelper.PetLevelUpAmount(newLevel);
                    decimal newBalance = await CreditService.AddCreditsAsync(scopedDb, userId, serverId, lvlBonus, "pet_levelup");

                    await msg.Channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        $"{emoji}  {petName} levelled up!",
                        $"{msg.Author.Mention}'s pet **{petName}** is now **Level {newLevel}**! 🎉\n" +
                        $"Bonus: {CreditHelper.Format(lvlBonus)} | Balance: {CreditHelper.Format(newBalance)}" +
                        (unlock is not null ? $"\n\n{unlock}" : ""),
                        new Color(255, 215, 0)).Build());
                }
            }
        }


        // Guild-channel scramble guess — audit-logged (DMs have no server ID to log against).
        if (await TryHandleScrambleGuessAsync(msg.Channel, msgChannel.Id.ToString(), message, msg.Author,
                onSolved: solvedDb => AuditService.InsertGameTriggerAuditAsync(solvedDb, "scramble", userId, serverId)))
            return;


        DiscordBot.Models.Generated.PetWordPuzzle? petPuzzle;
        using (var scope = services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            string channelIdStr = msg.Channel.Id.ToString();
            var nowUtc = DateTime.UtcNow;
            petPuzzle = await scopedDb.PetWordPuzzles.AsNoTracking()
                .Where(p => p.ChannelId == channelIdStr && !p.Claimed && p.ExpiresAt > nowUtc)
                .FirstOrDefaultAsync();
        }

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

                using (var scope = services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    await scopedDb.PetWordPuzzles.Where(p => p.PuzzleId == puzzleId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Claimed, true));
                    await AuditService.InsertGameTriggerAuditAsync(scopedDb, "petpuzzle", userId, serverId);
                }

                // Always award credits for solving the puzzle.
                using (var creditScope = services.CreateScope())
                {
                    var creditDb = creditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    await CreditService.AddCreditsAsync(creditDb, userId, serverId, CreditHelper.PuzzleSolveAmount, "puzzle");
                }

                bool awardedXp = false;
                string petLine  = string.Empty;

                // Pet XP bonus is separate from the credit reward above and only applies
                // if the solver has an active, non-hibernating pet.
                using (var scope = services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    var solverPet = await scopedDb.Pets.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);

                    if (solverPet is not null && !solverPet.IsHibernating)
                    {
                        string solverPetName = solverPet.Name;

                        solverPet.Xp += DiscordBot.Helper.PetHelper.XpWordPuzzle;
                        await scopedDb.SaveChangesAsync();

                        awardedXp = true;
                        petLine   = $"\n**{solverPetName}** earned **+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP**! 🐾";
                    }
                }

                string description = awardedXp
                    ? $"{msg.Author.Mention} solved the bonus word puzzle!\n" +
                      $"They earned {CreditHelper.Format(CreditHelper.PuzzleSolveAmount)}!{petLine} 🎉"
                    : $"{msg.Author.Mention} solved the bonus word puzzle!\n" +
                      $"They earned {CreditHelper.Format(CreditHelper.PuzzleSolveAmount)}! 🎉";

                await msg.Channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                    "🧩  Puzzle Solved!", description, Color.Green).Build());

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
                            $"Type the secret word in this channel to earn " +
                            $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                            $"**Hint:** `{guessHint}`  ({guessState.Word.Length} letters)\n" +
                            $"*(A letter was revealed after {totalGuesses} guesses!)*\n\n" +
                            $"⏳ First correct answer wins!",
                            new Color(255, 179, 71)).Build());
                    }
                    catch { /* message may have been deleted */ }
                }
            }
        }


        // Guild-channel Wordle guess — audit-log only on a win, same as the original inline check.
        if (await TryHandleWordleGuessAsync(msg.Channel, msgChannel.Id.ToString(), message, async (won, guessDb) =>
            {
                if (won) await AuditService.InsertGameTriggerAuditAsync(guessDb, "wordle", userId, serverId);
            }))
            return;


        // Fall-through: check whether this exact message text matches a registered
        // chat-triggered keyword (e.g. auto-replying with a saved image/link).
        var action = GetChatAction(long.Parse(serverId), message);

        if (action is not null)
            // Fire-and-forget: don't block the gateway event handler on file/network I/O.
            _ = Task.Run(() => SendChatActionsAsync(msg, msgChannel, action.Value));
    }

    /// <summary>
    /// Resolves a chat message to a registered keyword (direct name match, or via an alias)
    /// and returns one random entry for it — same intent as the source GetChatAction proc's
    /// STRING_SPLIT word-matching + ORDER BY NEWID() random picks, done client-side since
    /// there's no clean EF/LINQ translation for random ordering. Realistic per-server keyword
    /// counts are small (low hundreds at most), so pulling candidates into memory first is fine.
    /// </summary>
    private (string ChatAction, string Keyword, bool Nsfw)? GetChatAction(long serverId, string message)
    {
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        var words = message.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();
        if (words.Count == 0) return null;

        var directCandidates = scopedDb.ChatKeywordMaps.AsNoTracking()
            .Where(m => m.ServerId == serverId && m.Keyword != null)
            .Select(m => m.Keyword!)
            .ToList()
            .Where(k => words.Contains(k.ToLowerInvariant()))
            .Distinct()
            .ToList();

        string? resolvedKeyword = directCandidates.Count > 0
            ? directCandidates[Random.Shared.Next(directCandidates.Count)]
            : null;

        if (resolvedKeyword is null)
        {
            var aliasCandidates = scopedDb.ChatKeywordAliases.AsNoTracking()
                .Where(a => a.ServerId == serverId)
                .Select(a => new { a.Alias, a.Keyword })
                .ToList()
                .Where(a => words.Contains(a.Alias.ToLowerInvariant()))
                .Select(a => a.Keyword)
                .Distinct()
                .ToList();

            if (aliasCandidates.Count > 0)
                resolvedKeyword = aliasCandidates[Random.Shared.Next(aliasCandidates.Count)];
        }

        if (resolvedKeyword is null) return null;

        var entries = scopedDb.ChatKeywords.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.ChatKeyword1, resolvedKeyword))
            .Select(c => new { c.FilePath, c.Nsfw })
            .ToList();

        if (entries.Count == 0) return null;

        var picked = entries[Random.Shared.Next(entries.Count)];
        return (picked.FilePath, resolvedKeyword, picked.Nsfw);
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
    /// by <see cref="SendChatActionsAsync"/>.
    /// </summary>
    private async Task HandlePrefixCommandAsync(
        SocketMessage msg, string message, string serverId,
        string userId, string prefix, URLCleanup cleanup)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string keyword = parts[0][prefix.Length..];
        string? resolvedKeyword;
        using (var scope = services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            // Source LEFT JOINed ChatKeyword but never selected any of its columns or used it
            // to filter — a no-op join. Preserved as a plain single-table lookup.
            resolvedKeyword = scopedDb.ChatKeywordMaps.AsNoTracking()
                .Where(m => EF.Functions.ILike(m.AddKeyword, keyword))
                .Select(m => m.Keyword)
                .FirstOrDefault();
        }

        if (resolvedKeyword is null) return; // not a registered keyword — ignore silently

        if (msg.Attachments.Count > 0)
        {
            await AddAttachmentsAsync(msg, resolvedKeyword, userId);
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

                string storeValue = await TrySaveSocialImageAsync(url, keyword)
                                    ?? cleanup.CleanURLEmbed(url);
                StoreChatKeyword(resolvedKeyword, storeValue, userId);
            }

            await msg.Channel.SendMessageAsync(
                embed: _embed.BuildSimpleEmbed("Added Image", "Added link(s) successfully.", Color.Blue).Build());
        }
        else
        {
            string storeValue = await TrySaveSocialImageAsync(content, keyword)
                                ?? cleanup.CleanURLEmbed(content);

            StoreChatKeyword(resolvedKeyword, storeValue, userId);

            // Locally-downloaded social media images get a different confirmation message
            // than a plain URL/text entry, since the stored value is a file path either way.
            string confirmation = storeValue.StartsWith(@"C:\")
                ? "Image downloaded and saved locally."
                : "Added URL/Text successfully.";

            await msg.Channel.SendMessageAsync(
                embed: _embed.BuildSimpleEmbed("Added URL/Text", confirmation, Color.Blue).Build());
        }
    }

    /// <summary>Persists one keyword value (a file path, URL, or plain text) against the given keyword.</summary>
    private void StoreChatKeyword(string keyword, string value, string userId)
    {
        // Source stripped literal single-quotes from FilePath before insert.
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
        // CreatedOn: source used GETDATE() (local, not UTC) and ChatKeyword has no DB default.
        // Npgsql requires UTC-Kind DateTimes for timestamptz — ToUniversalTime() converts the
        // correct local instant rather than just relabelling it (which SpecifyKind would do).
        scopedDb.ChatKeywords.Add(new ChatKeyword
        {
            ChatKeyword1 = keyword,
            FilePath = value.Replace("'", ""),
            Nsfw = false,
            CreatedOn = DateTime.Now.ToUniversalTime()
        });
        scopedDb.SaveChanges();
    }

    /// <summary>
    /// Replays every registered response for a matched chat keyword: a locally-stored file,
    /// a live URL, or plain text. Dead links are auto-removed from the keyword's URL list;
    /// posted images/links get a ❌ reaction so any member can delete them (see
    /// <see cref="OnReactionAddedAsync"/>).
    /// </summary>
    private async Task SendChatActionsAsync(
        SocketMessage msg, SocketGuildChannel msgChannel, (string ChatAction, string Keyword, bool Nsfw) action)
    {
        if (client.GetChannel(msgChannel.Id) is not IMessageChannel sender) return;

        {
            string chatAction = action.ChatAction;
            string keyword = action.Keyword;
            bool isNsfw = action.Nsfw;

            if (string.IsNullOrWhiteSpace(chatAction)) return;

            await msg.Channel.TriggerTypingAsync();

            keyword = char.ToUpperInvariant(keyword[0]) + keyword[1..];

            // Three possible stored shapes for a keyword's value: a local file path,
            // a live URL, or plain text — each is delivered differently.
            if (chatAction.StartsWith(@"C:\"))
            {
                bool isSpoiler = isNsfw && !chatAction.Contains("SPOILER_");
                var embed = new EmbedBuilder()
                    .WithTitle(keyword)
                    .WithImageUrl("attachment://" + Path.GetFileName(chatAction))
                    .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                    .Build();

                await using var stream = File.OpenRead(chatAction);
                var output = await msg.Channel.SendFileAsync(
                    stream, Path.GetFileName(chatAction), embed: embed, isSpoiler: isSpoiler);

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
                    // FIX: source passed @Keyword='' (literal empty string) here instead of the
                    // actual keyword, and DeleteChatKeywordURL's WHERE requires both FilePath AND
                    // ChatKeyword to match a NOT NULL column — an empty string never matches a
                    // real keyword, so this delete has never actually removed anything (the bot
                    // claims success regardless). Passing the real keyword instead.
                    await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {chatAction}");
                    using (var scope = services.CreateScope())
                    {
                        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        scopedDb.ChatKeywords.RemoveRange(scopedDb.ChatKeywords.Where(c =>
                            EF.Functions.ILike(c.FilePath, chatAction) && EF.Functions.ILike(c.ChatKeyword1, action.Keyword)));
                        scopedDb.SaveChanges();
                    }
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

        long guildId = (long)guild.Id;

        // Local helper: forces every bot user out of a voice channel (used both when the
        // music bot itself disconnects and when the last human leaves).
        async Task DisconnectBotsAsync(SocketVoiceChannel channel)
        {
            foreach (var bot in channel.ConnectedUsers.Where(u => u.IsBot))
                await bot.VoiceChannel.DisconnectAsync();
        }

        // Same atomicity note as Audio.cs's DeletePlayerConnected helper: source was two
        // separate procs (PlayerConnected delete + Servers.IsPlayerConnected update bundled
        // in one proc, MusicQueue delete in another) — staged/saved the same way here.
        void ClearPlayerConnected(DiscordbotContext scopedDb)
        {
            scopedDb.PlayerConnecteds.RemoveRange(scopedDb.PlayerConnecteds.Where(p => p.ServerUid == guildId));
            var server = scopedDb.Servers.FirstOrDefault(s => s.ServerUid == guildId);
            if (server is not null) server.IsPlayerConnected = false;
            scopedDb.SaveChanges();
        }

        if (user.IsBot)
        {
            // The bot itself left a channel — clear its "connected" DB row so future
            // commands know no player is active for this server.
            if (after.VoiceChannel is null && before.VoiceChannel is not null)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                using var scope = services.CreateScope();
                ClearPlayerConnected(scope.ServiceProvider.GetRequiredService<DiscordbotContext>());
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
                using var scope = services.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                ClearPlayerConnected(scopedDb);
                scopedDb.MusicQueues.RemoveRange(scopedDb.MusicQueues.Where(q => q.ServerUid == guildId));
                scopedDb.SaveChanges();
            }
        }
    }


    /// <summary>
    /// Fires on every reaction added anywhere the bot can see. Handles two unrelated features
    /// via the reacted emoji: a ❌ on one of the bot's own image posts deletes/NSFW-flags it,
    /// and a trivia letter emoji (🇦-🇩) is scored as a quiz answer. Runs as fire-and-forget
    /// so a slow download/DB call never blocks the gateway event loop.
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
                    using (var auditScope = services.CreateScope())
                    {
                        var auditDb = auditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        await AuditService.InsertReactionAuditAsync(
                            auditDb,
                            reaction.Emote.Name,
                            download.Id.ToString(),
                            reaction.UserId.ToString(),
                            cachedChannel.Id.ToString());
                    }
                    await TryMarkNsfwAsync(fileName, cachedChannel, reaction);
                    return;
                }
            }

            if (IsTriviaEmoji(reaction.Emote.Name))
            {
                using (var auditScope = services.CreateScope())
                {
                    var auditDb = auditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    await AuditService.InsertReactionAuditAsync(
                        auditDb,
                        reaction.Emote.Name,
                        download.Id.ToString(),
                        reaction.UserId.ToString(),
                        cachedChannel.Id.ToString());
                }
                await HandleTriviaReactionAsync(cachedMsg, cachedChannel, reaction, download);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>True if the reacted emoji is one of the four trivia answer letters (🇦-🇩).</summary>
    private static bool IsTriviaEmoji(string name) =>
        name is "🇦" or "🇧" or "🇨" or "🇩";

    /// <summary>Flags a chat-keyword file as NSFW the first time it's ❌-reacted, if it isn't already marked.</summary>
    private async Task TryMarkNsfwAsync(
        string content,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        // Source's own comment flags this leading-wildcard LIKE as a known perf issue (full
        // table scan every call, with a TODO in the proc itself for a future hash/checksum
        // rewrite) — preserved exactly as-is; not something this conversion is fixing.
        var matches = await scopedDb.ChatKeywords
            .Where(c => EF.Functions.ILike(c.FilePath, $"%{content}%"))
            .ToListAsync();

        if (matches.Count == 0) return; // no match at all
        if (matches.Any(c => c.Nsfw)) return; // already flagged — nothing to do

        foreach (var m in matches) m.Nsfw = true;
        await scopedDb.SaveChangesAsync();

        await channel.Value.SendMessageAsync(embed: _embed.BuildMessageEmbed(
            "NSFW",
            $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)",
            "", "BigBirdBot", Color.Blue).Build());
    }

    /// <summary>
    /// Scores a trivia-emoji reaction against the stored correct answer for that message,
    /// replies with a correct/wrong result, and deletes the trivia record once answered
    /// correctly (so later reactions on the same message are no-ops).
    /// </summary>
    private async Task HandleTriviaReactionAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        IUserMessage download)
    {
        try
        {
            if (download.Embeds.Count == 0) return; // not a trivia embed

            long messageId = (long)cachedMsg.Id;
            string userMention = reaction.User.Value.Mention;

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

            var triviaMessage = await db.TriviaMessages.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TriviaMessageId == messageId);

            if (triviaMessage is null) return; // no matching trivia record (already answered, or not a trivia message)

            string correctAnswer = triviaMessage.CorrectAnswer;

            // The answer-choice fields are named "A. ...", "B. ...", etc. — filter out any
            // other fields the embed might have (e.g. a question/category field with no dot).
            var fields = download.Embeds
                .SelectMany(e => e.Fields)
                .Where(f => f.Name.Contains('.'))
                .ToList();

            // Map the reacted emoji (🇦-🇩) to its letter, then confirm that letter's field
            // is the one holding the correct answer text.
            var correctField = fields.FirstOrDefault(f => f.Value == correctAnswer);
            if (correctField == default
                || !EmojiToLetter.TryGetValue(reaction.Emote.Name, out string? selectedLetter))
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
            {
                db.TriviaMessages.Remove(triviaMessage);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            await channel.Value.SendMessageAsync(embed: new EmbedHelper()
                .BuildMessageEmbed("Error", ex.Message, Constants.errorImageUrl, "", Color.Red).Build());
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
    /// Background loop that drives every time-based feature: reminders and journal pings
    /// (every tick), pet stat decay (every 30 min), activity-based pet XP (every 15 min),
    /// bonus word puzzles and jackpot draws (hourly), plus stock-price ticks via
    /// <see cref="StartStockTimer"/> on its own separate timer. Runs for the lifetime of the
    /// process; <see cref="OnConnectedAsync"/> restarts it if it ever dies while disconnected.
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
                // Source proc did this as one atomic UPDATE...OUTPUT — select-then-update here
                // instead, same flagged weaker-atomicity tradeoff as the Journal reminders block
                // just below (fine in practice: single-instance sequential scheduler tick).
                List<Reminder> dueReminders;
                using (var reminderScope = services.CreateScope())
                {
                    var reminderDb = reminderScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    var nowUtc = DateTime.UtcNow;
                    dueReminders = await reminderDb.Reminders
                        .Where(r => !r.Sent && r.RemindAtUtc <= nowUtc)
                        .ToListAsync();
                    foreach (var r in dueReminders) r.Sent = true;
                    await reminderDb.SaveChangesAsync();
                }
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

                // ── Journal reminders (every tick = every minute) ────────────
                List<string> dueJournalUserIds;
                {
                    using var journalScope = services.CreateScope();
                    var journalDb = journalScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    var nowUtc = DateTime.UtcNow;
                    var nowTimeUtc = TimeOnly.FromDateTime(nowUtc);
                    var todayDateUtc = nowUtc.Date;

                    // Source proc did this as one atomic UPDATE...OUTPUT (select-and-mark in a
                    // single statement). This is select-then-update instead — a marginally
                    // weaker guarantee, flagged per instructions rather than silently accepted.
                    // Fine in practice: this is a single-instance scheduler tick, not concurrent.
                    var due = await journalDb.JournalSubscriptions
                        .Where(s => nowTimeUtc >= s.DailyTimeUtc
                                 && (s.LastReminderSentAt == null || s.LastReminderSentAt.Value.Date < todayDateUtc))
                        .ToListAsync();

                    dueJournalUserIds = due.Select(s => s.UserId).ToList();
                    foreach (var s in due) s.LastReminderSentAt = nowUtc;
                    await journalDb.SaveChangesAsync();
                }

                foreach (string journalUserIdStr in dueJournalUserIds)
                {
                    try
                    {
                        ulong userId = ulong.Parse(journalUserIdStr);
                        var journalUser = await client.GetUserAsync(userId)
                                       ?? await client.Rest.GetUserAsync(userId);
                        if (journalUser is null) continue;

                        string prompt = DiscordBot.Helper.JournalHelper.GetRandomPrompt();

                        var dm = await journalUser.CreateDMChannelAsync();
                        await dm.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "📓  Time to Journal!",
                            "Your daily journaling reminder is here!\n\n" +
                            $"**Today's prompt:**\n> *{prompt}*\n\n" +
                            "Take a few minutes to write your thoughts. " +
                            "When you're done, use `/journal done` to log your entry and build your streak!",
                            new Color(0x7B68EE),
                            footer: "Use /journal done when you finish • Use /journal unsubscribe to stop reminders").Build());
                    }
                    catch { /* DMs disabled or user not found */ }
                }

                // ── Birthdays (every tick = every minute) ────────────────────
                // Source (GetTodaysBirthdays) used an atomic UPDATE...OUTPUT to select and
                // mark Sent=1 in one statement. EF has no direct equivalent — this is a
                // select-then-update instead, the same weaker-atomicity tradeoff already
                // flagged for the Journal-reminders conversion. Not a real race here in
                // practice since RunSchedulerAsync's tick loop is sequential, not concurrent.
                // BirthdayDate is stored UTC (see ServerCommands.HandleBirthdayAsync) but the
                // source compared calendar dates in LOCAL time (CAST(GETDATE() AS DATE)) — the
                // .ToLocalTime() below reproduces that; done client-side since it isn't
                // translatable to SQL.
                List<Birthday> todaysBirthdays;
                using (var scope = services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    var today = DateTime.Now.Date;
                    var unsent = await scopedDb.Birthdays.Where(b => !b.Sent).ToListAsync();
                    todaysBirthdays = unsent.Where(b => b.BirthdayDate.ToLocalTime().Date == today).ToList();
                    foreach (var b in todaysBirthdays) b.Sent = true;
                    await scopedDb.SaveChangesAsync();
                }
                foreach (var birthdayRow in todaysBirthdays)
                {
                    try
                    {
                        string mention = birthdayRow.BirthdayUser;
                        ulong guildId = ulong.Parse(birthdayRow.BirthdayGuild);
                        string? overrideChannelId = birthdayRow.BirthdayChannel;

                        var birthdayGuild = client.GetGuild(guildId);
                        if (birthdayGuild is null) continue;

                        ITextChannel? channel = null;

                        if (!string.IsNullOrWhiteSpace(overrideChannelId) && ulong.TryParse(overrideChannelId, out ulong overrideId))
                            channel = birthdayGuild.GetTextChannel(overrideId);

                        if (channel is null)
                        {
                            ServerHelper.ServerInfo? serverDetails;
                            using (var serverScope = services.CreateScope())
                            {
                                var serverScopedDb = serverScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                                serverDetails = await ServerHelper.GetServerInfoAsync(serverScopedDb, guildId);
                            }
                            if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;

                            channel = ResolveAnnouncementChannel(birthdayGuild, serverDetails.DefaultChannelID);
                        }

                        if (channel is null) continue;

                        await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "🎂  Happy Birthday!",
                            $"Everyone wish {mention} a very happy birthday! 🎉🎈",
                            new Color(255, 105, 180)).Build());
                    }
                    catch { /* guild/channel may no longer exist */ }
                }

            // Runs once every 30 minutes (tick increments once per minute).
            if (_schedulerTick % 30 == 0)
            {
                List<(string UserId, string Name, string Species)> newlyHibernated;
                using (var scope = services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

                    // Step 1: decay stats for absent-owner (2h+) pets only.
                    var absenceCutoff = DateTime.UtcNow.AddHours(-2);
                    await scopedDb.Pets
                        .Where(p => !p.IsHibernating && scopedDb.Users.Any(u =>
                            u.UserId == p.UserId && (u.LastSeen == null || u.LastSeen < absenceCutoff)))
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.Hunger, p => p.Hunger > 5 ? p.Hunger - 5 : 0)
                            .SetProperty(p => p.Happiness, p => p.Happiness > 4 ? p.Happiness - 4 : 0)
                            .SetProperty(p => p.Energy, p => p.Energy > 3 ? p.Energy - 3 : 0)
                            .SetProperty(p => p.Hygiene, p => p.Hygiene > 2 ? p.Hygiene - 2 : 0));

                    // Step 2: trigger hibernation when 2+ stats fall below threshold (once only).
                    var hibernatedAt = DateTime.UtcNow;
                    await scopedDb.Pets
                        .Where(p => !p.IsHibernating && p.HibernatedAt == null &&
                            ((p.Hunger < 15 ? 1 : 0) + (p.Happiness < 15 ? 1 : 0) + (p.Energy < 15 ? 1 : 0)) >= 2)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.IsHibernating, true)
                            .SetProperty(p => p.HibernatedAt, hibernatedAt));

                    // Step 3: return only pets that just hibernated this tick (within last 2 min).
                    var recentCutoff = DateTime.UtcNow.AddMinutes(-2);
                    newlyHibernated = await scopedDb.Pets.AsNoTracking()
                        .Where(p => p.IsHibernating && p.HibernatedAt >= recentCutoff)
                        .Select(p => new ValueTuple<string, string, string>(p.UserId, p.Name, p.Species))
                        .ToListAsync();
                }

                foreach (var (decayUserId, petName, species) in newlyHibernated)
                {
                    try
                    {
                        ulong ownerId = ulong.Parse(decayUserId);
                        var owner = await client.GetUserAsync(ownerId);
                        if (owner is null) continue;

                        await owner.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                            "💤  Your pet is hibernating!",
                            $"**{petName}** the {species} has gone into hibernation.\n\n" +
                            $"They were too hungry, unhappy, and tired while you were away.\n\n" +
                            $"Use `/feed` to wake them up! Don't worry — they're safe. 🌿",
                            Color.DarkGrey).Build());
                    }
                    catch { /* DMs disabled or user not found */ }
                }
            }

            // Runs once every 15 minutes — awards pet XP to users currently shown as
            // playing/listening/streaming (Discord "Activity" status), rewarding presence
            // rather than requiring active chatting.
            if (_schedulerTick % 15 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    await guild.DownloadUsersAsync();

                    foreach (var guildUser in guild.Users.Where(u => !u.IsBot))
                    {
                        bool hasActivity = guildUser.Activities?.Any(a =>
                            a.Type is ActivityType.Playing
                                   or ActivityType.Listening
                                   or ActivityType.Streaming) == true;

                        if (!hasActivity) continue; // not showing a qualifying activity status right now

                        string activityUserId = guildUser.Id.ToString();
                        using var scope = services.CreateScope();
                        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        var userPet = await scopedDb.Pets.FirstOrDefaultAsync(p => p.UserId == activityUserId && p.IsActive);

                        if (userPet is null) continue;
                        if (userPet.IsHibernating) continue;
                        if (userPet.Hunger <= 20) continue;

                        userPet.Xp += DiscordBot.Helper.PetHelper.XpActivity;
                        await scopedDb.SaveChangesAsync();
                    }
                }
            }

            // Runs once every 60 minutes — posts a new bonus word puzzle to every eligible guild.
            if (_schedulerTick % 60 == 0)
            {
                // Pull a random word from the Words table — no clean server-side "random row"
                // translation in EF/Postgres (same as source's ORDER BY NEWID()), so fetch all
                // and pick client-side.
                string? puzzleWord;
                using (var scope = services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    var words = await scopedDb.Words.AsNoTracking().Select(w => w.Word1).ToListAsync();
                    puzzleWord = words.Count == 0 ? null : words[Random.Shared.Next(words.Count)].Trim();
                }
                if (puzzleWord is null) goto skipPuzzle;
                if (string.IsNullOrWhiteSpace(puzzleWord)) goto skipPuzzle;

                foreach (var guild in client.Guilds)
                {
                    ServerHelper.ServerInfo? serverDetails;
                    using (var serverScope = services.CreateScope())
                    {
                        var serverScopedDb = serverScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        serverDetails = await ServerHelper.GetServerInfoAsync(serverScopedDb, guild.Id);
                    }
                    if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;
                    var channel = ResolveAnnouncementChannel(guild, serverDetails.DefaultChannelID);
                    if (channel is null) continue;

                    using (var scope = services.CreateScope())
                    {
                        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        scopedDb.PetWordPuzzles.Add(new PetWordPuzzle
                        {
                            ChannelId = serverDetails.DefaultChannelID,
                            Word = puzzleWord,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(55)
                        });
                        await scopedDb.SaveChangesAsync();
                    }

                    string blankHint = $"{puzzleWord[0]}{new string('_', puzzleWord.Length - 1)}";

                    var puzzleMsg = await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        "🧩  Bonus Word Puzzle!",
                        $"Type the secret word in this channel to earn " +
                        $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                        $"**Hint:** `{blankHint}`  ({puzzleWord.Length} letters)\n\n" +
                        $"⏳ Expires in 55 minutes — first correct answer wins!",
                        new Color(255, 179, 71)).Build());

                    // Register shared hint state — all reveal sources (T+30, T+50, every-20-guesses)
                    // accumulate into this so the hint only ever grows, never resets.
                    string capturedChannelId = serverDetails.DefaultChannelID;
                    var hintState = new PuzzleHintState(puzzleWord, puzzleMsg);
                    _puzzleHintStates[capturedChannelId] = hintState;

                    var capturedMsg  = puzzleMsg;
                    var capturedWord = puzzleWord;
                    var capturedCh   = channel;

                    // ── 30-min hint: reveal a second letter ──────────────────
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30));

                        bool stillActive;
                        using (var scope = services.CreateScope())
                        {
                            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                            string chId = capturedCh.Id.ToString();
                            var nowUtc = DateTime.UtcNow;
                            stillActive = await scopedDb.PetWordPuzzles.AsNoTracking()
                                .AnyAsync(p => p.ChannelId == chId && !p.Claimed && p.ExpiresAt > nowUtc);
                        }
                        if (!stillActive) return;

                        if (!hintState.TryRevealNext(out string hint30)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Bonus Word Puzzle!",
                                $"Type the secret word in this channel to earn " +
                                $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
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

                        bool stillActive;
                        using (var scope = services.CreateScope())
                        {
                            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                            string chId = capturedCh.Id.ToString();
                            var nowUtc = DateTime.UtcNow;
                            stillActive = await scopedDb.PetWordPuzzles.AsNoTracking()
                                .AnyAsync(p => p.ChannelId == chId && !p.Claimed && p.ExpiresAt > nowUtc);
                        }
                        if (!stillActive) return;

                        if (!hintState.TryRevealNext(out string hint50)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                                "🧩  Bonus Word Puzzle — Last Chance!",
                                $"Type the secret word in this channel to earn " +
                                $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
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

                        // GetPetWordPuzzle filters ExpiresAt > NOW — use GetPuzzleClaimedStatus
                        // instead so we can distinguish solved vs expired-unsolved.
                        bool wasClaimed;
                        using (var scope = services.CreateScope())
                        {
                            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                            string chId = capturedCh.Id.ToString();
                            wasClaimed = await scopedDb.PetWordPuzzles.AsNoTracking()
                                .Where(p => p.ChannelId == chId)
                                .OrderByDescending(p => p.PuzzleId)
                                .Select(p => (bool?)p.Claimed)
                                .FirstOrDefaultAsync() == true;
                        }

                        if (wasClaimed)
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

            // Runs once every 60 minutes — draws a weighted-random winner from each guild's
            // entry jackpot pool (contributions from /jackpot).
            if (_schedulerTick % 60 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    string guildIdStr = guild.Id.ToString();
                    using var scope = services.CreateScope();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

                    var entries = await scopedDb.JackpotEntries.AsNoTracking()
                        .Where(e => e.ServerId == guildIdStr).ToListAsync();

                    long pot = (long)entries.Sum(e => e.Amount);
                    int entryCount = entries.Count;

                    if (pot <= 0 || entryCount == 0) continue;

                    var serverDetails = await ServerHelper.GetServerInfoAsync(scopedDb, guild.Id);
                    if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;

                    // Weighted random draw: each entrant's odds are proportional to how much
                    // they contributed. Sum every contribution, roll a point in that range,
                    // then walk the running cumulative total until the roll falls inside it.
                    var byUser = entries.GroupBy(e => e.UserId)
                        .Select(g => (userId: g.Key, total: g.Sum(e => e.Amount))).ToList();
                    long totalWeight = (long)byUser.Sum(u => u.total);

                    long roll = (long)(Random.Shared.NextDouble() * totalWeight);
                    long cum = 0;
                    string? winnerId = null;

                    foreach (var u in byUser)
                    {
                        cum += (long)u.total;
                        if (roll < cum) { winnerId = u.userId; break; }
                    }

                    winnerId ??= byUser[0].userId; // floating-point fallback — should be unreachable

                    await CreditService.AddCreditsAsync(scopedDb, winnerId, guildIdStr, pot, "jackpot_win");

                    await scopedDb.JackpotEntries.Where(e => e.ServerId == guildIdStr).ExecuteDeleteAsync();

                    var channel = ResolveAnnouncementChannel(guild, serverDetails.DefaultChannelID);
                    if (channel is null) continue;

                    IUser? winner = null;
                    try { winner = await client.GetUserAsync(ulong.Parse(winnerId)); } catch { }

                    string winnerDisplay = winner is not null ? winner.Mention : $"<@{winnerId}>";

                    await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        "🎰  Jackpot Winner!",
                        $"🎉 {winnerDisplay} won the jackpot!\n\n" +
                        $"💰 **Prize:** {CreditHelper.Format(pot)}\n" +
                        $"🎟️ **Entries this round:** {entryCount}\n\n" +
                        $"*The jackpot resets now — use `/jackpot` to enter the next round!*\n" +
                        $"*The jackpot will also add 1% of all gambling bets to the next round!*",
                        new Color(255, 215, 0)).Build());
                }
            }

            // ── Passive jackpot hourly draw ────────────────────────────────────────
            // Runs once every 60 minutes — separate pool from the entry jackpot above,
            // fed automatically by 1% of every gambling bet rather than direct contributions.
            if (_schedulerTick % 60 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    using var scope = services.CreateScope();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

                    var draw = await JackpotService.DrawAsync(scopedDb, (long)guild.Id);
                    if (draw is null) continue; // pool empty or no contributors

                    string passiveWinnerId = draw.Value.userId;
                    decimal passivePool    = draw.Value.pool;

                    await CreditService.AddCreditsAsync(scopedDb, passiveWinnerId, guild.Id.ToString(), passivePool, "passive_jackpot_win");

                    // Announce in the server's announcement channel (if configured and enabled).
                    var passiveDetails = await ServerHelper.GetServerInfoAsync(scopedDb, guild.Id);
                    if (passiveDetails is null || !passiveDetails.AnnouncementsEnabled) continue;

                    var passiveChan = ResolveAnnouncementChannel(guild, passiveDetails.DefaultChannelID);
                    if (passiveChan is null) continue;

                    IUser? passiveWinner = null;
                    try { passiveWinner = await client.GetUserAsync(ulong.Parse(passiveWinnerId)); } catch { }
                    string passiveDisplay = passiveWinner is not null ? passiveWinner.Mention : $"<@{passiveWinnerId}>";

                    await passiveChan.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        "🌊  Passive Jackpot Winner!",
                        $"🎉 {passiveDisplay} won the **passive jackpot** and took home **{DiscordBot.Helper.CreditHelper.Format(passivePool)}**!\n\n" +
                        $"*1% of every gambling bet feeds this pool — keep playing to build it back up!*",
                        new Color(100, 200, 255)).Build());
                }
            }
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
    /// <summary>
    /// Two hardcoded Discord user IDs that get a bonus themed video link piggybacked onto
    /// their regular scheduled delivery on Mondays/Fridays — an intentional easter egg from
    /// the source proc (GetUsersScheduledKeyword), not a bug. Preserved exactly.
    /// </summary>
    private static readonly string[] SpecialScheduleUserIds = ["233611778351824896", "171369791486033920"];

    /// <summary>
    /// Finds every due scheduled keyword delivery, reschedules each for a random time between
    /// 12:00 PM and 11:00 PM the following day, and resolves each to one random matching
    /// ChatKeyword entry (URL/file). Replicates GetUsersScheduledKeyword, including the
    /// Monday/Friday special-case rows for <see cref="SpecialScheduleUserIds"/>.
    ///
    /// NOTE: the source used GETDATE() throughout (local server time, not UTC) — matched here
    /// with DateTime.Now rather than DateTime.UtcNow, deliberately, since this subsystem's
    /// stored ScheduledDateTime values are all on that same local-time basis (unlike the
    /// Journal subsystem, which is UTC throughout).
    /// </summary>
    private List<(string UserId, string FilePath, string ThirstTable)> GetUsersScheduledKeyword()
    {
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        // Npgsql requires UTC-Kind DateTimes for timestamptz (writes AND query comparisons) —
        // all wall-clock math below stays in local time (to correctly match GETDATE()'s
        // semantics), converted to UTC only at the point each value touches the database.
        var now = DateTime.Now;
        var nowUtc = now.ToUniversalTime();
        // BUG FIX: UsersScheduledKeyword has no real primary key in the schema — the EF model's
        // HasKey(UserId, ChatKeyword) doesn't hold in practice (a user can have 2+ rows for the
        // same keyword; nothing prevents it — see HandleAddAsync). A tracked update against a
        // duplicated "key" throws DbUpdateConcurrencyException ("expected to affect 1 row(s),
        // but actually affected 0") — which is exactly what was spamming the owner DM every tick.
        // Fetch AsNoTracking (read-only) and reschedule via ExecuteUpdate, which filters by the
        // actual WHERE clause rather than resolving rows through key-based change tracking.
        var due = scopedDb.UsersScheduledKeywords.AsNoTracking().Where(u => u.ScheduledDateTime <= nowUtc).ToList();
        if (due.Count == 0) return [];

        // Source computed ONE random reschedule time and applied it to every due row in this
        // tick (not a distinct random value per row) — preserved exactly, quirky as that is.
        var today = now.Date;
        var fromDate = today.AddHours(12);
        var toDate = today.AddHours(23);
        int seconds = (int)(toDate - fromDate).TotalSeconds;
        var randomTime = fromDate.AddDays(1).AddSeconds(Random.Shared.Next(seconds));
        var randomTimeUtc = randomTime.ToUniversalTime();

        scopedDb.UsersScheduledKeywords.Where(u => u.ScheduledDateTime <= nowUtc)
            .ExecuteUpdate(s => s.SetProperty(u => u.ScheduledDateTime, randomTimeUtc));

        var results = new List<(string UserId, string FilePath, string ThirstTable)>();
        foreach (var d in due)
        {
            var candidates = scopedDb.ChatKeywords.AsNoTracking()
                .Where(c => EF.Functions.ILike(c.ChatKeyword1, d.ChatKeyword))
                .Select(c => c.FilePath)
                .ToList();
            if (candidates.Count == 0) continue; // source's CROSS APPLY drops non-matching rows too
            results.Add((d.UserId, candidates[Random.Shared.Next(candidates.Count)], d.ChatKeyword));
        }

        var dueUserIds = due.Select(d => d.UserId).ToHashSet();
        string weekday = now.DayOfWeek.ToString();
        if (weekday == "Monday")
            foreach (var u in SpecialScheduleUserIds.Where(dueUserIds.Contains))
                results.Add((u, "https://www.youtube.com/watch?v=QxCSQ0j-SFM", "DOTO MONDAY"));
        if (weekday == "Friday")
            foreach (var u in SpecialScheduleUserIds.Where(dueUserIds.Contains))
                results.Add((u, "https://www.youtube.com/watch?v=MGxMxko9hww", "MATIKANEFUKUKITARU FRIDAY"));

        return results;
    }

    /// <summary>
    /// Requeues every one of a user's scheduled keyword rows to fire again in 1 minute —
    /// used after a failed delivery attempt. Matches the redeployed
    /// UpdateUsersScheduledKeywordRequeue's local-time (GETDATE()) semantics.
    /// </summary>
    private void RequeueUserSchedule(string userId)
    {
        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
        var newTime = DateTime.Now.AddMinutes(1).ToUniversalTime();
        // Same duplicate-"key" concurrency issue as GetUsersScheduledKeyword above — bulk
        // ExecuteUpdate instead of a tracked read/mutate/SaveChanges.
        scopedDb.UsersScheduledKeywords.Where(u => u.UserId == userId)
            .ExecuteUpdate(s => s.SetProperty(u => u.ScheduledDateTime, newTime));
    }

    private async Task RunScheduledKeywordsAsync()
    {
        List<(string UserId, string FilePath, string ThirstTable)> dueList;
        try
        {
            dueList = GetUsersScheduledKeyword();
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"[Keywords] SP call failed: {ex.Message}");
            return;
        }

        if (dueList.Count == 0) return;

        foreach (var (userId, filePath, tableNameRaw) in dueList)
        {
            string tableName = tableNameRaw;
            tableName = char.ToUpperInvariant(tableName[0]) + tableName[1..];
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

                // Stored value is either a local file path or a URL — branch accordingly.
                if (filePath.StartsWith(@"C:\"))
                {
                    if (!File.Exists(filePath)) // file was moved/deleted since being registered
                    {
                        RequeueUserSchedule(userId);
                    }
                    else if (new FileInfo(filePath).Length > 8 * 1024 * 1024) // exceeds Discord's non-boosted upload limit
                    {
                        using var compressed = TryCompressImageUnder8Mb(filePath);
                        if (compressed is null)
                        {
                            await NotifyOwnerAsync($"[Keywords] Skipped {filePath} for user {userId} — file exceeds 8 MB Discord limit and could not be compressed.");
                        }
                        else
                        {
                            var fileEmbed = new EmbedBuilder()
                                .WithTitle(tableName)
                                .WithImageUrl("attachment://" + Path.GetFileName(filePath))
                                .WithColor(Color.Blue)
                                .WithFooter(timestamp)
                                .Build();
                            await user.SendFileAsync(compressed, Path.GetFileName(filePath), embed: fileEmbed);
                        }
                    }
                    else
                    {
                        var fileEmbed = new EmbedBuilder()
                            .WithTitle(tableName)
                            .WithImageUrl("attachment://" + Path.GetFileName(filePath))
                            .WithColor(Color.Blue)
                            .WithFooter(timestamp)
                            .Build();
                        await user.SendFileAsync(filePath, embed: fileEmbed);
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
                    // FIX: same @Keyword='' bug as SendChatActionsAsync above — passing the
                    // real keyword instead so the delete actually matches something.
                    using (var scope = services.CreateScope())
                    {
                        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        scopedDb.ChatKeywords.RemoveRange(scopedDb.ChatKeywords.Where(c =>
                            EF.Functions.ILike(c.FilePath, filePath) && EF.Functions.ILike(c.ChatKeyword1, tableName)));
                        scopedDb.SaveChanges();
                    }
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
                RequeueUserSchedule(userId);
                await NotifyOwnerAsync(
                    $"Scheduled send failed for user {userId}.\n{ex.StackTrace}\n" +
                    $"Requeued for {DateTime.Now.AddMinutes(1):yyyy-MM-dd hh:mm tt}.");
            }
        }
    }


    /// <summary>
    /// Starts the two stock-market timers, independent of the minute-based scheduler loop:
    /// a repeating price-tick timer, and a one-shot-then-repeating 24h high/low reset timer
    /// aligned to UTC midnight. Both fire their handler via the .NET <c>Timer.Elapsed</c>
    /// event rather than polling.
    /// </summary>
    private void StartStockTimer()
    {
        // Price tick every 15 minutes
        _stockTimer = new System.Timers.Timer(
            TimeSpan.FromMinutes(StockHelper.TickIntervalMinutes).TotalMilliseconds);
        _stockTimer.Elapsed += async (_, _) => await TickStockPricesAsync(); // event-driven: fires automatically on each interval
        _stockTimer.AutoReset = true;
        _stockTimer.Start();

        // 24h high/low reset — fire at next midnight UTC, then every 24h
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        double initialDelay = (nextMidnight - now).TotalMilliseconds;

        _stockDayResetTimer = new System.Timers.Timer(initialDelay);
        _stockDayResetTimer.Elapsed += async (_, _) =>
        {
            // First firing lands exactly at midnight (initialDelay above); once it fires,
            // reconfigure the same timer to repeat every 24h from then on.
            await ResetStockDayRangeAsync();
            _stockDayResetTimer!.Interval = TimeSpan.FromHours(24).TotalMilliseconds;
            _stockDayResetTimer.AutoReset = true;
        };
        _stockDayResetTimer.AutoReset = false; // one-shot until the handler above switches it to repeating
        _stockDayResetTimer.Start();

        Console.WriteLine(
            $"[StockMarket] Timers started — tick every {StockHelper.TickIntervalMinutes} min, " +
            $"day reset at {nextMidnight:HH:mm} UTC.");
    }

    /// <summary>Timer.Elapsed handler: advances every stock's price by one random-walk step and clears expired shop effects.</summary>
    private async Task TickStockPricesAsync()
    {
        try
        {
            using var scope = services.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

            // Clean expired shop effects on every tick (every 15 min)
            try
            {
                await scopedDb.UserActiveEffects
                    .Where(e => e.ExpiresAt != null && e.ExpiresAt <= DateTime.UtcNow)
                    .ExecuteDeleteAsync();
            }
            catch { /* non-fatal */ }

            var stocks = await scopedDb.Stocks.AsNoTracking().OrderBy(s => s.Ticker)
                .Select(s => new { s.Ticker, s.Price, s.Volatility, s.Trend }).ToListAsync();

            foreach (var row in stocks)
            {
                decimal newPrice = StockHelper.NextPrice(row.Price, (double)row.Volatility, (double)row.Trend);
                await StockService.ApplyTickAsync(scopedDb, row.Ticker, newPrice);
            }

            Console.WriteLine(
                $"[StockMarket] Tick at {DateTime.UtcNow:HH:mm:ss} UTC — {stocks.Count} stocks updated.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StockMarket] Tick error: {ex.Message}");
        }
    }

    /// <summary>Timer.Elapsed handler (fires once at UTC midnight, then every 24h): resets each stock's recorded 24h high/low.</summary>
    private async Task ResetStockDayRangeAsync()
    {
        try
        {
            using var scope = services.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            await scopedDb.Stocks.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.High24h, x => x.Price)
                .SetProperty(x => x.Low24h, x => x.Price));
            Console.WriteLine($"[StockMarket] 24h high/low reset at {DateTime.UtcNow:yyyy-MM-dd}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StockMarket] Day reset error: {ex.Message}");
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
        string dir = $@"C:\Temp\DiscordBot\{folder}";
        string fullPath = Path.Combine(dir, $"social_{DateTime.Now:yyyyMMdd_HHmmssfffff}.{ext}");

        Directory.CreateDirectory(dir);

        var bytes = await http.GetByteArrayAsync(imageUrl);
        await File.WriteAllBytesAsync(fullPath, bytes);

        await logger.DebugAsync($"[SocialImage] Saved → {fullPath}");
        return fullPath;
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
    private async Task AddAttachmentsAsync(
        SocketMessage msg, string tablename, string userId)
    {
        tablename = tablename.Replace("KeywordMulti.", "");

        using var scope = services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

        foreach (var attachment in msg.Attachments)
        {
            string[] parts = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string uniqueName = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}";
            string path = $@"C:\Temp\DiscordBot\{tablename}\{uniqueName}.{parts[1]}";

            // Source did SET @FilePath = REPLACE(@FilePath, '''', '') before insert.
            // CreatedOn: source used GETDATE() (local, not UTC); ChatKeyword has no DB default.
            scopedDb.ChatKeywords.Add(new ChatKeyword
            {
                ChatKeyword1 = tablename,
                FilePath = path.Replace("'", ""),
                Nsfw = false,
                CreatedOn = DateTime.Now.ToUniversalTime()
            });
            scopedDb.SaveChanges();

            using var http = httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(attachment.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);
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