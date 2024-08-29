using Discord;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

namespace DiscordBot.Helper
{
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

            var embed = BuildMusicEmbed("Playing", msg, artworkUrl);

            // send a message to the text channel
            await _textChannel
                .SendMessageAsync(embed: embed.Build())
                .ConfigureAwait(false);
        }

        private EmbedBuilder BuildMusicEmbed(string title, string description, string artwork = "")
        {
            var embed = new EmbedBuilder
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
