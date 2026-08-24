using Discord;
using DiscordBot.Constants;
using DiscordBot.Data;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Helper;

/// <summary>
/// Custom Lavalink player that sends Now Playing / Track Ended notifications
/// to a bound Discord text channel and keeps the database queue in sync.
/// </summary>
public sealed class CustomPlayer : QueuedLavalinkPlayer
{
    private readonly ITextChannel? _textChannel;
    private readonly IServiceProvider _services;

    /// <summary>Captures the bound text channel and DI service provider from the player options for later use.</summary>
    public CustomPlayer(IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties)
        : base(properties)
    {
        _textChannel = properties.Options.Value.TextChannel;
        _services = properties.Options.Value.Services;
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
            _ = Task.Run(async () =>
            {
                try
                {
                    string url = t.Uri?.OriginalString ?? "";
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DiscordbotContext>();

                    // Source proc deleted only the single lowest-MusicQueueID row matching
                    // this URL (in case of duplicate URLs queued), not every match — preserved
                    // exactly via OrderBy + Take(1) rather than a bulk delete-all-matching.
                    var row = await db.MusicQueues
                        .Where(q => q.Url == url)
                        .OrderBy(q => q.MusicQueueId)
                        .FirstOrDefaultAsync();

                    if (row is not null)
                    {
                        db.MusicQueues.Remove(row);
                        await db.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Intentionally swallowed — a failed DB cleanup must never
                    // affect playback or surface as an unhandled exception.
                }
            });
        }
    }


    /// <summary>Builds the small "Music — {title}" embed used for auto-fired track start notifications.</summary>
    private static EmbedBuilder BuildNowPlayingEmbed(string title, string description, string artwork = "") =>
        new EmbedHelper().BuildSimpleEmbed(
            $"Music — {title}", description, new Color(88, 101, 242))   // matches Audio.cs ColourDefault
            .WithImageUrl(artwork);

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
