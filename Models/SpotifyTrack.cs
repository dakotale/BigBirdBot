namespace DiscordBot.Models;

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
