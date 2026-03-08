using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DiscordBot.Constants;
using DiscordBot.Models;

namespace DiscordBot.Services;

/// <summary>
/// Handles Spotify Client Credentials auth and track search.
/// Register as a singleton in DI — the token is cached until it expires.
/// </summary>
public sealed class SpotifyService(IHttpClientFactory httpClientFactory) : ISpotifyService
{
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string SearchUrl = "https://api.spotify.com/v1/search";
    private const int PageSize = 50;
    private const int MaxOffset = 950;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;


    /// <summary>
    /// Returns a random track matching <paramref name="mood"/>,
    /// or <c>null</c> when Spotify returns no results or is unreachable.
    /// </summary>
    public async Task<SpotifyTrack?> GetRandomTrackAsync(string mood)
    {
        string? token = await GetTokenAsync();
        if (token is null) return null;

        return await FetchRandomTrackAsync(token, mood);
    }


    private async Task<string?> GetTokenAsync()
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        using var client = httpClientFactory.CreateClient();

        string creds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{Constants.Constants.spotifyClientId}:{Constants.Constants.spotifyClientSecret}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", creds);

        try
        {
            var response = await client.PostAsync(TokenUrl,
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                ]));

            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            _cachedToken = root.GetProperty("access_token").GetString();
            int expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30); // 30s safety buffer

            return _cachedToken;
        }
        catch
        {
            return null;
        }
    }


    private async Task<SpotifyTrack?> FetchRandomTrackAsync(string token, string mood)
    {
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        int offset = Random.Shared.Next(0, MaxOffset);
        string query = Uri.EscapeDataString($"{mood} mood");
        string url = $"{SearchUrl}?q={query}&type=track&limit={PageSize}&offset={offset}";

        try
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = doc.RootElement.GetProperty("tracks").GetProperty("items");

            var tracks = new List<SpotifyTrack>();
            foreach (var item in items.EnumerateArray())
            {
                if (ParseTrack(item) is { } t)
                    tracks.Add(t);
            }

            return tracks.Count is 0 ? null : tracks[Random.Shared.Next(tracks.Count)];
        }
        catch
        {
            return null;
        }
    }

    private static SpotifyTrack? ParseTrack(JsonElement item)
    {
        try
        {
            string name = item.GetProperty("name").GetString() ?? "";
            string url = item.GetProperty("external_urls").GetProperty("spotify").GetString() ?? "";
            int durationMs = item.GetProperty("duration_ms").GetInt32();
            bool explicit_ = item.GetProperty("explicit").GetBoolean();
            int popularity = item.GetProperty("popularity").GetInt32();

            var artists = new List<string>();
            foreach (var a in item.GetProperty("artists").EnumerateArray())
                artists.Add(a.GetProperty("name").GetString() ?? "Unknown");

            var albumEl = item.GetProperty("album");
            string album = albumEl.GetProperty("name").GetString() ?? "";
            string albumUrl = albumEl.GetProperty("external_urls").GetProperty("spotify").GetString() ?? "";

            var images = albumEl.GetProperty("images");
            string art = images.GetArrayLength() > 1 ? images[1].GetProperty("url").GetString() ?? ""
                        : images.GetArrayLength() > 0 ? images[0].GetProperty("url").GetString() ?? ""
                        : "";

            string preview = item.TryGetProperty("preview_url", out var prev)
                             && prev.ValueKind is not JsonValueKind.Null
                ? prev.GetString() ?? "" : "";

            return new SpotifyTrack(name, string.Join(" & ", artists), album,
                                    albumUrl, url, art, preview, durationMs, popularity, explicit_);
        }
        catch { return null; }
    }
}
