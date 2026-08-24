using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DiscordBot.Services;

/// <summary>
/// Handles slash command registration and interaction routing.
/// </summary>
public sealed class InteractionHandlerService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _handler;
    private readonly IServiceProvider _services;
    private readonly IAudioService _audioService;

    // Ensures ReadyAsync only runs once even if the client reconnects.
    private int _readyFired;

    public InteractionHandlerService(
        DiscordSocketClient client,
        InteractionService handler,
        IServiceProvider services,
        IAudioService audioService)
    {
        _client = client;
        _handler = handler;
        _services = services;
        _audioService = audioService;
    }

    /// <summary>Discovers every interaction module and wires up the Ready/InteractionCreated/InteractionExecuted event handlers.</summary>
    public async Task InitializeAsync()
    {
        _client.Ready += ReadyAsync;                                  // gateway ready — sync slash commands, restore voice players
        _client.InteractionCreated += HandleInteractionAsync;          // any slash command / component / autocomplete interaction
        _handler.InteractionExecuted += HandleInteractionExecutedAsync; // fires after a matched interaction command finishes

        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }

    /// <summary>
    /// Fires each time the gateway becomes ready (guarded to run only once, even across
    /// reconnects): syncs global slash commands if they've changed, then restores any
    /// voice players/queues that were active before a restart.
    /// </summary>
    private async Task ReadyAsync()
    {
        if (Interlocked.Exchange(ref _readyFired, 1) != 0)
            return;

        var logging = _services.GetRequiredService<LoggingService>();

        try
        {
            await SyncCommandsAsync(logging);
        }
        catch (Exception ex)
        {
            await logging.DebugAsync($"[InteractionHandler] Command registration failed: {ex.Message}");
        }

        await RestorePlayersAsync(logging);
    }

    /// <summary>
    /// Only bulk-overwrites global commands when a difference is detected,
    /// avoiding the rate limit on every restart.
    /// </summary>
    private async Task SyncCommandsAsync(LoggingService logging)
    {
        var allExisting = (await _client.GetGlobalApplicationCommandsAsync()).ToList();
        var existingSlash = allExisting.Where(c => c.Type == ApplicationCommandType.Slash).ToList();

        var desired = _handler.SlashCommands;
        int desiredTotal = desired.Count + _handler.ContextCommands.Count;

        if (allExisting.Count == desiredTotal && !CommandsDiffer(existingSlash, desired))
        {
            await logging.DebugAsync(
                $"[InteractionHandler] {allExisting.Count} command(s) up to date — skipping registration.");
            return;
        }

        await logging.DebugAsync(
            $"[InteractionHandler] Command mismatch detected — registering {desiredTotal} command(s) in background.");

        _ = Task.Run(async () =>
        {
            try
            {
                var registered = await _handler.RegisterCommandsGloballyAsync(deleteMissing: true);
                foreach (var cmd in registered)
                    await logging.DebugAsync($"Registered: {cmd.Name} ({cmd.Type})");
            }
            catch (Exception ex)
            {
                await logging.DebugAsync($"[InteractionHandler] Background registration failed: {ex.Message}");
            }
        });
    }

    /// <summary>Compares the live registered commands against the desired set by count, description, and parameter count.</summary>
    private static bool CommandsDiffer(
        IReadOnlyCollection<IApplicationCommand> existing,
        IReadOnlyCollection<SlashCommandInfo> desired)
    {
        if (existing.Count != desired.Count)
            return true;

        var existingByName = existing.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var cmd in desired)
        {
            if (!existingByName.TryGetValue(cmd.Name, out var live))
                return true;

            if (live.Description != cmd.Description)
                return true;

            if ((live.Options?.Count ?? 0) != cmd.Parameters.Count)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reconnects the music player to every voice channel that was active before the bot last
    /// stopped (per the DB's PlayerConnected rows), restores the saved volume, and re-queues
    /// whatever tracks were still pending — skipping channels that are now empty.
    /// </summary>
    private async Task RestorePlayersAsync(LoggingService logging)
    {
        List<(long voiceChannelId, long textChannelId)> connections;
        using (var scope = _services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
            var rows = await (
                from pc in scopedDb.PlayerConnecteds.AsNoTracking()
                join s in scopedDb.Servers.AsNoTracking() on pc.ServerUid equals s.ServerUid
                orderby s.ServerName
                select new { pc.VoiceChannelId, pc.TextChannelId }
            ).ToListAsync();
            connections = rows.Select(r => (r.VoiceChannelId, r.TextChannelId)).ToList();
        }
        if (connections.Count == 0) return;

        foreach (var (voiceChannelId, textChannelId) in connections)
        {
            ulong voiceId = (ulong)voiceChannelId;
            ulong textId = (ulong)textChannelId;

            foreach (var guild in _client.Guilds)
            {
                var voice = guild.GetVoiceChannel(voiceId);
                var text = guild.GetTextChannel(textId);
                if (voice is null || text is null) continue;

                if (voice.ConnectedUsers.Count == 0)
                {
                    _ = logging.DebugAsync($"Skipping {voice.Name} in {guild.Name} — no users connected.");
                    continue;
                }

                // Snapshot URLs before the Task.Run closure — its own DbContext scope
                // must be short-lived and can't outlive this loop iteration.
                List<string> queueUrls;
                using (var scope = _services.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                    long guildIdLong = (long)guild.Id;
                    queueUrls = await scopedDb.MusicQueues.AsNoTracking()
                        .Where(m => m.ServerUid == guildIdLong)
                        .OrderBy(m => m.MusicQueueId)
                        .Select(m => m.Url)
                        .ToListAsync();
                    queueUrls = queueUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
                }

                var capturedGuild = guild;
                var capturedVoice = voice;
                var capturedText = text;
                var capturedQueue = queueUrls;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _audioService.StartAsync();
                        await Task.Delay(3_000);

                        var player = await _audioService.Players
                            .JoinAsync<CustomPlayer, CustomPlayerOptions>(
                                capturedGuild.Id,
                                capturedVoice.Id,
                                CreatePlayerAsync,
                                Microsoft.Extensions.Options.Options.Create(
                                    new CustomPlayerOptions
                                    {
                                        SelfMute = true,
                                        TextChannel = capturedText,
                                        Services = _services
                                    }));

                        await logging.DebugAsync(
                            $"Player restored in {capturedGuild.Name} / {capturedVoice.Name}");

                        int? savedVol;
                        using (var innerScope = _services.CreateScope())
                        {
                            var innerDb = innerScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                            long guildIdLong = (long)capturedGuild.Id;
                            savedVol = await innerDb.Servers.AsNoTracking()
                                .Where(s => s.ServerUid == guildIdLong)
                                .Select(s => (int?)s.Volume)
                                .FirstOrDefaultAsync();
                        }

                        if (savedVol is not null)
                        {
                            await player.SetVolumeAsync(savedVol.Value / 100f);
                            await logging.DebugAsync($"Volume restored to {savedVol}% for {capturedGuild.Name}");
                        }

                        if (capturedQueue.Count == 0) return;

                        await logging.DebugAsync(
                            $"Restoring {capturedQueue.Count} queued track(s) for {capturedGuild.Name}");

                        bool firstTrack = true;

                        foreach (var url in capturedQueue)
                        {
                            try
                            {
                                var result = await _audioService.Tracks
                                    .LoadTracksAsync(url, TrackSearchMode.None);

                                if (result.IsFailed || result.Track is null)
                                {
                                    await logging.DebugAsync($"Could not resolve queued URL — skipping: {url}");
                                    continue;
                                }

                                if (firstTrack)
                                {
                                    await player.PlayAsync(result.Track);
                                    firstTrack = false;
                                }
                                else
                                {
                                    await player.Queue.AddAsync(new TrackQueueItem(result.Track));
                                }
                            }
                            catch (Exception ex)
                            {
                                await logging.DebugAsync(
                                    $"Failed to re-queue '{url}' in {capturedGuild.Name}: {ex.Message}");
                            }
                        }

                        await logging.DebugAsync(
                            $"Queue restore complete for {capturedGuild.Name} — {player.Queue.Count + 1} track(s) loaded.");
                    }
                    catch (Exception ex)
                    {
                        await logging.DebugAsync($"Player restore failed for {capturedGuild.Name}: {ex.Message}");
                    }
                });
            }
        }
    }

    /// <summary>
    /// Fires on every interaction (slash command, component, autocomplete). Audits slash-
    /// command usage, dispatches to the matching module via the InteractionService, and
    /// cleans up the deferred response if execution throws before replying.
    /// </summary>
    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        // One DI scope per interaction: any Scoped service a module asks for (e.g. the
        // EF Core DbContext) gets a fresh instance for just this command, disposed when
        // the scope ends here — not shared across interactions the way resolving from the
        // root _services provider would (root-resolved Scoped services live for the whole
        // process, effectively acting like singletons and accumulating stale tracked state).
        using var scope = _services.CreateScope();

        var logging = scope.ServiceProvider.GetService<LoggingService>();

        try
        {
            var context = new SocketInteractionContext(_client, interaction);

            if (interaction.Type is InteractionType.ApplicationCommand &&
                context.Interaction is SocketSlashCommand cmd)
            {
                // Previously a synchronous, blocking ADO.NET call against SQL Server, run inline
                // here before the command was even dispatched — any latency delayed every
                // command's own DeferAsync() past Discord's 3-second ack window. Backgrounded via
                // Task.Run and, now that SQL Server is fully retired, writing to Postgres via EF
                // instead. Needs its own fresh scope/DbContext here (unlike LoggingService, which
                // is a singleton) since `scope` created above is disposed once this method returns,
                // likely before this backgrounded task finishes.
                string fullName = GetFullCommandName(cmd);
                string auditUserId = context.User.Id.ToString();
                string guildOrChannel = context.Guild is not null
                    ? context.Guild.Id.ToString()
                    : context.Channel.Id.ToString();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var auditScope = _services.CreateScope();
                        var auditDb = auditScope.ServiceProvider.GetRequiredService<DiscordbotContext>();
                        await AuditService.InsertAuditAsync(auditDb, fullName, auditUserId, guildOrChannel);

                        if (logging is not null)
                            _ = logging.InfoAsync($"[Audit] OK — '{fullName}' by {auditUserId}");
                    }
                    catch (Exception auditEx)
                    {
                        if (logging is not null)
                            _ = logging.InfoAsync($"[Audit] FAILED — '{fullName}' by {auditUserId}: {auditEx.GetType().Name}: {auditEx.Message}");
                    }
                });
            }

            // DIAGNOSTIC: bracketing ExecuteCommandAsync to pin down whether dispatch is
            // hanging inside Discord.Net's own module resolution / the command's DeferAsync
            // HTTP call, vs. returning (successfully or not) and something after this point
            // silently failing. Remove once the "did not respond" root cause is found.
            if (logging is not null)
                _ = logging.DebugAsync($"[Dispatch] BEGIN ExecuteCommandAsync for {interaction.Type} from {interaction.User.Id}");

            var result = await _handler.ExecuteCommandAsync(context, scope.ServiceProvider);

            if (logging is not null)
                _ = logging.DebugAsync($"[Dispatch] END ExecuteCommandAsync — IsSuccess={result.IsSuccess} Error={result.Error} Reason={(result.IsSuccess ? "" : result.ErrorReason)}");

            if (!result.IsSuccess)
                await SendErrorAsync(interaction, result);
        }
        catch (Exception ex)
        {
            // BUG FIX: this was a bare `catch { }` that swallowed every exception from command
            // execution with zero logging — impossible to diagnose "did not respond" reports,
            // since nothing but the (now-backgrounded, unrelated) [Audit] line ever printed.
            if (logging is not null)
                _ = logging.ErrorAsync(ex);
            else
                Console.WriteLine(ex);

            if (interaction.Type is InteractionType.ApplicationCommand)
            {
                try
                {
                    var original = await interaction.GetOriginalResponseAsync();
                    await original.DeleteAsync();
                }
                catch { /* no original response existed (e.g. DeferAsync itself never ran) — nothing to clean up */ }
            }
        }
    }

    /// <summary>Builds the full "/group subcommand" name for audit logging by walking down through nested subcommand groups.</summary>
    private static string GetFullCommandName(SocketSlashCommand cmd)
    {
        var parts = new System.Text.StringBuilder(cmd.CommandName);
        var option = cmd.Data.Options?.FirstOrDefault();
        while (option is { Type: ApplicationCommandOptionType.SubCommandGroup or ApplicationCommandOptionType.SubCommand })
        {
            parts.Append(' ');
            parts.Append(option.Name);
            option = option.Options?.FirstOrDefault();
        }
        return parts.ToString();
    }

    /// <summary>Fires after a matched interaction command finishes; posts an error embed if it didn't succeed.</summary>
    private async Task HandleInteractionExecutedAsync(
        ICommandInfo info, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
            await SendErrorAsync(context.Interaction, result);
    }

    /// <summary>Posts a plain-language ephemeral error embed for a failed interaction, falling back to a followup if the initial response was already used.</summary>
    private static async Task SendErrorAsync(IDiscordInteraction interaction, IResult result)
    {
        string title = result.Error switch
        {
            InteractionCommandError.UnmetPrecondition => "Unmet Precondition",
            InteractionCommandError.BadArgs => "Bad Arguments",
            InteractionCommandError.Exception => "Command Exception",
            InteractionCommandError.Unsuccessful => "Unsuccessful",
            _ => "Error"
        };

        string body = result.Error switch
        {
            InteractionCommandError.BadArgs => "Invalid number of arguments.",
            InteractionCommandError.Unsuccessful => "Command could not be executed.",
            _ => result.ErrorReason
        };

        var embed = new EmbedHelper().BuildMessageEmbed(
            title, $"**{body}**",
            Constants.Constants.errorImageUrl,
            interaction.User.Username,
            Color.Red).Build();

        try { await interaction.RespondAsync(embed: embed, ephemeral: true); }
        catch { await interaction.FollowupAsync(embed: embed, ephemeral: true); }
    }

    /// <summary>Factory delegate Lavalink calls to construct a <see cref="CustomPlayer"/> when joining a voice channel.</summary>
    private static ValueTask<CustomPlayer> CreatePlayerAsync(
        IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);
        return ValueTask.FromResult(new CustomPlayer(properties));
    }
}
