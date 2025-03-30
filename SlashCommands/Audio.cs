using System.Data.SqlClient;
using System.Data;
using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using Fergun.Interactive;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using DiscordBot.Helper;
using Fergun.Interactive.Pagination;
using Lavalink4NET.Tracks;

namespace DiscordBot.SlashCommands
{
    public sealed class Audio : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IAudioService _audioService;
        private readonly InteractiveService _interactiveService;

        public Audio(IAudioService audioService, InteractiveService interactiveService)
        {
            ArgumentNullException.ThrowIfNull(audioService);
            _audioService = audioService;
            _interactiveService = interactiveService;
        }

        [SlashCommand("join", "Bot joins the voice channel.", runMode: RunMode.Async)]
        public async Task JoinAsync()
        {
            await DeferAsync();
            var voiceState = Context.User as IVoiceState;

            if (voiceState?.VoiceChannel == null)
            {
                var error = BuildMusicEmbed("Join", "You must be connected to a voice channel");
                await FollowupAsync(embed: error.Build());
                return;
            }

            await _audioService.StartAsync().ConfigureAwait(false);
            await Task.Delay(3000);

            AddPlayerConnected(voiceState);

            var player = await GetPlayerAsync(connectToVoiceChannel: true);

            if (player == null || player.ConnectionState.IsConnected)
            {
                var error = BuildMusicEmbed("Join", "I'm already connected to a voice channel!");
                await FollowupAsync(embed: error.Build());
                return;
            }

            var embed = BuildMusicEmbed("Join", $"Thank you for having me!\nThe current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**!");
            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("leave", "Bot leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            await DeferAsync();
            var isConnected = await _audioService.Players.GetPlayerAsync(Context.Guild as IGuild);

            if (isConnected != null)
                await isConnected.DisconnectAsync().ConfigureAwait(false);
            else
            {
                var player = await GetPlayerAsync().ConfigureAwait(false);

                if (player == null)
                    await FollowupAsync("No Player");
            }

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
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        [SlashCommand("play", "Play a Youtube, Spotify, Twitter, Twitch, or Soundcloud track/playlists in the bot.", runMode: RunMode.Async)]
        public async Task PlayAsync(string searchQuery)
        {
            await DeferAsync();

            searchQuery = HandleTwitter(searchQuery);

            var isConnected = await _audioService.Players.RetrieveAsync(Context).ConfigureAwait(false);
            LavalinkPlayer? player = isConnected.Player;

            if (player == null)
            {
                // Have to join then query
                var voiceState = Context.User as IVoiceState;

                if (voiceState?.VoiceChannel == null)
                {
                    var error = BuildMusicEmbed("Play", "You must be connected to a voice channel");
                    await FollowupAsync(embed: error.Build());
                    return;
                }

                await _audioService.StartAsync().ConfigureAwait(false);
                await Task.Delay(3000);

                AddPlayerConnected(voiceState);
                player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
            }

            // Now let's queue some tracks
            var tracks = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.YouTube);

            if (tracks.IsFailed)
            {
                string empty = $"I wasn't able to find anything for '{searchQuery}'.";
                var noresults = BuildMusicEmbed("Play", empty);
                await FollowupAsync(embed: noresults.Build());
                return;
            }

            var track = tracks.Track;

            // This could be a playlist so we handle it differently
            if (Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute))
            {
                if (tracks.Count == 1)
                    PlaySingleTrack(player, track);
                else
                    PlayMultipleTracks(player, tracks);
            }
            // If it's a direct search, we're not loading a playlist so only get the first track
            else
                PlaySingleTrack(player, track);
        }

        [SlashCommand("forceskip", "Skips the current track.")]
        public async Task ForceSkipTaskAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Skip", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                var embed = BuildMusicEmbed("Skip", "Woaaah there, I can't skip when nothing is playing!");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.SkipAsync().ConfigureAwait(false);
            var track = player.CurrentItem;

            if (track is not null)
            {
                string msg = $"Now Playing: **{track.Track.Title}**";
                var embed = BuildMusicEmbed("Skip", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                string msg = $"The last item in the queue was skipped and there is nothing playing.";
                var embed = BuildMusicEmbed("Skip", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("resume", "Resumes the current audio playing")]
        public async Task ResumeAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Resume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is not PlayerState.Paused)
            {
                var embed = BuildMusicEmbed("Resume", "Can't resume something that is currently playing.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.ResumeAsync();
            string msg = $"Resumed: {player.CurrentTrack.Title}";
            var result = BuildMusicEmbed("Resume", msg);
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("pause", "Pause the current audio playing.")]
        public async Task PauseAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Pause", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is PlayerState.Paused)
            {
                var embed = BuildMusicEmbed("Pause", "The current track is already paused.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.PauseAsync();
            string msg = $"Paused: {player.CurrentTrack.Title}";
            var result = BuildMusicEmbed("Paused", msg);
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("stop", "Stops the audio, clears the queue, and leaves the voice channel.")]
        public async Task StopAsync()
        {
            await DeferAsync();
            var isConnected = await _audioService.Players.GetPlayerAsync(Context.Guild as IGuild).ConfigureAwait(false);

            if (isConnected != null)
            {
                await isConnected.StopAsync().ConfigureAwait(false);
                await isConnected.DisconnectAsync().ConfigureAwait(false);
            }
            else
            {
                var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

                if (player is null)
                {
                    var embed = BuildMusicEmbed("Stop", "I'm not connected to a voice channel.");
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                if (player.CurrentItem == null)
                {
                    var embed = BuildMusicEmbed("Stop", "There is nothing playing.");
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                await player.StopAsync().ConfigureAwait(false);
                await player.DisconnectAsync().ConfigureAwait(false);
            }

            DeletePlayerConnected(Int64.Parse(Context.Guild.Id.ToString()));
            var result = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - Leave",
                Color = Color.Blue,
                Description = $"Bye, have a beautiful time",
                ThumbnailUrl = "https://static.wikia.nocookie.net/americandad/images/d/d0/Officer_Pena.jpg/revision/latest?cb=20100228182532",
            };

            result.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("volume", "Set the volume between 0 and 100.")]
        public async Task VolumeAsync([MinValue(0), MaxValue(100)] int volume)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Volume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            StoredProcedure procedure = new StoredProcedure();
            var guildId = Int64.Parse(player.GuildId.ToString());

            if (ushort.TryParse(volume.ToString(), out var vol))
            {
                await player.SetVolumeAsync(vol / 100f).ConfigureAwait(false);

                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateVolume", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerUID", guildId),
                        new SqlParameter("@Volume", volume)
                    });

                string msg = $"I've changed the player volume to {volume.ToString()}.";
                var embed = BuildMusicEmbed("Volume", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                var embed = BuildMusicEmbed("Volume", "Please enter a volume between 0 and 150.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("nowplaying", "View the current track.")]
        public async Task NowPlayingAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Now Playing", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is not PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Now Playing", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var track = player.CurrentTrack;
            string artworkUrl = "";

            if (track.ArtworkUri != null)
                artworkUrl = track.ArtworkUri.ToString();

            var artwork = artworkUrl;

            var npEmbed = new EmbedBuilder()
            {
                Title = $"BigBirdBot Music - Now Playing: **{track.Title}**",
                Color = Color.Blue,
            };

            npEmbed.WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Uri.ToString())
            .WithImageUrl(artwork.ToString());

            await FollowupAsync(embed: npEmbed.Build()).ConfigureAwait(false);
        }

        [SlashCommand("queue", "View the list of tracks set to play.")]
        public async Task GetQueue()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Queue", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count == 0)
            {
                var track = player.CurrentTrack;

                string artworkUrl = "";

                if (track.ArtworkUri != null)
                    artworkUrl = track.ArtworkUri.ToString();

                var artwork = artworkUrl;

                var npEmbed = new EmbedBuilder()
                {
                    Title = $"BigBirdBot Music - Queue: **{track.Title}**",
                    Color = Color.Blue,
                };

                npEmbed.WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Uri.ToString())
                .WithImageUrl(artwork);

                await FollowupAsync(embed: npEmbed.Build()).ConfigureAwait(false);
            }

            if (player.Queue.Count > 0)
            {
                List<PageBuilder> pages = new List<PageBuilder>();
                string queue = "";
                int i = 0;
                foreach (var p in player.Queue)
                {
                    i++;
                    var duration = new TimeSpan(p.Track.Duration.Hours, p.Track.Duration.Minutes, p.Track.Duration.Seconds);
                    queue += i.ToString() + ". **" + p.Track.Title + "** - " + duration + " \n " + p.Track.Uri + "\n\n";

                    if (i % 10 == 0)
                    {
                        pages.Add(new PageBuilder().WithTitle($"**BigBirdBot - Queue ({player.Queue.Count} total items)**").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());
                        queue = "";
                    }
                }

                pages.Add(new PageBuilder().WithTitle($"**BigBirdBot - Queue ({player.Queue.Count} total items)**").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());

                var paginator = new StaticPaginatorBuilder()
                    .AddUser(Context.User)
                    .WithPages(pages)
                    .Build();

                // Send the paginator to the source channel and wait until it times out after 15 minutes.
                await _interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(15)).ConfigureAwait(false);
            }
        }

        [SlashCommand("loop", "Repeats the current track the number of times provided.")]
        public async Task LoopTrack([MinValue(1)] int times)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Loop", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                var embed = BuildMusicEmbed("Loop", "There is no track available to loop.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var track = player.CurrentTrack;

            for (int i = 0; i < times; i++)
                await player.PlayAsync(track);

            var result = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - Loop",
                Color = Color.Blue,
                Description = $"Looping {track.Title} {times.ToString()} times.",
                ThumbnailUrl = "",
            };

            result.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                .WithCurrentTimestamp();
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("repeat", "Repeats the current track.")]
        public async Task RepeatTrack()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Repeat", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                var embed = BuildMusicEmbed("Repeat", "There is no track available to repeat.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var track = player.CurrentTrack;
            await player.PlayAsync(track).ConfigureAwait(false);

            var result = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - Repeat",
                Color = Color.Blue,
                Description = $"Repeating {track.Title}",
                ThumbnailUrl = "",
            };

            result.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                .WithCurrentTimestamp();
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("swap", "Switch two tracks in the queue.")]
        public async Task SwapTrack(int oldPosition, int newPosition)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Swap", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            EmbedHelper embedHelper = new EmbedHelper();

            if (player.Queue.ElementAt(oldPosition) != null && player.Queue.ElementAt(newPosition) != null)
            {
                var itemList = player.Queue.ToList();
                var val = itemList[oldPosition];
                itemList[oldPosition] = itemList[newPosition];
                itemList[newPosition] = val;

                await player.Queue.ClearAsync();

                foreach (var i in itemList)
                    await player.Queue.AddAsync(i);

                var embed = BuildMusicEmbed("Swap", $"Successfully swapped **{itemList[oldPosition].Track.Title}** and **{itemList[newPosition].Track.Title}** in the queue.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Swap Error", "Both elements must be present in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("shuffle", "Randomizes the queue.")]
        public async Task ShuffleVueue()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Shuffle", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count > 1)
            {
                await player.Queue.ShuffleAsync();
                var embed = BuildMusicEmbed("Shuffle", "Queue Shuffled");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have more than one element in it to shuffle.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("clear", "Removes everything in the queue.")]
        public async Task ClearQueue()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Clear", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count > 1)
            {
                await player.Queue.ClearAsync();
                var embed = BuildMusicEmbed("Clear", "Queue is now empty");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The queue must have one element to clear.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("remove", "Deletes a track from the queue.")]
        public async Task RemoveItem(int element)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Remove", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                element--;
                var track = player.Queue.ElementAt(element);
                var item = player.Queue.RemoveAtAsync(element);

                var embed = BuildMusicEmbed("Remove", $"Removed **{track.Track.Title}** from the queue");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            catch
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The element does not exist in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("seek", "Goes to a specific time of the current track.")]
        public async Task SeekAsync(string timeSpan)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                var embed = BuildMusicEmbed("Remove", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State != PlayerState.Playing)
            {
                var embed = BuildMusicEmbed("Seek", "Woaaah there, I can't seek when nothing is playing.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                TimeSpan time;
                if (TimeSpan.TryParse(timeSpan, out time))
                {
                    await player.SeekAsync(time);
                    string msg = $"I've seeked `{player.CurrentTrack.Title}` to {timeSpan}.";
                    var embed = BuildMusicEmbed("Seek", msg);
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                }
                else
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid seek time.\n**Example: -seek 00:00:30**\nAbove example would seek 30 seconds into the video.", Constants.Constants.errorImageUrl, "", Color.Red, "").Build()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        #region Helpers
        // Get Available Player or Join to a Voice Channel
        private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var options = new CustomPlayerOptions();
            options.SelfMute = true;
            options.TextChannel = (Context.Channel as ITextChannel);
            var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

            var result = await _audioService.Players
                .RetrieveAsync<CustomPlayer, CustomPlayerOptions>(Context, CreatePlayerAsync, options, retrieveOptions)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                var errorMessage = result.Status switch
                {
                    PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                    PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                    _ => "Unknown error.",
                };

                await FollowupAsync(errorMessage).ConfigureAwait(false);
                return null;
            }

            return result.Player;
        }

        // 'BigBirdBot' embed for music commands
        private EmbedBuilder BuildMusicEmbed(string title, string description, string artwork = "")
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

        // Audit of track entries entered by a user
        private void AddMusicTable(LavalinkTrack? lavaTrack, string serverId, string createdBy)
        {
            if (lavaTrack != null)
            {
                StoredProcedure stored = new StoredProcedure();
                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddMusic", new List<SqlParameter>
                {
                    new SqlParameter("@ServerID", Int64.Parse(serverId.ToString())),
                    new SqlParameter("@VideoID", lavaTrack.Identifier),
                    new SqlParameter("@Author", lavaTrack.Author),
                    new SqlParameter("@Title", lavaTrack.Title),
                    new SqlParameter("@URL", (lavaTrack.Uri != null) ? lavaTrack.Uri.OriginalString : ""),
                    new SqlParameter("@CreatedBy", createdBy)
                });
            }
        }

        // Retrieve the volume set in the Database for the specific Guild
        private int GetVolume(long guildId)
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

        // Add Connected Player to the Database
        private void AddPlayerConnected(IVoiceState? voiceState)
        {
            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPlayerConnected", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())),
                new SqlParameter("VoiceChannelID", Int64.Parse(voiceState.VoiceChannel.Id.ToString())),
                new SqlParameter("TextChannelID", Int64.Parse((Context.Channel as ITextChannel).Id.ToString())),
                new SqlParameter("@CreatedBy", Context.User.Id.ToString())
            });
        }

        // Remove the Connected player from the Database
        private void DeletePlayerConnected(long serverId)
        {
            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePlayerConnected", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(serverId.ToString()))
            });
        }

        // For Single Tracks and Searches
        private async void PlaySingleTrack(LavalinkPlayer? player, LavalinkTrack? track)
        {
            AddMusicTable(track, Context.Guild.Id.ToString(), Context.User.Username);

            TimeSpan duration = new TimeSpan();
            System.Text.Json.JsonElement albumName = new System.Text.Json.JsonElement();
            System.Text.Json.JsonElement artist = new System.Text.Json.JsonElement();
            string msg = "";

            if (track?.Duration != null)
                duration = new TimeSpan(track.Duration.Hours, track.Duration.Minutes, track.Duration.Seconds);

            if (track.AdditionalInformation.Count > 0)
                foreach (var keyValue in track.AdditionalInformation)
                {
                    if (keyValue.Key.Equals("artistUrl"))
                        artist = keyValue.Value;

                    if (keyValue.Key.Equals("albumName"))
                        albumName = keyValue.Value;
                }

            // For some reason Artist and Album are hiding, need to figure out why....
            if (track.AdditionalInformation.Count > 0)
                msg = $"Track Name: **{track?.Title}**" +
                    $"\nArtist: {artist.ToString()}" +
                    $"\nAlbum: **{albumName.ToString()}**" +
                    $"\nURL: {track?.Uri}" +
                    $"\nDuration: **{duration}**" +
                    $"\nSource: **{track?.SourceName}**" +
                    $"\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";
            else
                msg = $"Track Name: **{track?.Title}**\nArtist: **{(string.IsNullOrEmpty(track.Author) ? "" : track.Author)}**\nURL: {track?.Uri}\nDuration: **{duration}**\nSource: **{track?.SourceName}**\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";

            await player.PlayAsync(track).ConfigureAwait(false);
            await player.SetVolumeAsync(GetVolume(long.Parse(Context.Guild.Id.ToString())) / 100f).ConfigureAwait(false);
            var embed = BuildMusicEmbed("Queued", msg);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        // For Playlists and Multiple Tracks
        private async void PlayMultipleTracks(LavalinkPlayer? player, TrackLoadResult tracks)
        {
            string msg = "";
            System.Text.Json.JsonElement count = new System.Text.Json.JsonElement();
            System.Text.Json.JsonElement artworkUrl = new System.Text.Json.JsonElement();
            System.Text.Json.JsonElement url = new System.Text.Json.JsonElement();

            if (tracks.Playlist != null)
            {
                var playlist = tracks.Playlist;

                foreach (var keyValue in playlist.AdditionalInformation)
                {
                    if (keyValue.Key.Equals("totalTracks"))
                        count = keyValue.Value;

                    if (keyValue.Key.Equals("artworkUrl"))
                        artworkUrl = keyValue.Value;

                    if (keyValue.Key.Equals("url"))
                        url = keyValue.Value;
                }

                msg = $"Playlist Name: **{playlist.Name}** with {count.ToString()} items added.\nURL: {url.ToString()}\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";
            }

            foreach (var t in tracks.Tracks)
            {
                await player.PlayAsync(t);
                AddMusicTable(t, Context.Guild.Id.ToString(), Context.User.Username);
            }
            await player.SetVolumeAsync(GetVolume(long.Parse(Context.Guild.Id.ToString())) / 100f).ConfigureAwait(false);
            var embed = BuildMusicEmbed("Queued", msg, artworkUrl.ToString());
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        // Handle Twitter Links
        private string HandleTwitter(string searchQuery)
        {
            if (searchQuery.Contains("https://twitter.com"))
                searchQuery = searchQuery.Replace("twitter", "dl.fxtwitter");
            if (searchQuery.Contains("https://x.com"))
                searchQuery = searchQuery.Replace("x.com", "dl.fxtwitter.com");

            return searchQuery;
        }

        // Custom Player
        private static ValueTask<CustomPlayer> CreatePlayerAsync(IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(properties);

            return ValueTask.FromResult(new CustomPlayer(properties));
        }
        #endregion
    }
}
