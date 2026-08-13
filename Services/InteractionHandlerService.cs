using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Microsoft.Data.SqlClient;
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
        var dt = new StoredProcedure().Select(
            Constants.Constants.discordBotConnStr, "GetPlayerConnected", []);
        if (dt.Rows.Count == 0) return;

        foreach (DataRow row in dt.Rows)
        {
            if (!ulong.TryParse(row["VoiceChannelID"]?.ToString(), out var voiceId) ||
                !ulong.TryParse(row["TextChannelID"]?.ToString(), out var textId))
                continue;

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

                // Snapshot URLs on the main thread — DataTable is not thread-safe.
                var queueUrls = new StoredProcedure()
                    .Select(Constants.Constants.discordBotConnStr, "GetMusicQueue",
                    [
                        new SqlParameter("@ServerID", guild.Id.ToString())
                    ])
                    .Rows.Cast<DataRow>()
                    .Select(r => r["URL"].ToString()!)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList();

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
                                        TextChannel = capturedText
                                    }));

                        await logging.DebugAsync(
                            $"Player restored in {capturedGuild.Name} / {capturedVoice.Name}");

                        var volDt = new StoredProcedure().Select(
                            Constants.Constants.discordBotConnStr, "GetVolume",
                            [new SqlParameter("@ServerUID", (long)capturedGuild.Id)]);

                        if (volDt.Rows.Count > 0 &&
                            int.TryParse(volDt.Rows[0]["Volume"]?.ToString(), out int savedVol))
                        {
                            await player.SetVolumeAsync(savedVol / 100f);
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
        try
        {
            var context = new SocketInteractionContext(_client, interaction);

            if (interaction.Type is InteractionType.ApplicationCommand &&
                context.Interaction is SocketSlashCommand cmd)
            {
                var logging = _services.GetService<LoggingService>();
                string fullName = GetFullCommandName(cmd);
                try
                {
                    var guildOrChannel = context.Guild is not null
                        ? context.Guild.Id.ToString()
                        : context.Channel.Id.ToString();

                    new Audit().InsertAudit(
                        fullName,
                        context.User.Id.ToString(),
                        Constants.Constants.discordBotConnStr,
                        guildOrChannel);

                    if (logging is not null)
                        _ = logging.InfoAsync($"[Audit] OK — '{fullName}' by {context.User.Id}");
                }
                catch (Exception auditEx)
                {
                    if (logging is not null)
                        _ = logging.InfoAsync($"[Audit] FAILED — '{fullName}' by {context.User.Id}: {auditEx.GetType().Name}: {auditEx.Message}");
                }
            }

            var result = await _handler.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                await SendErrorAsync(interaction, result);
        }
        catch
        {
            if (interaction.Type is InteractionType.ApplicationCommand)
            {
                await interaction
                    .GetOriginalResponseAsync()
                    .ContinueWith(t => t.Result.DeleteAsync());
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
