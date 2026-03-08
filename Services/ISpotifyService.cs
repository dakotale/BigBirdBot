using DiscordBot.Models;

namespace DiscordBot.Services;

public interface ISpotifyService
{
    Task<SpotifyTrack?> GetRandomTrackAsync(string mood);
}
