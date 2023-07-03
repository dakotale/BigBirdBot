using Discord;
using Discord.WebSocket;
using Google.Apis.Upload;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Victoria.Node;
using Victoria.Player;
using Victoria.Responses.Search;

namespace DiscordBot.Helper
{
    public class SpotifyHelper
    {
        private LavaNode Node { get; set; }
        public SpotifyClient Spotify { get; }

        public SpotifyHelper(LavaNode lavaNode)
        {
            Node = lavaNode;

            var config = SpotifyClientConfig.CreateDefault();
            var request = new ClientCredentialsRequest(Constants.Constants.spotifyClientId, Constants.Constants.spotifyClientSecret);
            var response = new OAuthClient(config).RequestToken(request);
            var spotify = new SpotifyClient(config.WithToken(response.Result.AccessToken));
            Spotify = spotify;
        }

        /// <summary>
        /// Searches Spotify for tracks
        /// </summary>
        /// <param name="channel">Channel in which to send to</param>
        /// <param name="url">Spotify URL</param>
        /// <returns><see cref="List{T}"/> of tracks (track name + artists' name) or null if not found</returns>
        public async Task<bool> IsSpotifyUrl(string url)
        {
            Regex r = new Regex(@"https?:\/\/(?:open\.spotify\.com)\/(?<type>\w+)\/(?<id>[\w-]{22})(?:\?si=(?:[\w-]{22}))?");
            var match = r.Match(url);

            return match.Success;
        }

        public async Task<List<string>> SearchSpotify(ISocketMessageChannel channel, string url)
        {
            Regex r = new Regex(@"https?:\/\/(?:open\.spotify\.com)\/(?<type>\w+)\/(?<id>[\w-]{22})(?:\?si=(?:[\w-]{22}))?");
            if (!r.Match(url).Success)
            {
                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Spotify",
                    Color = Color.Blue,
                    Description = $"Invalid Spotify link.",
                    ThumbnailUrl = ""//"https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png",
                };

                await channel.SendMessageAsync(embed: embed.Build());
                return null;
            }

            string type = r.Match(url).Groups["type"].Value;
            string id = r.Match(url).Groups["id"].Value;
            List<string> tracks = new List<string>();

            switch (type)
            {
                case "album":
                    await foreach (var item in Spotify.Paginate((await Spotify.Albums.Get(id)).Tracks))
                    {
                        tracks.Add($"{item.Name} {string.Join(" ", item.Artists.Select(x => x.Name))}");
                    }
                    break;

                case "playlist":
                    var playlist = await Spotify.Playlists.Get(id);
                    await foreach (var item in Spotify.Paginate(playlist.Tracks))
                    {
                        if (item.Track is FullTrack track)
                        {
                            tracks.Add($"{track.Name} {string.Join(" ", track.Artists.Select(x => x.Name))}");
                        }
                    }
                    break;

                case "track":
                    var trackItem = await Spotify.Tracks.Get(id);
                    tracks.Add($"{trackItem.Name} {string.Join(" ", trackItem.Artists.Select(x => x.Name))}");
                    break;

                default:
                    var embed = new EmbedBuilder
                    {
                        Title = $"BigBirdBot Music - Spotify",
                        Color = Color.Blue,
                        Description = $"Must be a `track`, `playlist`, or `album`.",
                        ThumbnailUrl = "",
                    };

                    await channel.SendMessageAsync(embed: embed.Build());

                    return null;
            }

            return tracks;
        }
    }
}
