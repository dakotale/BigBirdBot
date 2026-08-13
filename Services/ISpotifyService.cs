using DiscordBot.Models;

namespace DiscordBot.Services;

/// <summary>Abstraction over the Spotify integration used by the mood-based track picker command.</summary>
public interface ISpotifyService
{
    /// <summary>Returns a random track matching the given mood/genre, or null if nothing matched.</summary>
    Task<SpotifyTrack?> GetRandomTrackAsync(string mood);
}
