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
    }
}

