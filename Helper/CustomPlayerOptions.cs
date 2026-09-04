using Discord;
using Lavalink4NET.Players.Queued;

namespace DiscordBot.Helper
{
    /// <summary>
    /// Player options for <see cref="CustomPlayer"/> — adds the Discord text channel the
    /// player should post Now Playing/queue notifications to, on top of the base Lavalink options.
    /// </summary>
    public sealed record class CustomPlayerOptions : QueuedLavalinkPlayerOptions
    {
        public ITextChannel? TextChannel { get; set; }

        /// <summary>
        /// Carries the EF Core music service through to <see cref="CustomPlayer"/>, which is
        /// constructed by Lavalink4NET's player factory rather than by DI, so it can't take a
        /// constructor-injected service directly.
        /// </summary>
        public MusicService? MusicService { get; set; }
    }
}

