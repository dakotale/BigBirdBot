using Discord.Commands;
using Discord;
using DiscordBot.Services;
using System.Text;
using Victoria.Node;
using Victoria.Player;
using Victoria.Responses.Search;
using Victoria;
using System.Runtime.InteropServices;
using DiscordBot.Helper;
using System.Globalization;
using DiscordBot.Constants;
using System.Data;
using System.Data.SqlClient;
using Microsoft.VisualBasic;
using SpotifyAPI.Web;
using System;
using KillersLibrary.Services;
using System.Collections.Concurrent;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    /*
     * TODO:
     * - Cleanup code to make more organized
     */
    public sealed class AudioModule : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();
        private readonly LavaNode _lavaNode;
        private readonly AudioService _audioService;
        private readonly SpotifyHelper _spotifyHelper;

        public EmbedPagesService EmbedPagesService { get; set; }
        public MultiButtonsService MultiButtonsService { get; set; }

        private static readonly IEnumerable<int> Range = Enumerable.Range(1900, 2000);
        private static Dictionary<string, double[]> EqBands = new()
        {
            { "superbass", new[] { 1, 1, 1, 1, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, 1, 1, 1, 1 } },
            { "bass", new[] { 0.10, 0.10, 0.05, 0.05, 0.05, -0.05, -0.05, 0, -0.05, -0.05, 0, 0.05, 0.05, 0.10, 0.10 }},
            { "pop", new[] { -0.01, -0.01, 0, 0.01, 0.02, 0.05, 0.07, 0.10, 0.07, 0.05, 0.02, 0.01, 0, -0.01, -0.01 }},
            { "off", null }
        };

        public AudioModule(LavaNode lavaNode, AudioService audioService, SpotifyHelper spotifyHelper)
        {
            _lavaNode = lavaNode;
            _audioService = audioService;
            _spotifyHelper = spotifyHelper;
        }

        [Command("Join", RunMode = RunMode.Async)]
        [Alias("j")]
        [Discord.Commands.Summary("Bot joins the voice channel to play audio.")]
        public async Task JoinAsync()
        {
            audit.InsertAudit("join", Context.User.Username, Constants.Constants.discordBotConnStr);
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                var embed = BuildMusicEmbed("Join", "You must be connected to a voice channel");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (_lavaNode.HasPlayer(Context.Guild))
            {
                var embed = BuildMusicEmbed("Join", "I'm already connected to a voice channel!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                var player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                var embed = BuildMusicEmbed("Play", $"Thank you for having me, as a heads up the current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**!");
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Leave", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Bot leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            audit.InsertAudit("leave", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Leave", "I'm not connected to a voice channel!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null)
            {
                var embed = BuildMusicEmbed("Leave", "Not sure which voice channel to disconnect from.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                await _lavaNode.LeaveAsync(voiceChannel);
                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Leave",
                    Color = Color.Blue,
                    Description = $"Bye, have a beautiful time",
                    ThumbnailUrl = "https://static.wikia.nocookie.net/americandad/images/d/d0/Officer_Pena.jpg/revision/latest?cb=20100228182532",
                };

                embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                        .WithCurrentTimestamp();
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Discord.Commands.Summary("Bot will join (if not already in) and play audio from Youtube, Soundcloud, Spotify, and local files.")]
        public async Task PlayAsync([Optional] string searchQuery)
        {
            audit.InsertAudit("play", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                if (Context.Message.Attachments.Count > 0)
                {
                    foreach (var a in Context.Message.Attachments)
                    {
                        searchQuery = a.Url;
                    }
                }
                else
                {
                    var embed = BuildMusicEmbed("Play", "Please provide search terms.");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }
            }
            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    string msg = Context.Message.Author.Mention + ", you must be connected to a voice channel!";
                    var embed = BuildMusicEmbed("Play", msg);
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                try
                {
                    player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                    //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                    var embed = BuildMusicEmbed("Play", $"Thank you for having me, as a heads up the current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**!");
                    await ReplyAsync(embed: embed.Build());
                }
                catch (Exception exception)
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }

            if (_spotifyHelper.IsSpotifyUrl(searchQuery).Result)
            {
                try
                {
                    SpotifyHelper spotify = new SpotifyHelper(_lavaNode);
                    var tracks = await spotify.SearchSpotify(Context.Channel, searchQuery);
                    if (tracks != null)
                    {
                        if (player != null)
                        {
                            string msg = "";
                            //It's a playlist
                            if (tracks.Count > 1)
                            {
                                List<LavaTrack?> lavaTracks = new List<LavaTrack?>();
                                var serverId = player.VoiceChannel.GuildId.ToString();

                                // Load the first 5 to get something going
                                for (int i = 0; i < 5; i++)
                                {
                                    var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[i]);
                                    if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                    {
                                        var track = node.Tracks.FirstOrDefault();
                                        if (track != null)
                                        {
                                            AddMusicTable(track, serverId, Context.Message.Author.Username);
                                            lavaTracks.Add(track);
                                        }
                                    }
                                }

                                player.Vueue.Enqueue(lavaTracks);

                                msg = "Loaded the first five Spotify track(s) successfully, queueing the rest of the playlist.";

                                var embed = BuildMusicEmbed("Playlist Loaded", msg);
                                await ReplyAsync(embed: embed.Build());

                                if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                {
                                    return;
                                }

                                player.Vueue.TryDequeue(out var lavaTrack);
                                await player.PlayAsync(lavaTrack);

                                lavaTracks = new List<LavaTrack?>();
                                // Load the rest while the first 5 play
                                for (int i = 5; i < tracks.Count; i++)
                                {
                                    var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[i]);
                                    if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                    {
                                        var track = node.Tracks.FirstOrDefault();
                                        if (track != null)
                                        {
                                            AddMusicTable(track, serverId, Context.Message.Author.Username);
                                            lavaTracks.Add(track);
                                        }
                                    }
                                }

                                player.Vueue.Enqueue(lavaTracks);

                                msg = "The remaining tracks are loaded in queue.";
                                embed = BuildMusicEmbed("Playlist Loaded", msg);
                                await ReplyAsync(embed: embed.Build());
                            }
                            // It's not a playlist
                            else
                            {
                                var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[0]);
                                if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                {
                                    var track = node.Tracks.FirstOrDefault();
                                    player.Vueue.Enqueue(track);
                                    var serverId = player.VoiceChannel.GuildId.ToString();

                                    msg = $"One **{track?.Title}** coming right up!\nURL: {track?.Url}\n Duration: {track?.Duration}\n Source: {track?.Source.ToUpper()}";

                                    var embed = BuildMusicEmbed("Track Loaded", msg);
                                    await ReplyAsync(embed: embed.Build());

                                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                    {
                                        var queueEmbed = BuildMusicEmbed("Queue", "Item was added to the queue because something is currently playing.");
                                        await ReplyAsync(embed: queueEmbed.Build());
                                        return;
                                    }
                                    player.Vueue.TryDequeue(out var lavaTrack);
                                    AddMusicTable(track, serverId, Context.Message.Author.Username);
                                    await player.PlayAsync(lavaTrack);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var embed = BuildMusicEmbed("Spotify", ex.Message);
                    await ReplyAsync(embed: embed.Build());
                }
            }
            else
            {
                var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                {
                    string msg = $"I wasn't able to find anything for '{searchQuery}'.";
                    var embed = BuildMusicEmbed("Play", msg);
                    await ReplyAsync(embed: embed.Build());

                    return;
                }

                var serverId = player.VoiceChannel.GuildId.ToString();
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    foreach (var track in searchResponse.Tracks)
                        AddMusicTable(track, serverId, Context.Message.Author.Username);

                    player.Vueue.Enqueue(searchResponse.Tracks);

                    string msg = $"Queued up {searchResponse.Tracks.Count} songs!";
                    var embed = BuildMusicEmbed("Playlist Loaded", msg);
                    await ReplyAsync(embed: embed.Build());
                }
                else
                {
                    LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                    AddMusicTable(track, serverId, Context.Message.Author.Username);
                    player.Vueue.Enqueue(track);

                    string msg = $"**{track?.Title}** coming right up!\nURL: {track?.Url}\nDuration: **{track?.Duration}**\nSource: **{track?.Source.ToUpper()}**\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**";

                    var embed = BuildMusicEmbed("Track Loaded", msg);
                    await ReplyAsync(embed: embed.Build());

                }
                if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                {
                    var embed = BuildMusicEmbed("Queue", "Item was added to the queue because something is currently playing.");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }
                player.Vueue.TryDequeue(out var lavaTrack);
                await player.SetVolumeAsync(0);
                await player.PlayAsync(lavaTrack);
            }
        }

        [Command("Pause", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Pause the current audio playing.")]
        public async Task PauseAsync()
        {
            audit.InsertAudit("pause", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Pause", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Pause", "I cannot pause when I'm not playing anything!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.PauseAsync();

                string msg = $"Paused: **{player.Track.Title}**";
                var embed = BuildMusicEmbed("Pause", msg);
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Resume", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Resume the current audio that is paused.")]
        public async Task ResumeAsync()
        {
            audit.InsertAudit("resume", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Resume", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Paused)
            {
                var embed = BuildMusicEmbed("Resume", "I cannot resume when I'm not playing anything!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.ResumeAsync();
                string msg = $"Resumed: {player.Track.Title}";
                var embed = BuildMusicEmbed("Resume", msg);
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Stop", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Stops the audio, clears the queue, and the bot will leave the voice channel.")]
        public async Task StopAsync()
        {
            audit.InsertAudit("stop", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Stop", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState == PlayerState.Stopped)
            {
                var embed = BuildMusicEmbed("Stop", "Woaaah there, I can't stop the stopped forced!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.StopAsync();

                var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;
                if (voiceChannel != null)
                {
                    await _lavaNode.LeaveAsync(voiceChannel);
                }
                var embed = BuildMusicEmbed("Stop", "Fine, I guess I'll shut up.");
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("forceskip", RunMode = RunMode.Async), Alias("fs", "fskip", "skip")]
        [Discord.Commands.Summary("Skip the current track playing in the bot.")]
        public async Task ForceSkipTaskAsync()
        {
            audit.InsertAudit("skip", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Skip", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Skip", "Woaaah there, I can't skip when nothing is playing!");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                if (player.Vueue.Count > 0)
                {
                    var currentTrack = player.Track;
                    string msg = $"Skipped: **{currentTrack.Title}**";
                    await player.StopAsync();
                    await player.ResumeAsync();
                    var embed = BuildMusicEmbed("Skip", msg);
                    await ReplyAsync(embed: embed.Build());
                }
                else
                {
                    await player.StopAsync();
                    string msg = $"The last item in the queue was skipped and there is nothing playing.";
                    var embed = BuildMusicEmbed("Skip", msg);
                    await ReplyAsync(embed: embed.Build());
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Seek", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Go to a specific section of the current track playing.")]
        public async Task SeekAsync(TimeSpan timeSpan)
        {
            audit.InsertAudit("seek", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Seek", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Seek", "Woaaah there, I can't seek when nothing is playing.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.SeekAsync(timeSpan);
                string msg = $"I've seeked `{player.Track.Title}` to {timeSpan}.";
                var embed = BuildMusicEmbed("Seek", msg);
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Volume", RunMode = RunMode.Async), Alias("Vol")]
        [Discord.Commands.Summary("Set the volume between 0 and 150.")]
        public async Task VolumeAsync([Remainder] string? volume = null)
        {
            audit.InsertAudit("volume", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Volume", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }
            
            try
            {
                StoredProcedure procedure = new StoredProcedure();
                var guildId = Int64.Parse(player.VoiceChannel.GuildId.ToString());

                if (string.IsNullOrWhiteSpace(volume))
                {
                    DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetVolume", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerUID", guildId)
                    });

                    if (dt.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            // Display current volume
                            string msg = $"The current player volume is **{dr["Volume"].ToString()} out of 150**.";
                            var embed = BuildMusicEmbed("Volume", msg);
                            await ReplyAsync(embed: embed.Build());
                        }
                    }
                }
                else
                {
                    if(ushort.TryParse(volume, out var vol))
                    {
                        if (vol > 150 || vol < 0)
                        {
                            var errorEmbed = BuildMusicEmbed("Volume", "The volume must be between 0 and 150!");
                            await ReplyAsync(embed: errorEmbed.Build());
                        }

                        await player.SetVolumeAsync(vol);

                        procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateVolume", new List<SqlParameter>
                        {
                            new SqlParameter("@ServerUID", guildId),
                            new SqlParameter("@Volume", int.Parse(volume))
                        });

                        string msg = $"I've changed the player volume to {volume}.";
                        var embed = BuildMusicEmbed("Volume", msg);
                        await ReplyAsync(embed: embed.Build());
                    }
                    else
                    {
                        var embed = BuildMusicEmbed("Volume", "Please enter a volume between 0 and 150.");
                        await ReplyAsync(embed: embed.Build());
                    }
                }

                
            }
            catch (Exception exception)
            {
                var embed = BuildMusicEmbed("Volume", exception.Message);
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("NowPlaying", RunMode = RunMode.Async), Alias("Np")]
        [Discord.Commands.Summary("View the current playing track in the bot.")]
        public async Task NowPlayingAsync()
        {
            audit.InsertAudit("nowplaying", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Now Playing", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Now Playing", "Woaaah there, I'm not playing any tracks.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            var track = player.Track;
            var artwork = await track.FetchArtworkAsync();

            var npEmbed = new EmbedBuilder()
            {
                Title = $"BigBirdBot Music - Now Playing: **{track.Title}**",
                Color = Color.Blue,
            };

            var position = new TimeSpan(track.Position.Hours, track.Position.Minutes, track.Position.Seconds);
            var duration = new TimeSpan(track.Duration.Hours, track.Duration.Minutes, track.Duration.Seconds);

            npEmbed.WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
            .WithImageUrl(artwork)
            //.WithThumbnailUrl("https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png")
            .WithFooter($"{position}/{duration}");

            await ReplyAsync(embed: npEmbed.Build());
        }

        [Command("queue", RunMode = RunMode.Async), Alias("q")]
        [Discord.Commands.Summary("View the queue that is set for the bot to play.")]
        public async Task GetQueue()
        {
            audit.InsertAudit("queue", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Queue", "Woaaah there, I'm not playing any tracks.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.Vueue.Count == 0)
            {
                var track = player.Track;
                var artwork = await track.FetchArtworkAsync();

                var npEmbed = new EmbedBuilder()
                {
                    Title = $"BigBirdBot Music - Queue: **{track.Title}**",
                    Color = Color.Blue,
                };

                var position = new TimeSpan(track.Position.Hours, track.Position.Minutes, track.Position.Seconds);
                var duration = new TimeSpan(track.Duration.Hours, track.Duration.Minutes, track.Duration.Seconds);

                npEmbed.WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
                .WithImageUrl(artwork)
                //.WithThumbnailUrl("https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png")
                .WithFooter($"{position}/{duration}");

                await ReplyAsync(embed: npEmbed.Build());
            }

            if (player.Vueue.Count > 0)
            {
                string queue = "";
                List<EmbedBuilder> list = new();
                EmbedBuilder embedBuilder = new();
                int i = 0;
                foreach (var p in player.Vueue)
                {
                    i++;
                    queue += i.ToString() + ". **" + p.Title + "** - " + p.Duration + " \n " + p.Url + "\n\n";

                    if (i % 30 == 0)
                    {
                        embedBuilder.WithTitle("BigBirdBot - Queue");
                        embedBuilder.WithDescription(queue);
                        //embedBuilder.WithThumbnailUrl("https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png");
                        embedBuilder.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username);
                        embedBuilder.Color = Discord.Color.Blue;
                        embedBuilder.WithCurrentTimestamp();
                        list.Add(embedBuilder);
                        queue = "";
                    }
                }
                embedBuilder = new();
                embedBuilder.WithTitle($"BigBirdBot - Queue - {player.Vueue.Count } total");
                embedBuilder.WithDescription(queue);
                //embedBuilder.WithThumbnailUrl("https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png");
                embedBuilder.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username);
                embedBuilder.Color = Discord.Color.Blue;
                embedBuilder.WithCurrentTimestamp();
                list.Add(embedBuilder);

                EmbedPagesStyles style = new();
                style.FirstLabel = "«";
                style.BackLabel = "‹";
                style.DeletionEmoji = "🗑";
                style.ForwardLabel = "›";
                style.LastLabel = "»";
                style.BtnColor = ButtonStyle.Primary;
                style.DeletionBtnColor = ButtonStyle.Danger;
                style.SkipBtnColor = ButtonStyle.Primary;
                style.FastChangeBtns = false; // Do you want there to be a button that goes directly to either ends?
                style.PageNumbers = true; //Do you want the embed to have page numbers like "Page: 1/4"? Depends on how many pages you have.

                try
                {
                    await EmbedPagesService.CreateEmbedPages(Context.Client, list, null, Context, null, style);
                }
                catch (Exception ex)
                {
                    /* Eat it */
                }
            }

        }

        [Command("equalizer", RunMode = RunMode.Async), Alias("eq")]
        [Discord.Commands.Summary("Sets the audio EQ with three settings; Super bass, bass, and pop")]
        public async Task GetEqualizer([Remainder] string? eq = null)
        {
            audit.InsertAudit("equalizer", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                return;
            }

            _lavaNode.TryGetPlayer(Context.Guild, out var player);

            if (eq == null)
            {
                string eqmsg = "";

                if (EQHelper.CurrentEQ == "Off")
                {
                    eqmsg = "No EQ Applied.";
                }
                else
                {
                    eqmsg = $"Current EQ is: `{EQHelper.CurrentEQ}`";
                }

                var embed = BuildMusicEmbed("Equalizer", eqmsg);
                await ReplyAsync(embed: embed.Build());

                return;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;

            if (!EqBands.ContainsKey(eq))
            {
                var sb = new StringBuilder();

                var keys = EqBands.Keys.ToList();

                for (var i = 0; i < keys.Count; i++)
                {
                    keys[i] = $"`{keys[i]}`";
                }

                var eqMsg = $"Valid EQ modes: {string.Join(", ", keys)}";
                var embed = BuildMusicEmbed("Equalizer", eqMsg);

                await ReplyAsync(embed: embed.Build());
            }

            var bands = EQHelper.BuildEQ(EqBands[eq]);

            EQHelper.CurrentEQ = textInfo.ToTitleCase(eq);
            await player.EqualizerAsync(bands);

            var msg = (EQHelper.CurrentEQ == "Off") ? "EQ turned off" : $"`{EQHelper.CurrentEQ}`: working my magic!";
            var resultEmbed = BuildMusicEmbed("Equalizer", msg);
            await ReplyAsync(embed: resultEmbed.Build());
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Repeats the current track")]
        public async Task RepeatTrack()
        {
            audit.InsertAudit("repeat", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                string msg = Context.Message.Author.Mention + ", you must be connected to a voice channel!";
                var errorEmbed = BuildMusicEmbed("Repeat", msg);
                await ReplyAsync(embed: errorEmbed.Build());
                return;
            }

            var track = player.Track;
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;

            if (voiceChannel != null)
            {
                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Repeat",
                    Color = Color.Blue,
                    Description = $"Repeating {track.Title}",
                    ThumbnailUrl = "",
                };

                embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                        .WithCurrentTimestamp();
                await ReplyAsync(embed: embed.Build());

                // Get current track and loop it until said otherwise /shrug
                player.Vueue.Enqueue(track);
                if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.Paused)
                {
                    return;
                }
                player.Vueue.TryDequeue(out var lavaTrack);
                await player.PlayAsync(lavaTrack);
            }
        }

        [Command("loop", RunMode = RunMode.Async)]
        [Discord.Commands.Summary("Loops the current track X number of times")]
        public async Task LoopTrack([Remainder] int times)
        {
            audit.InsertAudit("loop", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                string msg = Context.Message.Author.Mention + ", you must be connected to a voice channel!";
                var errorEmbed = BuildMusicEmbed("Repeat", msg);
                await ReplyAsync(embed: errorEmbed.Build());
                return;
            }

            var track = player.Track;
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;

            if (voiceChannel != null)
            {
                if (times > 0)
                {
                    var embed = new EmbedBuilder
                    {
                        Title = $"BigBirdBot Music - Loop",
                        Color = Color.Blue,
                        Description = $"Looping {track.Title} {times.ToString()} times.",
                        ThumbnailUrl = "",
                    };

                    embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                        .WithCurrentTimestamp();
                    await ReplyAsync(embed: embed.Build());
                    for (int i = 0; i < times; i++)
                    {
                        // Get current track and loop it until said otherwise /shrug
                        player.Vueue.Enqueue(track);
                    }
                }
                else
                {
                    var embed = new EmbedBuilder
                    {
                        Title = $"BigBirdBot Music - Loop",
                        Color = Color.Blue,
                        Description = $"Please enter a number greater than 0.",
                        ThumbnailUrl = Constants.Constants.errorImageUrl,
                    };

                    embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                        .WithCurrentTimestamp();
                    await ReplyAsync(embed: embed.Build());
                }
            }
            else
            {
                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Loop",
                    Color = Color.Blue,
                    Description = $"The bot must be connected to your voice channel to run this command.",
                    ThumbnailUrl = Constants.Constants.errorImageUrl,
                };

                embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();
                await ReplyAsync(embed: embed.Build());
            }
        }

        //[Command("stay", RunMode = RunMode.Async)]
        //[Discord.Commands.Summary("Bot will stay in the VC for as long as you want.")]
        //public async Task BotStay()
        //{
        //    audit.InsertAudit("stay", Context.User.Username, Constants.Constants.discordBotConnStr);
        //    bool stayFlag = false;
        //    StoredProcedure stored = new StoredProcedure();
        //    DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetStayFlag", new List<System.Data.SqlClient.SqlParameter> 
        //    { 
        //        new System.Data.SqlClient.SqlParameter("@ServerUID", Int64.Parse(Context.Guild.Id.ToString())) 
        //    });
            
        //    stayFlag = bool.Parse(dt.Rows[0]["StayInVC"].ToString());

        //    using (var con = new SqlConnection(Constants.Constants.discordBotConnStr))
        //    using (var cmd = new SqlCommand("UpdateStayFlag", con))
        //    {
        //        cmd.CommandType = CommandType.StoredProcedure;

        //        cmd.Parameters.Add("@ServerUID", SqlDbType.BigInt).Value = Int64.Parse(Context.Guild.Id.ToString());
        //        cmd.Parameters.Add("@StayFlag", SqlDbType.Bit).Value = !stayFlag;

        //        con.Open();
        //        cmd.ExecuteNonQuery();
        //    }

        //    stayFlag = !stayFlag;

        //    if (stayFlag)
        //    {
        //        var embed = new EmbedBuilder
        //        {
        //            Title = $"BigBirdBot Music - Stay",
        //            Color = Color.Blue,
        //            Description = $"The bot is now going to stay here.",
        //            ThumbnailUrl = "",
        //        };

        //        embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
        //                            .WithCurrentTimestamp();
        //        await ReplyAsync(embed: embed.Build());
        //    }
        //    else
        //    {
        //        var embed = new EmbedBuilder
        //        {
        //            Title = $"BigBirdBot Music - Stay",
        //            Color = Color.Blue,
        //            Description = $"The bot is now going to leave on the next track.",
        //            ThumbnailUrl = "",
        //        };

        //        embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
        //                            .WithCurrentTimestamp();
        //        await ReplyAsync(embed: embed.Build());
        //    }
            
        //}

        [Command("swap", RunMode = RunMode.Async)]
        public async Task SwapTrack([Remainder] string swap)
        {
            audit.InsertAudit("swap", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            EmbedHelper embedHelper = new EmbedHelper();

            if (swap.Contains(","))
            {
                var swapList = swap.Split(',', StringSplitOptions.TrimEntries);
                if (swapList.Length == 2)
                {
                    string oldSwap = swapList[0];
                    string newSwap = swapList[1];

                    if (int.TryParse(oldSwap, out int swapOne) && int.TryParse(newSwap, out int swapTwo))
                    {
                        swapOne--;
                        swapTwo--;
                        if (player.Vueue.ElementAt(swapOne) != null && player.Vueue.ElementAt(swapTwo) != null)
                        {
                            var itemList = player.Vueue.ToList();
                            var val = itemList[swapOne];
                            itemList[swapOne] = itemList[swapTwo];
                            itemList[swapTwo] = val;

                            player.Vueue.Clear();
                            player.Vueue.Enqueue(itemList);

                            var embed = BuildMusicEmbed("Swap", $"Successfully swapped **{itemList[swapOne].Title}** and **{itemList[swapTwo].Title}** in the queue.");
                            await ReplyAsync(embed: embed.Build());
                        }
                        else
                        {
                            var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "Both elements must be present in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                            await ReplyAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "The values entered must be whole numbers.\nExample -swap 5, 10", Constants.Constants.errorImageUrl, "", Color.Red, "");
                        await ReplyAsync(embed: embed.Build());
                    }
                }
                else
                {
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "There must be two positions included in your message separated with a comma.\nExample -swap 5, 10", Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "There must be two positions included in your message separated with a comma.\nExample -swap 5, 10", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("shuffle", RunMode = RunMode.Async)]
        public async Task ShuffleVueue()
        {
            audit.InsertAudit("shuffle", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.Vueue.Count > 1)
            {
                player.Vueue.Shuffle();

                var embed = BuildMusicEmbed("Shuffle", "Queue Shuffled");
                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have more than one element in it to shuffle.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("clear", RunMode = RunMode.Async)]
        public async Task ClearQueue()
        {
            audit.InsertAudit("clear", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            if (player.Vueue.Count > 0)
            {
                player.Vueue.Clear();

                var embed = BuildMusicEmbed("Clear", "Queue is now empty");
                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have one element to clear.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("remove", RunMode = RunMode.Async)]
        public async Task RemoveItem([Remainder] int element)
        {
            audit.InsertAudit("remove", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await ReplyAsync(embed: embed.Build());
                return;
            }

            try
            {
                element--;
                var item = player.Vueue.RemoveAt(element);

                var embed = BuildMusicEmbed("Remove", $"Removed {item.Title} from the queue");
                await ReplyAsync(embed: embed.Build());
            }
            catch
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The element does not exist in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("playnext", RunMode = RunMode.Async)]
        [Alias("pn")]
        public async Task PlayNext([Optional] string searchQuery)
        {
            audit.InsertAudit("playnext", Context.User.Username, Constants.Constants.discordBotConnStr);
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                if (Context.Message.Attachments.Count > 0)
                {
                    foreach (var a in Context.Message.Attachments)
                    {
                        searchQuery = a.Url;
                    }
                }
                else
                {
                    var embed = BuildMusicEmbed("Playnext", "Please provide search terms.");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }
            }
            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    string msg = Context.Message.Author.Mention + ", you must be connected to a voice channel!";
                    var embed = BuildMusicEmbed("Playnext", msg);
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                try
                {
                    player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                    //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                    var embed = BuildMusicEmbed("Play", $"Thank you for having me, as a heads up the current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**!");
                    await ReplyAsync(embed: embed.Build());
                }
                catch (Exception exception)
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }

            var serverId = player.VoiceChannel.GuildId.ToString();

            if (player.Vueue.Count > 0)
            {
                if (_spotifyHelper.IsSpotifyUrl(searchQuery).Result)
                {
                    try
                    {
                        SpotifyHelper spotify = new SpotifyHelper(_lavaNode);
                        var tracks = await spotify.SearchSpotify(Context.Channel, searchQuery);
                        if (tracks != null)
                        {
                            if (player != null)
                            {
                                string msg = "";
                                var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[0]);
                                if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                {
                                    var track = node.Tracks.FirstOrDefault();

                                    var itemList = player.Vueue.ToList();
                                    itemList.Insert(0, track);

                                    AddMusicTable(track, serverId, Context.Message.Author.Username);

                                    player.Vueue.Clear();
                                    player.Vueue.Enqueue(itemList);

                                    msg = $"**{track?.Title}** coming right up!\nURL: {track?.Url}\nDuration: {track?.Duration}\nSource: {track?.Source.ToUpper()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**";

                                    var embed = BuildMusicEmbed("Track Loaded", msg);
                                    await ReplyAsync(embed: embed.Build());

                                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                    {
                                        var queueEmbed = BuildMusicEmbed("Queue", "Item was added to the top of the queue because something is currently playing.");
                                        await ReplyAsync(embed: queueEmbed.Build());
                                        return;
                                    }
                                    player.Vueue.TryDequeue(out var lavaTrack);
                                    await player.PlayAsync(lavaTrack);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var embed = BuildMusicEmbed("Spotify", ex.Message);
                        await ReplyAsync(embed: embed.Build());
                    }
                }
                else
                {
                    var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                    if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                    {
                        string msgMusic = $"I wasn't able to find anything for '{searchQuery}'.";
                        var embedMusic = BuildMusicEmbed("Playnext", msgMusic);
                        await ReplyAsync(embed: embedMusic.Build());

                        return;
                    }
                    
                    LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                    AddMusicTable(track, serverId, Context.Message.Author.Username);
                    var itemList = player.Vueue.ToList();
                    itemList.Insert(0, track);

                    player.Vueue.Clear();
                    player.Vueue.Enqueue(itemList);

                    string msg = $"**{track?.Title}** coming right up!\nURL:  {track?.Url} \nDuration:  {track?.Duration} \nSource: {track?.Source.ToUpper()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**";

                    var embed = BuildMusicEmbed("Track Loaded", msg);
                    await ReplyAsync(embed: embed.Build());

                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                    {
                        var embedMusic = BuildMusicEmbed("Queue", "Item was added to the top of the queue because something is currently playing.");
                        await ReplyAsync(embed: embedMusic.Build());
                        return;
                    }
                    player.Vueue.TryDequeue(out var lavaTrack);
                    await player.PlayAsync(lavaTrack);
                }
            }
            // Treat this just like a normal -play
            else
            {
                if (_spotifyHelper.IsSpotifyUrl(searchQuery).Result)
                {
                    try
                    {
                        SpotifyHelper spotify = new SpotifyHelper(_lavaNode);
                        var tracks = await spotify.SearchSpotify(Context.Channel, searchQuery);
                        if (tracks != null)
                        {
                            if (player != null)
                            {
                                string msg = "";
                                //It's a playlist
                                if (tracks.Count > 1)
                                {
                                    List<LavaTrack?> lavaTracks = new List<LavaTrack?>();

                                    // Load the first 5 to get something going
                                    for (int i = 0; i < 5; i++)
                                    {
                                        var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[i]);
                                        if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                        {
                                            var track = node.Tracks.FirstOrDefault();
                                            if (track != null)
                                            {
                                                AddMusicTable(track, serverId, Context.Message.Author.Username);
                                                lavaTracks.Add(track);
                                            }
                                        }
                                    }

                                    player.Vueue.Enqueue(lavaTracks);

                                    msg = "Loaded the first five Spotify track(s) successfully, queuing the rest of the playlist.";

                                    var embed = BuildMusicEmbed("Playlist Loaded", msg);
                                    await ReplyAsync(embed: embed.Build());

                                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                    {
                                        return;
                                    }

                                    player.Vueue.TryDequeue(out var lavaTrack);
                                    await player.PlayAsync(lavaTrack);

                                    lavaTracks = new List<LavaTrack?>();
                                    // Load the rest while the first 5 play
                                    for (int i = 5; i < tracks.Count; i++)
                                    {
                                        var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[i]);
                                        if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                        {
                                            var track = node.Tracks.FirstOrDefault();
                                            if (track != null)
                                            {
                                                AddMusicTable(track, serverId, Context.Message.Author.Username);
                                                lavaTracks.Add(track);
                                            }
                                        }
                                    }

                                    player.Vueue.Enqueue(lavaTracks);

                                    msg = "The remaining tracks are loaded in the queue.";
                                    embed = BuildMusicEmbed("Playlist Loaded", msg);
                                    await ReplyAsync(embed: embed.Build());
                                }
                                // It's not a playlist
                                else
                                {
                                    var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[0]);
                                    if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                    {
                                        var track = node.Tracks.FirstOrDefault();
                                        AddMusicTable(track, serverId, Context.Message.Author.Username);
                                        player.Vueue.Enqueue(track);

                                        msg = $"One {track?.Title} coming right up!\n {track?.Url}\n {track?.Duration}\n {track?.Source.ToUpper()}";

                                        var embed = BuildMusicEmbed("Track Loaded", msg);
                                        await ReplyAsync(embed: embed.Build());

                                        if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                        {
                                            var queueEmbed = BuildMusicEmbed("Queue", "Item was added to the queue because something is currently playing.");
                                            await ReplyAsync(embed: queueEmbed.Build());
                                            return;
                                        }
                                        player.Vueue.TryDequeue(out var lavaTrack);
                                        await player.PlayAsync(lavaTrack);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var embed = BuildMusicEmbed("Spotify", ex.Message);
                        await ReplyAsync(embed: embed.Build());
                    }
                }
                else
                {
                    var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                    if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                    {
                        string msg = $"I wasn't able to find anything for '{searchQuery}'.";
                        var embed = BuildMusicEmbed("Play", msg);
                        await ReplyAsync(embed: embed.Build());

                        return;
                    }


                    if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                    {
                        player.Vueue.Enqueue(searchResponse.Tracks);

                        string msg = $"Queued up {searchResponse.Tracks.Count} songs!";
                        var embed = BuildMusicEmbed("Playlist Loaded", msg);
                        await ReplyAsync(embed: embed.Build());
                    }
                    else
                    {
                        LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                        player.Vueue.Enqueue(track);

                        string msg = $"**{track?.Title}** coming right up!\n {track?.Url}\n {track?.Duration}\n {track?.Source.ToUpper()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))} out of 150**";

                        var embed = BuildMusicEmbed("Track Loaded", msg);
                        await ReplyAsync(embed: embed.Build());

                    }
                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                    {
                        var embed = BuildMusicEmbed("Queue", "Item was added to the queue because something is currently playing.");
                        await ReplyAsync(embed: embed.Build());
                        return;
                    }
                    player.Vueue.TryDequeue(out var lavaTrack);
                    await player.PlayAsync(lavaTrack);
                }
            }
        }
        public EmbedBuilder BuildMusicEmbed(string title, string description)
        {
            var embed = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - {title}",
                Color = Color.Blue,
                Description = $"{description}",
                ThumbnailUrl = ""//"https://toppng.com/uploads/preview/clip-art-free-music-ministry-transparent-background-music-notes-11562855021eg6xmxzw2u.png",
            };

            embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();

            return embed;
        }

        public void AddMusicTable(LavaTrack lavaTrack, string serverId, string createdBy)
        {
            if (lavaTrack == null)
                return;

            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddMusic", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(serverId.ToString())),
                new SqlParameter("@VideoID", lavaTrack.Id),
                new SqlParameter("@Author", lavaTrack.Author),
                new SqlParameter("@Title", lavaTrack.Title),
                new SqlParameter("@URL", lavaTrack.Url),
                new SqlParameter("@CreatedBy", createdBy)
            });
        }
        public int GetVolume(long guildId)
        {
            StoredProcedure procedure = new StoredProcedure();
            int volume = 100;

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetVolume", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", guildId)
            });

            foreach (DataRow dr in dt.Rows) 
            {
                volume = int.Parse(dr["Volume"].ToString());
            }

            return volume;
        }
    }
}
