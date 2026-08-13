namespace DiscordBot.Models;

/// <summary>Plain-data snapshot of a Spotify track, as returned by <see cref="DiscordBot.Services.ISpotifyService"/> for the mood-based track picker.</summary>
public sealed record SpotifyTrack(
    string Name,
    string Artist,
    string Album,
    string AlbumUrl,
    string Url,
    string ArtworkUrl,
    string PreviewUrl,
    int DurationMs,
    int Popularity,
    bool Explicit);
