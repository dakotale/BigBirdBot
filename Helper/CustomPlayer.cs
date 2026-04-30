using Microsoft.Data.SqlClient;
using Discord;
using DiscordBot.Constants;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;

namespace DiscordBot.Helper;

/// <summary>
/// Custom Lavalink player that sends Now Playing / Track Ended notifications
/// to a bound Discord text channel and keeps the database queue in sync.
/// </summary>
public sealed class CustomPlayer : QueuedLavalinkPlayer
{
    private readonly ITextChannel? _textChannel;

    public CustomPlayer(IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties)
        : base(properties)
    {
        _textChannel = properties.Options.Value.TextChannel;
    }

    /// <inheritdoc/>
    protected override async ValueTask NotifyTrackStartedAsync(
        ITrackQueueItem track,
        CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken).ConfigureAwait(false);

        if (_textChannel is null) return;

        var t = track.Track;
        string artwork = t.ArtworkUri?.ToString() ?? "";
        string duration = t.Duration.ToString(@"hh\:mm\:ss");
        string msg = $"**[{duration}]**\n**{t.Title}**\n{t.Uri}\n{t.SourceName.ToUpperInvariant()}";

        await _textChannel
            .SendMessageAsync(
                embed: BuildNowPlayingEmbed("Playing", msg, artwork).Build(),
                components: BuildPlaybackButtons())
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// IMPORTANT: <c>base.NotifyTrackEndedAsync</c> is called FIRST so that
    /// <see cref="QueuedLavalinkPlayer"/> always advances the queue regardless of
    /// what happens during the DB cleanup. A synchronous DB call placed before
    /// base was the root cause of queue clearing — any exception or slow query
    /// would prevent base from running, silently stalling queue progression.
    /// </remarks>
    protected override async ValueTask NotifyTrackEndedAsync(
        ITrackQueueItem queueItem,
        TrackEndReason endReason,
        CancellationToken cancellationToken = default)
    {
        // Advance the queue first — must always complete before cleanup work.
        await base.NotifyTrackEndedAsync(queueItem, endReason, cancellationToken).ConfigureAwait(false);

        // DB cleanup is fire-and-forget; never let it block or throw into the event loop.
        if (queueItem?.Track is { } t)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    new StoredProcedure().UpdateCreate(
                        Constants.Constants.discordBotConnStr,
                        "DeleteMusicQueue",
                        [new SqlParameter("@URL", t.Uri?.OriginalString ?? "")]);
                }
                catch
                {
                    // Intentionally swallowed — a failed DB cleanup must never
                    // affect playback or surface as an unhandled exception.
                }
            });
        }
    }


    private static EmbedBuilder BuildNowPlayingEmbed(string title, string description, string artwork = "") =>
        new EmbedBuilder()
            .WithTitle($"Music — {title}")
            .WithColor(new Color(88, 101, 242))   // matches Audio.cs ColourDefault
            .WithDescription(description)
            .WithImageUrl(artwork)
            .WithCurrentTimestamp();

    /// <summary>
    /// Builds the same two-row playback button row used by the slash commands
    /// so that auto-fired Now Playing messages are also interactive.
    /// </summary>
    private static MessageComponent BuildPlaybackButtons() =>
        new ComponentBuilder()
            .WithButton("Pause", "audio:pause", ButtonStyle.Primary, new Emoji("⏸️"), row: 0)
            .WithButton("Skip", "audio:skip", ButtonStyle.Secondary, new Emoji("⏭️"), row: 0)
            .WithButton("Stop", "audio:stop", ButtonStyle.Danger, new Emoji("⏹️"), row: 0)
            .WithButton("Shuffle", "audio:shuffle", ButtonStyle.Secondary, new Emoji("🔀"), row: 0)
            .WithButton("Loop ×1", "audio:loop1", ButtonStyle.Secondary, new Emoji("🔁"), row: 0)
            .WithButton("Vol −", "audio:vol_down", ButtonStyle.Secondary, new Emoji("🔉"), row: 1)
            .WithButton("Vol +", "audio:vol_up", ButtonStyle.Secondary, new Emoji("🔊"), row: 1)
            .WithButton("Queue", "audio:queue", ButtonStyle.Secondary, new Emoji("📋"), row: 1)
            .Build();
}
