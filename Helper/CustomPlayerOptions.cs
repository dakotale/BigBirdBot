using Discord;
using Lavalink4NET.Players.Queued;

namespace DiscordBot.Helper
{
    public sealed record class CustomPlayerOptions : QueuedLavalinkPlayerOptions
    {
        public ITextChannel TextChannel { get; set; }
    }
}
