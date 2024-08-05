using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Services;
using Fergun.Interactive;
using System.Data.SqlClient;
using System.Data;
using System.Text;
using Victoria.Node;
using Victoria.Player;
using Victoria.Responses.Search;
using Victoria;
using Fergun.Interactive.Pagination;
using System.Globalization;

namespace DiscordBot.SlashCommands
{
    public class Audio : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly AudioService _audioService;
        private readonly SpotifyHelper _spotifyHelper;
        private readonly InteractiveService _interactive;

        private static readonly IEnumerable<int> Range = Enumerable.Range(1900, 2000);
        private static Dictionary<string, double[]> EqBands = new()
        {
            { "superbass", new[] { 1, 1, 1, 1, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, 1, 1, 1, 1 } },
            { "bass", new[] { 0.10, 0.10, 0.05, 0.05, 0.05, -0.05, -0.05, 0, -0.05, -0.05, 0, 0.05, 0.05, 0.10, 0.10 }},
            { "pop", new[] { -0.01, -0.01, 0, 0.01, 0.02, 0.05, 0.07, 0.10, 0.07, 0.05, 0.02, 0.01, 0, -0.01, -0.01 }},
            { "off", null }
        };

        public Audio(LavaNode lavaNode, AudioService audioService, SpotifyHelper spotifyHelper, InteractiveService interactive) 
        {
            _lavaNode = lavaNode;
            _audioService = audioService;
            _spotifyHelper = spotifyHelper;
            _interactive = interactive;
        }

        [SlashCommand("join", "Bot joins the voice channel.")]
        [EnabledInDm(false)]
        public async Task JoinAsync()
        {
            await DeferAsync();
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                var embed = BuildMusicEmbed("Join", "You must be connected to a voice channel");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (_lavaNode.HasPlayer(Context.Guild))
            {
                var embed = BuildMusicEmbed("Join", "I'm already connected to a voice channel!");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                var player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);

                StoredProcedure stored = new StoredProcedure();
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPlayerConnected", new List<SqlParameter>
                {
                    new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())),
                    new SqlParameter("VoiceChannelID", Int64.Parse(voiceState.VoiceChannel.Id.ToString())),
                    new SqlParameter("TextChannelID", Int64.Parse((Context.Channel as ITextChannel).Id.ToString())),
                    new SqlParameter("@CreatedBy", Context.User.Id.ToString())
                });

                //await FollowupAsync($"Joined {voiceState.VoiceChannel.Name}!");
                var embed = BuildMusicEmbed("Join", $"Thank you for having me!\nThe current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**!");
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("play", "Play a Youtube, Spotify, or Soundcloud track/playlist in the bot.")]
        [EnabledInDm(false)]
        public async Task PlayAsync(string searchQuery)
        {
            await DeferAsync();
            if (searchQuery.Contains("https://twitter.com"))
                searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
            if (searchQuery.Contains("https://x.com"))
                searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");
            
            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    string msg = Context.Interaction.User.Mention + ", you must be connected to a voice channel!";
                    var embed = BuildMusicEmbed("Play", msg);
                    await FollowupAsync(embed: embed.Build());
                    return;
                }

                try
                {
                    player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);

                    StoredProcedure stored = new StoredProcedure();
                    stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPlayerConnected", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())),
                        new SqlParameter("VoiceChannelID", Int64.Parse(voiceState.VoiceChannel.Id.ToString())),
                        new SqlParameter("TextChannelID", Int64.Parse((Context.Channel as ITextChannel).Id.ToString())),
                        new SqlParameter("@CreatedBy", Context.User.Id.ToString())
                    });

                    //await FollowupAsync($"Joined {voiceState.VoiceChannel.Name}!");
                    var embed = BuildMusicEmbed("Play", $"Thank you for having me!\nThe current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**!");
                    await FollowupAsync(embed: embed.Build());
                }
                catch (Exception exception)
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await FollowupAsync(embed: embed.Build());
                }
            }

            if (_spotifyHelper.IsSpotifyUrl(searchQuery).Result)
                HandleSpotify(searchQuery, player);
            else
            {
                if (searchQuery.Contains("https://twitter.com"))
                    searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
                if (searchQuery.Contains("https://x.com"))
                    searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");

                var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                {
                    string msg = $"I wasn't able to find anything for '{searchQuery}'.";
                    var embed = BuildMusicEmbed("Play", msg);
                    await FollowupAsync(embed: embed.Build());

                    return;
                }

                var serverId = player.VoiceChannel.GuildId.ToString();
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    foreach (var track in searchResponse.Tracks)
                        AddMusicTable(track, serverId, Context.User.Id.ToString());

                    player.Vueue.Enqueue(searchResponse.Tracks);

                    string msg = $"Queued up {searchResponse.Tracks.Count} songs!";
                    var embed = BuildMusicEmbed("Playlist Loaded", msg);
                    await FollowupAsync(embed: embed.Build());
                }
                else
                {
                    LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                    var artwork = await track.FetchArtworkAsync();
                    if (string.IsNullOrEmpty(artwork))
                        artwork = "";
                        
                    AddMusicTable(track, serverId, Context.User.Id.ToString());
                    player.Vueue.Enqueue(track);

                    string msg = $"Track Name: **{track?.Title}**\nURL: {track?.Url}\nDuration: **{track?.Duration}**\nSource: **{track?.Source.ToUpper()}**\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";

                    var embed = BuildMusicEmbed("Track Loaded", msg, artwork);
                    await FollowupAsync(embed: embed.Build());

                }
                if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                {
                    var embed = BuildMusicEmbed("Queue", "**Item added to the queue because something was playing.**");
                    await FollowupAsync(embed: embed.Build());
                    return;
                }
                player.Vueue.TryDequeue(out var lavaTrack);
                await player.SetVolumeAsync(0);
                await player.PlayAsync(lavaTrack);
            }
        }

        [SlashCommand("leave", "Leaves the voice channel and clears the queue.")]
        [EnabledInDm(false)]
        public async Task LeaveAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Leave", "I'm not connected to a voice channel!");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null)
            {
                var embed = BuildMusicEmbed("Leave", "Not sure which voice channel to disconnect from.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                await _lavaNode.LeaveAsync(voiceChannel);
                DeletePlayerConnected(Int64.Parse(Context.Guild.Id.ToString()));
                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Leave",
                    Color = Color.Blue,
                    Description = $"Bye, have a beautiful time",
                    ThumbnailUrl = "https://static.wikia.nocookie.net/americandad/images/d/d0/Officer_Pena.jpg/revision/latest?cb=20100228182532",
                };

                embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                        .WithCurrentTimestamp();
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("pause", "Pause the current audio playing.")]
        [EnabledInDm(false)]
        public async Task PauseAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Pause", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Pause", "I cannot pause when I'm not playing anything!");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.PauseAsync();

                string msg = $"Paused: **{player.Track.Title}**";
                var embed = BuildMusicEmbed("Pause", msg);
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("resume", "Resumes the current audio playing")]
        [EnabledInDm(false)]
        public async Task ResumeAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Resume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Paused)
            {
                var embed = BuildMusicEmbed("Resume", "I cannot resume when I'm not playing anything!");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.ResumeAsync();
                string msg = $"Resumed: {player.Track.Title}";
                var embed = BuildMusicEmbed("Resume", msg);
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("stop", "Stops the audio, clears the queue, and leaves the voice channel.")]
        [EnabledInDm(false)]
        public async Task StopAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Stop", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState == PlayerState.Stopped)
            {
                var embed = BuildMusicEmbed("Stop", "**There was nothing to stop!**");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                await player.StopAsync();

                var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;
                if (voiceChannel != null)
                {
                    await _lavaNode.LeaveAsync(voiceChannel);
                    DeletePlayerConnected(Int64.Parse(Context.Guild.Id.ToString()));
                    var embed = new EmbedBuilder
                    {
                        Title = $"BigBirdBot Music - Leave",
                        Color = Color.Blue,
                        Description = $"Bye, have a beautiful time",
                        ThumbnailUrl = "https://static.wikia.nocookie.net/americandad/images/d/d0/Officer_Pena.jpg/revision/latest?cb=20100228182532",
                    };

                    embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                            .WithCurrentTimestamp();
                    await FollowupAsync(embed: embed.Build());
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("forceskip", "Skips the current track.")]
        [EnabledInDm(false)]
        public async Task ForceSkipTaskAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Skip", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Skip", "Woaaah there, I can't skip when nothing is playing!");
                await FollowupAsync(embed: embed.Build());
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
                    await FollowupAsync(embed: embed.Build());
                }
                else
                {
                    await player.StopAsync();
                    string msg = $"The last item in the queue was skipped and there is nothing playing.";
                    var embed = BuildMusicEmbed("Skip", msg);
                    await FollowupAsync(embed: embed.Build());
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("seek", "Goes to a specific time of the current track.")]
        [EnabledInDm(false)]
        public async Task SeekAsync(string timeSpan)
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Seek", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Seek", "Woaaah there, I can't seek when nothing is playing.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                TimeSpan time;
                if (TimeSpan.TryParse(timeSpan, out time))
                {
                    await player.SeekAsync(time);
                    string msg = $"I've seeked `{player.Track.Title}` to {timeSpan}.";
                    var embed = BuildMusicEmbed("Seek", msg);
                    await FollowupAsync(embed: embed.Build());
                }
                else
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid seek time.\n**Example: -seek 00:00:30**\nAbove example would seek 30 seconds into the video.", Constants.Constants.errorImageUrl, "", Color.Red, "").Build());
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("volume", "Set the volume between 0 and 100.")]
        [EnabledInDm(false)]
        public async Task VolumeAsync([MinValue(0), MaxValue(100)] int volume)
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Volume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                StoredProcedure procedure = new StoredProcedure();
                var guildId = Int64.Parse(player.VoiceChannel.GuildId.ToString());

                if (ushort.TryParse(volume.ToString(), out var vol))
                {
                    await player.SetVolumeAsync(vol);

                    procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateVolume", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerUID", guildId),
                        new SqlParameter("@Volume", volume)
                    });

                    string msg = $"I've changed the player volume to {volume.ToString()}.";
                    var embed = BuildMusicEmbed("Volume", msg);
                    await FollowupAsync(embed: embed.Build());
                }
                else
                {
                    var embed = BuildMusicEmbed("Volume", "Please enter a volume between 0 and 150.");
                    await FollowupAsync(embed: embed.Build());
                }
            }
            catch (Exception exception)
            {
                var embed = BuildMusicEmbed("Volume", exception.Message);
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("nowplaying", "View the current track.")]
        [EnabledInDm(false)]
        public async Task NowPlayingAsync()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Now Playing", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Now Playing", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build());
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

            await FollowupAsync(embed: npEmbed.Build());
        }

        [SlashCommand("queue", "View the list of tracks set to play.")]
        [EnabledInDm(false)]
        public async Task GetQueue()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Queue", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build());
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

                await FollowupAsync(embed: npEmbed.Build());
            }

            if (player.Vueue.Count > 0)
            {
                List<PageBuilder> pages = new List<PageBuilder>();
                string queue = "";
                int i = 0;
                foreach (var p in player.Vueue)
                {
                    i++;
                    queue += i.ToString() + ". **" + p.Title + "** - " + p.Duration + " \n " + p.Url + "\n\n";

                    if (i % 10 == 0)
                    {
                        pages.Add(new PageBuilder().WithTitle("BigBirdBot - Queue").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());
                        queue = "";
                    }
                }

                pages.Add(new PageBuilder().WithTitle("**BigBirdBot - Queue**").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());

                var paginator = new StaticPaginatorBuilder()
                    .AddUser(Context.User)
                    .WithPages(pages)
                    .Build();

                // Send the paginator to the source channel and wait until it times out after 15 minutes.
                await _interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(15));
            }

        }

        [SlashCommand("equalizer", "Set the audio EQ to one of the three settings: super bass, bass, or pop.")]
        [EnabledInDm(false)]
        public async Task GetEqualizer([Choice("Superbass", "superbass"), Choice("Bass", "bass"), Choice("Pop", "pop"), Choice("Off", "off")] string eq)
        {
            await DeferAsync();
            if (!_lavaNode.HasPlayer(Context.Guild))
                return;

            _lavaNode.TryGetPlayer(Context.Guild, out var player);

            if (eq == null)
            {
                string eqmsg = "";

                if (EQHelper.CurrentEQ == "Off")
                    eqmsg = "No EQ Applied.";
                else
                    eqmsg = $"Current EQ is: `{EQHelper.CurrentEQ}`";

                var embed = BuildMusicEmbed("Equalizer", eqmsg);
                await FollowupAsync(embed: embed.Build());

                return;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;

            if (!EqBands.ContainsKey(eq))
            {
                var sb = new StringBuilder();

                var keys = EqBands.Keys.ToList();

                for (var i = 0; i < keys.Count; i++)
                    keys[i] = $"`{keys[i]}`";

                var eqMsg = $"Valid EQ modes: {string.Join(", ", keys)}";
                var embed = BuildMusicEmbed("Equalizer", eqMsg);

                await FollowupAsync(embed: embed.Build());
            }

            var bands = EQHelper.BuildEQ(EqBands[eq]);

            EQHelper.CurrentEQ = textInfo.ToTitleCase(eq);
            await player.EqualizerAsync(bands);

            var msg = (EQHelper.CurrentEQ == "Off") ? "EQ turned off" : $"`{EQHelper.CurrentEQ}`: working my magic!";
            var resultEmbed = BuildMusicEmbed("Equalizer", msg);
            await FollowupAsync(embed: resultEmbed.Build());
        }

        [SlashCommand("repeat", "Repeats the current track.")]
        [EnabledInDm(false)]
        public async Task RepeatTrack()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                string msg = Context.User.Mention + ", you must be connected to a voice channel!";
                var errorEmbed = BuildMusicEmbed("Repeat", msg);
                await FollowupAsync(embed: errorEmbed.Build());
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
                await FollowupAsync(embed: embed.Build());

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

        [SlashCommand("loop", "Repeats the current track the number of times provided.")]
        [EnabledInDm(false)]
        public async Task LoopTrack([MinValue(1)] int times)
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                string msg = Context.User.Mention + ", you must be connected to a voice channel!";
                var errorEmbed = BuildMusicEmbed("Loop", msg);
                await FollowupAsync(embed: errorEmbed.Build());
                return;
            }

            var track = player.Track;
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel ?? player.VoiceChannel;

            if (voiceChannel != null)
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
                await FollowupAsync(embed: embed.Build());
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
                    Description = $"The bot must be connected to your voice channel to run this command.",
                    ThumbnailUrl = Constants.Constants.errorImageUrl,
                };

                embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("swap", "Switch two tracks in the queue.")]
        [EnabledInDm(false)]
        public async Task SwapTrack(int oldPosition, int newPosition)
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            EmbedHelper embedHelper = new EmbedHelper();

            oldPosition--;
            newPosition--;
            if (player.Vueue.ElementAt(oldPosition) != null && player.Vueue.ElementAt(newPosition) != null)
            {
                var itemList = player.Vueue.ToList();
                var val = itemList[oldPosition];
                itemList[oldPosition] = itemList[newPosition];
                itemList[newPosition] = val;

                player.Vueue.Clear();
                player.Vueue.Enqueue(itemList);

                var embed = BuildMusicEmbed("Swap", $"Successfully swapped **{itemList[oldPosition].Title}** and **{itemList[newPosition].Title}** in the queue.");
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "Both elements must be present in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("shuffle", "Randomizes the queue.")]
        [EnabledInDm(false)]
        public async Task ShuffleVueue()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.Vueue.Count > 1)
            {
                player.Vueue.Shuffle();

                var embed = BuildMusicEmbed("Shuffle", "Queue Shuffled");
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have more than one element in it to shuffle.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("clear", "Removes everything in the queue.")]
        [EnabledInDm(false)]
        public async Task ClearQueue()
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            if (player.Vueue.Count > 0)
            {
                player.Vueue.Clear();

                var embed = BuildMusicEmbed("Clear", "Queue is now empty");
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have one element to clear.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("remove", "Deletes a track from the queue.")]
        [EnabledInDm(false)]
        public async Task RemoveItem(int element)
        {
            await DeferAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build());
                return;
            }

            try
            {
                element--;
                var item = player.Vueue.RemoveAt(element);

                var embed = BuildMusicEmbed("Remove", $"Removed {item.Title} from the queue");
                await FollowupAsync(embed: embed.Build());
            }
            catch
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The element does not exist in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("playnext", "Sets this track to be the next in the queue.")]
        [EnabledInDm(false)]
        public async Task PlayNext(string searchQuery)
        {
            await DeferAsync();

            if (!string.IsNullOrWhiteSpace(searchQuery) && searchQuery.Contains("https://twitter.com"))
                searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
            if (!string.IsNullOrWhiteSpace(searchQuery) && searchQuery.Contains("https://x.com"))
                searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");
            
            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    string msg = Context.User.Mention + ", you must be connected to a voice channel!";
                    var embed = BuildMusicEmbed("Playnext", msg);
                    await FollowupAsync(embed: embed.Build());
                    return;
                }

                try
                {
                    player = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);

                    StoredProcedure stored = new StoredProcedure();
                    stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPlayerConnected", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())),
                        new SqlParameter("VoiceChannelID", Int64.Parse(voiceState.VoiceChannel.Id.ToString())),
                        new SqlParameter("TextChannelID", Int64.Parse((Context.Channel as ITextChannel).Id.ToString())),
                        new SqlParameter("@CreatedBy", Context.User.Id.ToString())
                    });
                    //await FollowupAsync($"Joined {voiceState.VoiceChannel.Name}!");
                    var embed = BuildMusicEmbed("Play", $"Thank you for having me, as a heads up the current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**!");
                    await FollowupAsync(embed: embed.Build());
                }
                catch (Exception exception)
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await FollowupAsync(embed: embed.Build());
                }
            }

            var serverId = player.VoiceChannel.GuildId.ToString();

            if (player.Vueue.Count > 0)
            {
                if (_spotifyHelper.IsSpotifyUrl(searchQuery).Result)
                    HandleSpotify(searchQuery, player);
                else
                {
                    if (searchQuery.Contains("https://twitter.com"))
                        searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
                    if (searchQuery.Contains("https://x.com"))
                        searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");

                    var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                    if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                    {
                        string msgMusic = $"I wasn't able to find anything for '{searchQuery}'.";
                        var embedMusic = BuildMusicEmbed("Playnext", msgMusic);
                        await FollowupAsync(embed: embedMusic.Build());

                        return;
                    }

                    LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                    var artwork = await track.FetchArtworkAsync();
                    if (string.IsNullOrEmpty(artwork))
                        artwork = "";

                    AddMusicTable(track, serverId, Context.User.Id.ToString());
                    var itemList = player.Vueue.ToList();
                    itemList.Insert(0, track);

                    player.Vueue.Clear();
                    player.Vueue.Enqueue(itemList);

                    string msg = $"Track Name: **{track?.Title}**\nURL:  {track?.Url} \nDuration:  {track?.Duration} \nSource: {track?.Source.ToUpper()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";

                    var embed = BuildMusicEmbed("Track Loaded", msg, artwork);
                    await FollowupAsync(embed: embed.Build());

                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                    {
                        var embedMusic = BuildMusicEmbed("Queue", "**Item added to the top of the queue because something was playing.**");
                        await FollowupAsync(embed: embedMusic.Build());
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
                    HandleSpotify(searchQuery, player);
                else
                {
                    if (searchQuery.Contains("https://twitter.com"))
                        searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
                    if (searchQuery.Contains("https://x.com"))
                        searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");

                    var searchResponse = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, searchQuery);
                    if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                    {
                        string msg = $"I wasn't able to find anything for '{searchQuery}'.";
                        var embed = BuildMusicEmbed("Play", msg);
                        await FollowupAsync(embed: embed.Build());

                        return;
                    }


                    if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                    {
                        player.Vueue.Enqueue(searchResponse.Tracks);

                        string msg = $"Queued up {searchResponse.Tracks.Count} songs!";
                        var embed = BuildMusicEmbed("Playlist Loaded", msg);
                        await FollowupAsync(embed: embed.Build());
                    }
                    else
                    {
                        LavaTrack? track = searchResponse.Tracks.FirstOrDefault();
                        var artwork = await track.FetchArtworkAsync();
                        if (string.IsNullOrEmpty(artwork))
                            artwork = "";

                        player.Vueue.Enqueue(track);

                        string msg = $"Track Name: **{track?.Title}**\n {track?.Url}\n {track?.Duration}\n {track?.Source.ToUpper()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";

                        var embed = BuildMusicEmbed("Track Loaded", msg, artwork);
                        await FollowupAsync(embed: embed.Build());

                    }
                    if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                    {
                        var embed = BuildMusicEmbed("Queue", "**Item was added to the queue because something was playing.**");
                        await FollowupAsync(embed: embed.Build());
                        return;
                    }
                    player.Vueue.TryDequeue(out var lavaTrack);
                    await player.PlayAsync(lavaTrack);
                }
            }
        }

        public EmbedBuilder BuildMusicEmbed(string title, string description, string artwork = "")
        {
            var embed = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - {title}",
                Color = Color.Blue,
                Description = $"{description}",
                ImageUrl = artwork
            };

            embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();

            return embed;
        }

        #region Helpers
        public void AddMusicTable(LavaTrack lavaTrack, string serverId, string createdBy)
        {
            if (lavaTrack != null)
            {
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
        }
        public int GetVolume(long guildId)
        {
            StoredProcedure procedure = new StoredProcedure();
            int volume = 50;

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetVolume", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", guildId)
            });

            foreach (DataRow dr in dt.Rows)
                volume = int.Parse(dr["Volume"].ToString());

            return volume;
        }

        public async void HandleSpotify(string searchQuery, LavaPlayer<LavaTrack?> player)
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
                                        AddMusicTable(track, serverId, Context.User.Id.ToString());
                                        lavaTracks.Add(track);
                                    }
                                }
                            }

                            player.Vueue.Enqueue(lavaTracks);

                            msg = "**Loaded the first five Spotify tracks successfully, queueing the rest of the playlist.**";

                            var embed = BuildMusicEmbed("Playlist Loaded", msg);
                            await FollowupAsync(embed: embed.Build());

                            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                            {
                                // Load the rest while the first 5 play
                                for (int i = 5; i < tracks.Count; i++)
                                {
                                    var node = await _lavaNode.SearchAsync(SearchType.YouTube, tracks[i]);
                                    if (node.Status != SearchStatus.NoMatches || node.Status != SearchStatus.LoadFailed)
                                    {
                                        var track = node.Tracks.FirstOrDefault();
                                        if (track != null)
                                        {
                                            AddMusicTable(track, serverId, Context.User.Id.ToString());
                                            lavaTracks.Add(track);
                                        }
                                    }
                                }

                                player.Vueue.Enqueue(lavaTracks);
                            }
                            else
                            {
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
                                            AddMusicTable(track, serverId, Context.User.Id.ToString());
                                            lavaTracks.Add(track);
                                        }
                                    }
                                }

                                player.Vueue.Enqueue(lavaTracks);
                            }

                            msg = "**The remaining tracks were loaded in queue.**";
                            embed = BuildMusicEmbed("Playlist Loaded", msg);
                            await FollowupAsync(embed: embed.Build());
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

                                msg = $"Track Name: **{track?.Title}**\nURL: {track?.Url}\n Duration: {track?.Duration}\n Source: {track?.Source.ToUpper()}";

                                var embed = BuildMusicEmbed("Track Loaded", msg);
                                await FollowupAsync(embed: embed.Build());

                                if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
                                {
                                    var queueEmbed = BuildMusicEmbed("Queue", "Item added to the queue because something was playing.");
                                    await FollowupAsync(embed: queueEmbed.Build());
                                    return;
                                }
                                player.Vueue.TryDequeue(out var lavaTrack);
                                AddMusicTable(track, serverId, Context.User.Id.ToString());
                                await player.PlayAsync(lavaTrack);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var embed = BuildMusicEmbed("Spotify", ex.Message);
                await FollowupAsync(embed: embed.Build());
            }
        }

        public void DeletePlayerConnected(long serverId)
        {
            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(serverId.ToString()))
            });
        }
        #endregion
    }
}
