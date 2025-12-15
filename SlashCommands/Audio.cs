using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
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
            IVoiceState? voiceState = Context.User as IVoiceState;

            if (voiceState?.VoiceChannel == null)
            {
                EmbedBuilder error = BuildMusicEmbed("Join", "You must be connected to a voice channel");
                await FollowupAsync(embed: error.Build());
                return;
            }

            await _audioService.StartAsync().ConfigureAwait(false);
            await Task.Delay(3000);

            AddPlayerConnected(voiceState);

            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: true);

            if (player == null || player.ConnectionState.IsConnected)
            {
                EmbedBuilder error = BuildMusicEmbed("Join", "I'm already connected to a voice channel!");
                await FollowupAsync(embed: error.Build());
                return;
            }

            EmbedBuilder embed = BuildMusicEmbed("Join", $"Thank you for having me!\nThe current volume for the player is **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**!");
            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("leave", "Bot leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            await DeferAsync();
            ILavalinkPlayer? isConnected = await _audioService.Players.GetPlayerAsync(Context.Guild);

            if (isConnected != null)
                await isConnected.DisconnectAsync().ConfigureAwait(false);
            else
            {
                QueuedLavalinkPlayer? player = await GetPlayerAsync().ConfigureAwait(false);

                if (player == null)
                    await FollowupAsync("No Player");
            }

            DeletePlayerConnected(Int64.Parse(Context.Guild.Id.ToString()));

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"Music - Leave",
                Color = Color.Blue,
                Description = $"Bye, have a beautiful time",
                ThumbnailUrl = "https://static.wikia.nocookie.net/americandad/images/d/d0/Officer_Pena.jpg/revision/latest?cb=20100228182532",
            };

            embed.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                    .WithCurrentTimestamp();
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        [SlashCommand("play", "Play a Youtube, Spotify, Twitter, Twitch, or Soundcloud track/playlists in the bot.", runMode: RunMode.Async)]
        public async Task PlayAsync([MinLength(1)] string searchQuery)
        {
            await DeferAsync();

            searchQuery = HandleTwitter(searchQuery);

            PlayerResult<LavalinkPlayer> isConnected = await _audioService.Players.RetrieveAsync(Context).ConfigureAwait(false);
            LavalinkPlayer? player = isConnected.Player;

            if (player == null)
            {
                // Have to join then query
                IVoiceState? voiceState = Context.User as IVoiceState;

                if (voiceState?.VoiceChannel == null)
                {
                    EmbedBuilder error = BuildMusicEmbed("Play", "You must be connected to a voice channel");
                    await FollowupAsync(embed: error.Build());
                    return;
                }

                await _audioService.StartAsync().ConfigureAwait(false);
                await Task.Delay(3000);

                AddPlayerConnected(voiceState);
                player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
            }

            // Now let's queue some tracks
            TrackLoadResult tracks = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.YouTube);

            if (tracks.IsFailed)
            {
                string empty = $"I wasn't able to find anything for '{searchQuery}'.";
                EmbedBuilder noresults = BuildMusicEmbed("Play", empty);
                await FollowupAsync(embed: noresults.Build());
                return;
            }

            LavalinkTrack track = tracks.Track;

            // This could be a playlist so we handle it differently
            if (Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute))
            {
                if (tracks.Count == 1)
                    await PlaySingleTrackAsync(player, track);
                else
                    await PlayMultipleTracksAsync(player, tracks);
            }
            // If it's a direct search, we're not loading a playlist so only get the first track
            else
                await PlaySingleTrackAsync(player, track);
        }

        [SlashCommand("playnext", "Works the same as the play command except force the track to be next in queue.", runMode: RunMode.Async)]
        public async Task PlayNextAsync([MinLength(1)] string searchQuery)
        {
            await DeferAsync();

            searchQuery = HandleTwitter(searchQuery);

            PlayerResult<LavalinkPlayer> isConnected = await _audioService.Players.RetrieveAsync(Context).ConfigureAwait(false);
            LavalinkPlayer? player = isConnected.Player;

            if (player == null)
            {
                // Have to join then query
                IVoiceState? voiceState = Context.User as IVoiceState;

                if (voiceState?.VoiceChannel == null)
                {
                    EmbedBuilder error = BuildMusicEmbed("Play Next", "You must be connected to a voice channel");
                    await FollowupAsync(embed: error.Build());
                    return;
                }

                await _audioService.StartAsync().ConfigureAwait(false);
                await Task.Delay(3000);

                AddPlayerConnected(voiceState);
                player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
            }

            // Now let's queue some tracks
            TrackLoadResult tracks = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.YouTube);

            if (tracks.IsFailed)
            {
                string empty = $"I wasn't able to find anything for '{searchQuery}'.";
                EmbedBuilder noresults = BuildMusicEmbed("Play Next", empty);
                await FollowupAsync(embed: noresults.Build());
                return;
            }

            LavalinkTrack track = tracks.Track;

            // This could be a playlist so we handle it differently
            if (Uri.IsWellFormedUriString(searchQuery, UriKind.Absolute))
            {
                if (tracks.Count == 1)
                    await PlaySingleTrackAsync(player, track, true);
                else
                    await PlayMultipleTracksAsync(player, tracks, true);
            }
            // If it's a direct search, we're not loading a playlist so only get the first track
            else
                await PlaySingleTrackAsync(player, track, true);
        }

        [SlashCommand("forceskip", "Skips the current track.")]
        public async Task ForceSkipTaskAsync()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Skip", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Skip", "Woaaah there, I can't skip when nothing is playing!");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.SkipAsync().ConfigureAwait(false);
            ITrackQueueItem? track = player.CurrentItem;

            if (track is not null)
            {
                string msg = $"Now Playing: **{track.Track.Title}**";
                EmbedBuilder embed = BuildMusicEmbed("Skip", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                string msg = $"The last item in the queue was skipped and there is nothing playing.";
                EmbedBuilder embed = BuildMusicEmbed("Skip", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("resume", "Resumes the current audio playing")]
        public async Task ResumeAsync()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Resume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is not PlayerState.Paused)
            {
                EmbedBuilder embed = BuildMusicEmbed("Resume", "Can't resume something that is currently playing.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.ResumeAsync();
            string msg = $"Resumed: {player.CurrentTrack.Title}";
            EmbedBuilder result = BuildMusicEmbed("Resume", msg);
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("pause", "Pause the current audio playing.")]
        public async Task PauseAsync()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Pause", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is PlayerState.Paused)
            {
                EmbedBuilder embed = BuildMusicEmbed("Pause", "The current track is already paused.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await player.PauseAsync();
            string msg = $"Paused: {player.CurrentTrack.Title}";
            EmbedBuilder result = BuildMusicEmbed("Paused", msg);
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("stop", "Stops the audio, clears the queue, and leaves the voice channel.")]
        public async Task StopAsync()
        {
            await DeferAsync();
            ILavalinkPlayer? isConnected = await _audioService.Players.GetPlayerAsync(Context.Guild).ConfigureAwait(false);

            if (isConnected != null)
            {
                await isConnected.StopAsync().ConfigureAwait(false);
                await isConnected.DisconnectAsync().ConfigureAwait(false);
            }
            else
            {
                QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

                if (player is null)
                {
                    EmbedBuilder embed = BuildMusicEmbed("Stop", "I'm not connected to a voice channel.");
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                if (player.CurrentItem == null)
                {
                    EmbedBuilder embed = BuildMusicEmbed("Stop", "There is nothing playing.");
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                await player.StopAsync().ConfigureAwait(false);
                await player.DisconnectAsync().ConfigureAwait(false);
            }

            DeletePlayerConnected(Int64.Parse(Context.Guild.Id.ToString()));
            EmbedBuilder result = new EmbedBuilder
            {
                Title = $"Music - Leave",
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
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Volume", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            StoredProcedure procedure = new StoredProcedure();
            long guildId = Int64.Parse(player.GuildId.ToString());

            if (ushort.TryParse(volume.ToString(), out ushort vol))
            {
                await player.SetVolumeAsync(vol / 100f).ConfigureAwait(false);

                procedure.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateVolume", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerUID", guildId),
                        new SqlParameter("@Volume", volume)
                    });

                string msg = $"I've changed the player volume to {volume.ToString()}.";
                EmbedBuilder embed = BuildMusicEmbed("Volume", msg);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedBuilder embed = BuildMusicEmbed("Volume", "Please enter a volume between 0 and 150.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("nowplaying", "View the current track.")]
        public async Task NowPlayingAsync()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Now Playing", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State is not PlayerState.Playing)
            {
                EmbedBuilder embed = BuildMusicEmbed("Now Playing", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            LavalinkTrack? track = player.CurrentTrack;
            string artworkUrl = "";

            if (track.ArtworkUri != null)
                artworkUrl = track.ArtworkUri.ToString();

            string artwork = artworkUrl;

            EmbedBuilder npEmbed = new EmbedBuilder()
            {
                Title = $"Music - Now Playing: **{track.Title}**",
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
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Queue", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State != PlayerState.Playing)
            {
                EmbedBuilder embed = BuildMusicEmbed("Queue", "Woaaah there, I'm not playing any tracks.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count == 0)
            {
                LavalinkTrack? track = player.CurrentTrack;

                string artworkUrl = "";

                if (track.ArtworkUri != null)
                    artworkUrl = track.ArtworkUri.ToString();

                string artwork = artworkUrl;

                EmbedBuilder npEmbed = new EmbedBuilder()
                {
                    Title = $"Music - Queue: **{track.Title}**",
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
                foreach (ITrackQueueItem p in player.Queue)
                {
                    i++;
                    TimeSpan duration = new TimeSpan(p.Track.Duration.Hours, p.Track.Duration.Minutes, p.Track.Duration.Seconds);
                    queue += i.ToString() + ". **" + p.Track.Title + "** - " + duration + " \n " + p.Track.Uri + "\n\n";

                    if (i % 10 == 0)
                    {
                        pages.Add(new PageBuilder().WithTitle($"**Queue ({player.Queue.Count} total items)**").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());
                        queue = "";
                    }
                }

                pages.Add(new PageBuilder().WithTitle($"**Queue ({player.Queue.Count} total items)**").WithDescription(queue).WithColor(Discord.Color.Blue).WithCurrentTimestamp());

                StaticPaginator paginator = new StaticPaginatorBuilder()
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
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Loop", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Loop", "There is no track available to loop.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            LavalinkTrack? track = player.CurrentTrack;

            for (int i = 0; i < times; i++)
                await player.PlayAsync(track);

            EmbedBuilder result = new EmbedBuilder
            {
                Title = $"Music - Loop",
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
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Repeat", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.CurrentItem is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Repeat", "There is no track available to repeat.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            LavalinkTrack? track = player.CurrentTrack;
            await player.PlayAsync(track).ConfigureAwait(false);

            EmbedBuilder result = new EmbedBuilder
            {
                Title = $"Music - Repeat",
                Color = Color.Blue,
                Description = $"Repeating {track.Title}",
                ThumbnailUrl = "",
            };

            result.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username)
                                .WithCurrentTimestamp();
            await FollowupAsync(embed: result.Build()).ConfigureAwait(false);
        }

        [SlashCommand("swap", "Switch two tracks in the queue.")]
        public async Task SwapTrack([MinValue(0)] int oldPosition, [MinValue(0)] int newPosition)
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Swap", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            EmbedHelper embedHelper = new EmbedHelper();

            if (player.Queue.ElementAt(oldPosition) != null && player.Queue.ElementAt(newPosition) != null)
            {
                List<ITrackQueueItem> itemList = player.Queue.ToList();
                ITrackQueueItem val = itemList[oldPosition];
                itemList[oldPosition] = itemList[newPosition];
                itemList[newPosition] = val;

                await player.Queue.ClearAsync();

                foreach (ITrackQueueItem? i in itemList)
                    await player.Queue.AddAsync(i);

                EmbedBuilder embed = BuildMusicEmbed("Swap", $"Successfully swapped **{itemList[oldPosition].Track.Title}** and **{itemList[newPosition].Track.Title}** in the queue.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Swap Error", "Both elements must be present in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("shuffle", "Randomizes the queue.")]
        public async Task ShuffleVueue()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Shuffle", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count > 1)
            {
                await player.Queue.ShuffleAsync();
                EmbedBuilder embed = BuildMusicEmbed("Shuffle", "Queue Shuffled");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", "The queue must have more than one element in it to shuffle.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("clear", "Removes everything in the queue.")]
        public async Task ClearQueue()
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Clear", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            StoredProcedure stored = new StoredProcedure();
            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteMusicQueueAll", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString()))
            });

            if (player.Queue.Count > 1)
            {
                await player.Queue.ClearAsync();
                EmbedBuilder embed = BuildMusicEmbed("Clear", "Queue is now empty");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", "The queue must have one element to clear.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("remove", "Deletes a track from the queue.")]
        public async Task RemoveItem([MinValue(0)] int element)
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Remove", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                element--;
                ITrackQueueItem track = player.Queue.ElementAt(element);
                ValueTask<bool> item = player.Queue.RemoveAtAsync(element);

                EmbedBuilder embed = BuildMusicEmbed("Remove", $"Removed **{track.Track.Title}** from the queue");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            catch
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", "The element does not exist in the queue.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        [SlashCommand("seek", "Goes to a specific time of the current track.")]
        public async Task SeekAsync([MinLength(1)] string timeSpan)
        {
            await DeferAsync();
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                EmbedBuilder embed = BuildMusicEmbed("Remove", "I'm not connected to a voice channel.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (player.State != PlayerState.Playing)
            {
                EmbedBuilder embed = BuildMusicEmbed("Seek", "Woaaah there, I can't seek when nothing is playing.");
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                if (TimeSpan.TryParse(timeSpan, out TimeSpan time))
                {
                    await player.SeekAsync(time);
                    string msg = $"I've seeked `{player.CurrentTrack.Title}` to {timeSpan}.";
                    EmbedBuilder embed = BuildMusicEmbed("Seek", msg);
                    await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                }
                else
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Error", "Please enter a valid seek time.\n**Example: -seek 00:00:30**\nAbove example would seek 30 seconds into the video.", Constants.Constants.errorImageUrl, "", Color.Red, "").Build()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                EmbedBuilder embed = embedHelper.BuildMessageEmbed("Error", exception.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        #region Helpers
        // Get Available Player or Join to a Voice Channel
        private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
        {
            PlayerChannelBehavior channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            CustomPlayerOptions options = new CustomPlayerOptions();
            options.SelfMute = true;
            options.TextChannel = Context.Channel as ITextChannel;
            PlayerRetrieveOptions retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

            PlayerResult<CustomPlayer> result = await _audioService.Players
                .RetrieveAsync<CustomPlayer, CustomPlayerOptions>(Context, CreatePlayerAsync, options, retrieveOptions)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                string errorMessage = result.Status switch
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
        private EmbedBuilder BuildMusicEmbed(string title, string description, string artwork = "", double duration = 0.0)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"Music - {title}",
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
                new SqlParameter("@VoiceChannelID", Int64.Parse(voiceState.VoiceChannel.Id.ToString())),
                new SqlParameter("@TextChannelID", Int64.Parse((Context.Channel as ITextChannel).Id.ToString())),
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

            stored.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteMusicQueueAll", new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(serverId.ToString()))
            });
        }

        // For Single Tracks and Searches
        private async Task PlaySingleTrackAsync(LavalinkPlayer? player, LavalinkTrack? track, bool playNext = false)
        {
            if (player == null || track == null) return;

            AddMusicTable(track, Context.Guild.Id.ToString(), Context.User.Username);

            // Extract metadata
            TimeSpan duration = track.Duration;
            string artist = "";
            string albumName = "";

            if (track.AdditionalInformation?.Count > 0)
            {
                foreach (var (key, value) in track.AdditionalInformation)
                {
                    if (key.Equals("artistUrl", StringComparison.OrdinalIgnoreCase))
                        artist = value.ToString();
                    else if (key.Equals("albumName", StringComparison.OrdinalIgnoreCase))
                        albumName = value.ToString();
                }
            }

            // Play the track
            if (playNext)
            {
                var queued = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
                if (queued?.Queue.Count > 0)
                {
                    var items = queued.Queue.ToList();
                    await queued.Queue.ClearAsync().ConfigureAwait(false);
                    await player.PlayAsync(track).ConfigureAwait(false);
                    foreach (var item in items)
                        await queued.Queue.AddAsync(item).ConfigureAwait(false);
                }
                else
                {
                    await player.PlayAsync(track).ConfigureAwait(false);
                }
            }
            else
            {
                await player.PlayAsync(track).ConfigureAwait(false);
            }

            // Set volume
            float volume = GetVolume(long.Parse(Context.Guild.Id.ToString())) / 100f;
            await player.SetVolumeAsync(volume).ConfigureAwait(false);

            // Build and send embed
            var embed = await BuildTrackEmbedAsync("Queued", track, artist, albumName, duration, volume);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        // For Playlists and Multiple Tracks
        private async Task PlayMultipleTracksAsync(LavalinkPlayer? player, TrackLoadResult tracks, bool playNext = false)
        {
            if (player == null || tracks == null || tracks.Tracks.Count() == 0)
                return;

            string playlistName = "";
            string totalTracks = "";
            string artworkUrl = "";
            string playlistUrl = "";

            if (tracks.Playlist != null)
            {
                var playlist = tracks.Playlist;
                playlistName = playlist.Name;

                foreach (var (key, value) in playlist.AdditionalInformation)
                {
                    switch (key)
                    {
                        case "totalTracks":
                            totalTracks = value.ToString();
                            break;
                        case "artworkUrl":
                            artworkUrl = value.ToString();
                            break;
                        case "url":
                            playlistUrl = value.ToString();
                            break;
                    }
                }
            }

            string msg = $"Playlist Name: **{playlistName}** with {totalTracks} items added." +
                         $"\nURL: {playlistUrl}" +
                         $"\nVolume: **{GetVolume(long.Parse(Context.Guild.Id.ToString()))}%**";

            var guildIdStr = Context.Guild.Id.ToString();
            var userName = Context.User.Username;

            if (playNext)
            {
                var queued = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
                if (queued != null && queued.Queue.Count > 0)
                {
                    var currentQueue = queued.Queue.ToList();
                    await queued.Queue.ClearAsync().ConfigureAwait(false);

                    foreach (var track in tracks.Tracks)
                    {
                        await player.PlayAsync(track).ConfigureAwait(false);
                        AddMusicTable(track, guildIdStr, userName);
                    }

                    foreach (var item in currentQueue)
                        await queued.Queue.AddAsync(item).ConfigureAwait(false);
                }
                else
                {
                    foreach (var track in tracks.Tracks)
                    {
                        await player.PlayAsync(track).ConfigureAwait(false);
                        AddMusicTable(track, guildIdStr, userName);
                    }
                }
            }
            else
            {
                foreach (var track in tracks.Tracks)
                {
                    await player.PlayAsync(track).ConfigureAwait(false);
                    AddMusicTable(track, guildIdStr, userName);
                }
            }

            float volume = GetVolume(long.Parse(guildIdStr)) / 100f;
            await player.SetVolumeAsync(volume).ConfigureAwait(false);

            var embed = BuildMusicEmbed("Queued", msg, artworkUrl);
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

        private async Task<EmbedBuilder> BuildTrackEmbedAsync(string title, LavalinkTrack track, string artist, string albumName, TimeSpan duration, float volume)
        {
            QueuedLavalinkPlayer? player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
            string msg = $"**[{duration:hh\\:mm\\:ss}] {(string.IsNullOrEmpty(artist) ? $"{track.Author}" : artist)}**\n" +
                         $"**{track.Title}**\n" +
                         $"{track.Uri}\n" +
                         $"**{track.SourceName.ToUpper()} | Volume: {volume * 100}%**\n" +
                         $"**Total in Queue: {(player != null ? player.Queue.Count : 0)}**";

            return BuildMusicEmbed(title, msg);
        }
        #endregion
    }
}
