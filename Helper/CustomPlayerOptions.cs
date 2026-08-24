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
        public ITextChannel TextChannel { get; set; }

        /// <summary>
        /// The root DI service provider. CustomPlayer is constructed directly by Lavalink4NET's
        /// player factory, not resolved from a per-interaction DI scope like a slash command
        /// module is — so it needs this to create its own short-lived scope on demand (e.g. for
        /// a scoped DbContext) rather than holding one long-lived instance for its entire life.
        /// </summary>
        public IServiceProvider Services { get; set; } = null!;
    }
}

