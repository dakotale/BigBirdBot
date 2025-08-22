using System.Data.SqlClient;
using Discord;
using DiscordBot.Constants;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;

namespace DiscordBot.Helper
{
    /// <summary>
    /// This is the LavaLink/Audio stuff.
    /// A custom player provides some additional functionality
    /// needed like showing the track that is now playing and
    /// ended.
    /// </summary>
    public sealed class CustomPlayer : QueuedLavalinkPlayer
    {
        private readonly ITextChannel _textChannel;

        public CustomPlayer(IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties)
            : base(properties)
        {
            _textChannel = properties.Options.Value.TextChannel;
        }

        protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
        {
            await base
                .NotifyTrackStartedAsync(track, cancellationToken)
                .ConfigureAwait(false);

            TimeSpan duration = new TimeSpan();
            string artworkUrl = "";

            if (track?.Track.Duration != null)
                duration = new TimeSpan(track.Track.Duration.Hours, track.Track.Duration.Minutes, track.Track.Duration.Seconds);

            string msg = $"Track Name: **{track?.Track.Title}**\nURL: {track?.Track.Uri}\nDuration: **{duration}**\nSource: **{track?.Track.SourceName}**";

            if (track.Track.ArtworkUri != null)
                artworkUrl = track.Track.ArtworkUri.ToString();

            EmbedBuilder embed = BuildMusicEmbed("Playing", msg, artworkUrl);

            // send a message to the text channel
            await _textChannel
                .SendMessageAsync(embed: embed.Build())
                .ConfigureAwait(false);
        }

        protected override ValueTask NotifyTrackEndedAsync(ITrackQueueItem queueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
        {
            StoredProcedure stored = new StoredProcedure();

            if (queueItem != null && queueItem.Track != null)
            {
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteMusicQueue", new List<SqlParameter>
                {
                    new SqlParameter("@URL", (queueItem.Track.Uri != null) ? queueItem.Track.Uri.OriginalString : "")
                });
            }

            return base.NotifyTrackEndedAsync(queueItem, endReason, cancellationToken);
        }

        private EmbedBuilder BuildMusicEmbed(string title, string description, string artwork = "")
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - {title}",
                Color = Color.Blue,
                Description = $"{description}",
                ImageUrl = artwork
            };

            return embed;
        }
    }
}
