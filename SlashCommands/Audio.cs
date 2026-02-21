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

namespace DiscordBot.SlashCommands;

/// <summary>
/// Slash command module that handles all audio/music playback functionality
/// via Lavalink4NET, including queueing, playback control, and volume management.
/// </summary>
public sealed class Audio(IAudioService audioService, InteractiveService interactiveService)
    : InteractionModuleBase<SocketInteractionContext>
{
    private const string EmojiMusic = "🎵";
    private const string EmojiPlay = "▶️";
    private const string EmojiPause = "⏸️";
    private const string EmojiStop = "⏹️";
    private const string EmojiSkip = "⏭️";
    private const string EmojiVolume = "🔊";
    private const string EmojiQueue = "📋";
    private const string EmojiLoop = "🔁";
    private const string EmojiShuffle = "🔀";
    private const string EmojiJoin = "👋";
    private const string EmojiLeave = "🚪";
    private const string EmojiSeek = "⏩";
    private const string EmojiError = "❌";
    private const string EmojiSuccess = "✅";

    private static readonly Color ColourDefault = new(88, 101, 242);
    private static readonly Color ColourSuccess = new(87, 242, 135);
    private static readonly Color ColourError = new(237, 66, 69);
    private static readonly Color ColourWarning = new(254, 231, 92);

    /// <summary>
    /// Gets the current guild ID cast to <see cref="long"/> without repeated
    /// Parse/ToString chains throughout the class.
    /// </summary>
    private long GuildId => (long)Context.Guild.Id;

    #region Slash Commands

    /// <summary>
    /// Joins the voice channel the invoking user is currently in.
    /// Responds with an error if the user is not in a voice channel or the
    /// bot is already connected.
    /// </summary>
    [SlashCommand("join", "Joins your voice channel.", runMode: RunMode.Async)]
    public async Task JoinAsync()
    {
        await DeferAsync();

        if (Context.User is not IVoiceState { VoiceChannel: not null } voiceState)
        {
            await ReplyEmbedAsync(EmojiJoin, "Join", "You must be in a voice channel first.", ColourError);
            return;
        }

        await audioService.StartAsync();
        await Task.Delay(3_000);
        AddPlayerConnected(voiceState);

        var player = await GetPlayerAsync(connectToVoiceChannel: true);

        if (player is null || player.ConnectionState.IsConnected)
        {
            await ReplyEmbedAsync(EmojiJoin, "Join", "I'm already connected to a voice channel!", ColourWarning);
            return;
        }

        int vol = GetVolume(GuildId);
        var embed = Embed(EmojiJoin, "Joined", ColourSuccess)
            .WithDescription($"Ready to play! Current volume is **{vol}%**.")
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    /// Disconnects the bot from the voice channel it is currently in and
    /// cleans up the player record from the database.
    /// </summary>
    [SlashCommand("leave", "Leaves the voice channel.")]
    public async Task LeaveAsync()
    {
        await DeferAsync();

        var connected = await audioService.Players.GetPlayerAsync(Context.Guild);

        if (connected is not null)
        {
            await connected.DisconnectAsync();
        }
        else
        {
            var player = await GetPlayerAsync();

            if (player is null)
            {
                return;
            }
        }

        DeletePlayerConnected(GuildId);
        await FollowupAsync(embed: LeaveEmbed().Build());
    }

    /// <summary>
    /// Plays a track or playlist from YouTube, Spotify, SoundCloud, Twitch,
    /// or Twitter/X. Automatically joins the user's voice channel if the bot
    /// is not already connected.
    /// </summary>
    [SlashCommand("play", "Play a track or playlist from YouTube, Spotify, SoundCloud, etc.", runMode: RunMode.Async)]
    public async Task PlayAsync([MinLength(1)] string query)
    {
        await DeferAsync();
        query = HandleTwitter(query);

        var player = (await audioService.Players.RetrieveAsync(Context)).Player
                     ?? await EnsureJoinedAsync();

        if (player is null)
        {
            return;
        }

        await QueueAndPlayAsync(player, query, playNext: false);
    }

    /// <summary>
    /// Same as <see cref="PlayAsync"/> but inserts the resolved track(s)
    /// immediately after the currently playing track rather than at the end
    /// of the queue.
    /// </summary>
    [SlashCommand("playnext", "Same as /play but inserts the track next in queue.", runMode: RunMode.Async)]
    public async Task PlayNextAsync([MinLength(1)] string query)
    {
        await DeferAsync();
        query = HandleTwitter(query);

        var player = (await audioService.Players.RetrieveAsync(Context)).Player
                     ?? await EnsureJoinedAsync();

        if (player is null)
        {
            return;
        }

        await QueueAndPlayAsync(player, query, playNext: true);
    }

    /// <summary>
    /// Immediately skips the currently playing track and begins playing the
    /// next track in the queue, if one exists.
    /// </summary>
    [SlashCommand("forceskip", "Skips the current track.")]
    public async Task ForceSkipAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiSkip, "Skip", "Nothing is playing right now.", ColourWarning);
            return;
        }

        await player.SkipAsync();

        if (player.CurrentItem is { } next)
        {
            await ReplyEmbedAsync(EmojiSkip, "Skipped", $"Now playing: **{next.Track.Title}**", ColourSuccess);
        }
        else
        {
            await ReplyEmbedAsync(EmojiSkip, "Skipped", "Queue is now empty.", ColourDefault);
        }
    }

    /// <summary>
    /// Resumes playback of the currently paused track.
    /// Responds with a warning if the player is not in a paused state.
    /// </summary>
    [SlashCommand("resume", "Resumes the paused track.")]
    public async Task ResumeAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is not PlayerState.Paused)
        {
            await ReplyEmbedAsync(EmojiPlay, "Resume", "The player isn't paused.", ColourWarning);
            return;
        }

        await player.ResumeAsync();
        await ReplyEmbedAsync(EmojiPlay, "Resumed", $"▶️  **{player.CurrentTrack!.Title}**", ColourSuccess);
    }

    /// <summary>
    /// Pauses the currently playing track.
    /// Responds with a warning if the player is already paused.
    /// </summary>
    [SlashCommand("pause", "Pauses the current track.")]
    public async Task PauseAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is PlayerState.Paused)
        {
            await ReplyEmbedAsync(EmojiPause, "Pause", "Already paused.", ColourWarning);
            return;
        }

        await player.PauseAsync();
        await ReplyEmbedAsync(EmojiPause, "Paused", $"⏸️  **{player.CurrentTrack!.Title}**", ColourDefault);
    }

    /// <summary>
    /// Stops all playback, clears the queue, disconnects the bot from the
    /// voice channel, and removes the player record from the database.
    /// </summary>
    [SlashCommand("stop", "Stops playback, clears the queue, and disconnects.")]
    public async Task StopAsync()
    {
        await DeferAsync();

        var connected = await audioService.Players.GetPlayerAsync(Context.Guild);

        if (connected is not null)
        {
            await connected.StopAsync();
            await connected.DisconnectAsync();
        }
        else
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.CurrentItem is null)
            {
                await ReplyEmbedAsync(EmojiStop, "Stop", "Nothing is playing.", ColourWarning);
                return;
            }

            await player.StopAsync();
            await player.DisconnectAsync();
        }

        DeletePlayerConnected(GuildId);
        await FollowupAsync(embed: LeaveEmbed().Build());
    }

    /// <summary>
    /// Sets the playback volume to the specified value between 0 and 100,
    /// persists the new value to the database, and displays a visual volume bar.
    /// </summary>
    [SlashCommand("volume", "Set playback volume (0–100).")]
    public async Task VolumeAsync([MinValue(0), MaxValue(100)] int volume)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        await player.SetVolumeAsync(volume / 100f);

        new StoredProcedure().UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateVolume",
        [
            new SqlParameter("@ServerUID", GuildId),
            new SqlParameter("@Volume", volume)
        ]);

        string bar = VolumeBar(volume);
        var embed = Embed(EmojiVolume, "Volume", ColourSuccess)
            .AddField("Level", $"{bar}  **{volume}%**", inline: false);

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    /// Displays an embed showing details about the track that is currently
    /// playing, including artwork, artist, duration, source, and queue depth.
    /// </summary>
    [SlashCommand("nowplaying", "Shows the currently playing track.")]
    public async Task NowPlayingAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is not PlayerState.Playing)
        {
            await ReplyEmbedAsync(EmojiPlay, "Now Playing", "Nothing is playing right now.", ColourWarning);
            return;
        }

        var track = player.CurrentTrack!;
        await FollowupAsync(embed: BuildNowPlayingEmbed(track, player.Queue.Count).Build());
    }

    /// <summary>
    /// Displays a paginated embed listing all tracks in the queue (10 per page).
    /// If the queue is empty, shows the now-playing embed instead.
    /// </summary>
    [SlashCommand("queue", "Shows the upcoming tracks.")]
    public async Task GetQueueAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ReplyEmbedAsync(EmojiQueue, "Queue", "Nothing is playing right now.", ColourWarning);
            return;
        }

        var current = player.CurrentTrack!;

        if (player.Queue.Count == 0)
        {
            await FollowupAsync(embed: BuildNowPlayingEmbed(current, 0).Build());
            return;
        }

        var pages = new List<PageBuilder>();
        var sb = new System.Text.StringBuilder();
        int i = 0;

        foreach (var item in player.Queue)
        {
            i++;
            var dur = item.Track.Duration;
            sb.AppendLine($"`{i:00}.` [{item.Track.Title}]({item.Track.Uri}) — `{dur:hh\\:mm\\:ss}`");

            if (i % 10 == 0)
            {
                pages.Add(QueuePage(sb.ToString(), player.Queue.Count, current));
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            pages.Add(QueuePage(sb.ToString(), player.Queue.Count, current));
        }

        var paginator = new StaticPaginatorBuilder()
            .AddUser(Context.User)
            .WithPages(pages)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// Re-queues the currently playing track a specified number of additional
    /// times so that it repeats back-to-back after finishing.
    /// </summary>
    [SlashCommand("loop", "Queues the current track N more times.")]
    public async Task LoopAsync([MinValue(1)] int times)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiLoop, "Loop", "Nothing is playing to loop.", ColourWarning);
            return;
        }

        var track = player.CurrentTrack!;

        for (int i = 0; i < times; i++)
        {
            await player.PlayAsync(track);
        }

        await ReplyEmbedAsync(EmojiLoop, "Loop", $"**{track.Title}** will repeat **{times}** more time(s).", ColourSuccess);
    }

    /// <summary>
    /// Re-queues the currently playing track exactly one additional time so
    /// that it plays again immediately after the current playthrough ends.
    /// </summary>
    [SlashCommand("repeat", "Queues the current track one more time.")]
    public async Task RepeatAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiLoop, "Repeat", "Nothing is playing to repeat.", ColourWarning);
            return;
        }

        var track = player.CurrentTrack!;
        await player.PlayAsync(track);
        await ReplyEmbedAsync(EmojiLoop, "Repeat", $"**{track.Title}** added to queue again.", ColourSuccess);
    }

    /// <summary>
    /// Swaps the positions of two tracks in the queue identified by their
    /// 0-based index positions.
    /// </summary>
    [SlashCommand("swap", "Swaps two tracks in the queue by position.")]
    public async Task SwapAsync([MinValue(0)] int posA, [MinValue(0)] int posB)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        try
        {
            var list = player.Queue.ToList();
            (list[posA], list[posB]) = (list[posB], list[posA]);

            await player.Queue.ClearAsync();

            foreach (var item in list)
            {
                await player.Queue.AddAsync(item);
            }

            await ReplyEmbedAsync(EmojiSuccess, "Swap",
                $"Swapped **#{posA + 1}** and **#{posB + 1}** in the queue.", ColourSuccess);
        }
        catch
        {
            await ReplyEmbedAsync(EmojiError, "Swap Failed", "One or both positions don't exist in the queue.", ColourError);
        }
    }

    /// <summary>
    /// Randomly shuffles the order of all tracks currently in the queue.
    /// Requires at least two tracks to be present.
    /// </summary>
    [SlashCommand("shuffle", "Randomises the queue.")]
    public async Task ShuffleAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.Count < 2)
        {
            await ReplyEmbedAsync(EmojiShuffle, "Shuffle", "Need at least 2 tracks in the queue to shuffle.", ColourWarning);
            return;
        }

        await player.Queue.ShuffleAsync();
        await ReplyEmbedAsync(EmojiShuffle, "Shuffled", $"**{player.Queue.Count}** tracks shuffled.", ColourSuccess);
    }

    /// <summary>
    /// Removes all tracks from the queue and deletes the corresponding
    /// database records for this guild.
    /// </summary>
    [SlashCommand("clear", "Removes all tracks from the queue.")]
    public async Task ClearQueueAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.Count < 1)
        {
            await ReplyEmbedAsync(EmojiQueue, "Clear", "The queue is already empty.", ColourWarning);
            return;
        }

        int count = player.Queue.Count;
        await player.Queue.ClearAsync();

        new StoredProcedure().UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteMusicQueueAll",
        [
            new SqlParameter("@ServerID", GuildId)
        ]);

        await ReplyEmbedAsync(EmojiQueue, "Queue Cleared", $"Removed **{count}** track(s).", ColourSuccess);
    }

    /// <summary>
    /// Removes a single track from the queue at the specified 1-based position.
    /// </summary>
    [SlashCommand("remove", "Removes a specific track from the queue by position.")]
    public async Task RemoveAsync([MinValue(1)] int position)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        try
        {
            int index = position - 1;
            var track = player.Queue.ElementAt(index);
            await player.Queue.RemoveAtAsync(index);
            await ReplyEmbedAsync(EmojiSuccess, "Removed",
                $"Removed **{track.Track.Title}** from position **#{position}**.", ColourSuccess);
        }
        catch
        {
            await ReplyEmbedAsync(EmojiError, "Remove Failed", $"No track at position **#{position}**.", ColourError);
        }
    }

    /// <summary>
    /// Seeks to a specific timestamp within the currently playing track.
    /// Accepts timestamps in the format <c>hh:mm:ss</c>, e.g. <c>00:01:30</c>.
    /// </summary>
    [SlashCommand("seek", "Jumps to a timestamp in the current track (e.g. 00:01:30).")]
    public async Task SeekAsync([MinLength(1)] string timestamp)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ReplyEmbedAsync(EmojiSeek, "Seek", "Nothing is playing right now.", ColourWarning);
            return;
        }

        if (!TimeSpan.TryParse(timestamp, out var time))
        {
            await ReplyEmbedAsync(EmojiError, "Invalid Timestamp",
                "Use the format `hh:mm:ss` — e.g. `/seek 00:01:30` to jump to 1 min 30 sec.", ColourError);
            return;
        }

        try
        {
            await player.SeekAsync(time);
            await ReplyEmbedAsync(EmojiSeek, "Seeked",
                $"**{player.CurrentTrack!.Title}** → `{timestamp}`", ColourSuccess);
        }
        catch (Exception ex)
        {
            await ReplyEmbedAsync(EmojiError, "Seek Failed", ex.Message, ColourError);
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Retrieves or creates a <see cref="QueuedLavalinkPlayer"/> for the current
    /// guild context. When <paramref name="connectToVoiceChannel"/> is <c>true</c>
    /// the bot will join the user's channel; otherwise it only retrieves an
    /// already-connected player. Returns <c>null</c> and sends an error embed
    /// automatically on any failure.
    /// </summary>
    private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var options = new CustomPlayerOptions
        {
            SelfMute = true,
            TextChannel = Context.Channel as ITextChannel
        };

        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None);

        var result = await audioService.Players
            .RetrieveAsync<CustomPlayer, CustomPlayerOptions>(
                Context, CreatePlayerAsync, options, retrieveOptions);

        if (!result.IsSuccess)
        {
            string msg = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You must be in a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "I'm not connected to a voice channel.",
                _ => "An unknown error occurred."
            };
            await ReplyEmbedAsync(EmojiError, "Error", msg, ColourError);
            return null;
        }

        return result.Player;
    }

    /// <summary>
    /// Ensures the bot has joined the invoking user's voice channel before
    /// attempting playback. Starts the audio service, registers the player in
    /// the database, and returns the connected player, or <c>null</c> on failure.
    /// </summary>
    private async Task<LavalinkPlayer?> EnsureJoinedAsync()
    {
        if (Context.User is not IVoiceState { VoiceChannel: not null } voiceState)
        {
            await ReplyEmbedAsync(EmojiError, "Error", "You must be in a voice channel.", ColourError);
            return null;
        }

        await audioService.StartAsync();
        await Task.Delay(3_000);
        AddPlayerConnected(voiceState);
        return await GetPlayerAsync(connectToVoiceChannel: true);
    }

    /// <summary>
    /// Shared entry point for both <c>/play</c> and <c>/playnext</c>. Loads
    /// tracks from the audio service for the given query and delegates to
    /// either <see cref="PlaySingleTrackAsync"/> or
    /// <see cref="PlayMultipleTracksAsync"/> depending on the result.
    /// </summary>
    private async Task QueueAndPlayAsync(LavalinkPlayer player, string query, bool playNext)
    {
        var tracks = await audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

        if (tracks.IsFailed)
        {
            await ReplyEmbedAsync(EmojiError, "Not Found", $"No results for `{query}`.", ColourError);
            return;
        }

        bool isUrl = Uri.IsWellFormedUriString(query, UriKind.Absolute);
        bool isPlaylist = isUrl && tracks.Count > 1;

        if (isPlaylist)
        {
            await PlayMultipleTracksAsync(player, tracks, playNext);
        }
        else
        {
            await PlaySingleTrackAsync(player, tracks.Track, playNext);
        }
    }

    /// <summary>
    /// Queues a single <see cref="LavalinkTrack"/>, optionally inserting it
    /// before the rest of the queue when <paramref name="playNext"/> is
    /// <c>true</c>. Sets volume and sends a confirmation embed.
    /// </summary>
    private async Task PlaySingleTrackAsync(LavalinkPlayer player, LavalinkTrack track, bool playNext)
    {
        AddMusicTable(track, Context.Guild.Id.ToString(), Context.User.Username);

        string artist = "";
        string albumName = "";

        if (track.AdditionalInformation is { Count: > 0 } info)
        {
            if (info.TryGetValue("artistUrl", out var artistVal))
            {
                artist = artistVal.ToString();
            }

            if (info.TryGetValue("albumName", out var albumVal))
            {
                albumName = albumVal.ToString();
            }
        }

        if (playNext)
        {
            var queued = await GetPlayerAsync(connectToVoiceChannel: false);

            if (queued?.Queue.Count > 0)
            {
                var saved = queued.Queue.ToList();
                await queued.Queue.ClearAsync();
                await player.PlayAsync(track);

                foreach (var item in saved)
                {
                    await queued.Queue.AddAsync(item);
                }
            }
            else
            {
                await player.PlayAsync(track);
            }
        }
        else
        {
            await player.PlayAsync(track);
        }

        float volume = GetVolume(GuildId) / 100f;
        await player.SetVolumeAsync(volume);

        var queuedPlayer = await GetPlayerAsync(connectToVoiceChannel: false);
        int queueCount = queuedPlayer?.Queue.Count ?? 0;
        string displayArtist = string.IsNullOrEmpty(artist) ? track.Author : artist;

        var embed = Embed(EmojiPlay, playNext ? "Playing Next" : "Added to Queue", ColourSuccess)
            .WithThumbnailUrl(track.ArtworkUri?.ToString())
            .AddField("Track", $"[{track.Title}]({track.Uri})", inline: false)
            .AddField("Artist", displayArtist, inline: true)
            .AddField("Duration", $"`{track.Duration:hh\\:mm\\:ss}`", inline: true)
            .AddField("Source", track.SourceName.ToUpperInvariant(), inline: true);

        if (!string.IsNullOrEmpty(albumName))
        {
            embed.AddField("Album", albumName, inline: true);
        }

        embed.AddField("Volume", $"{volume * 100:0}%", inline: true)
             .AddField("In Queue", $"{queueCount}", inline: true);

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    /// Queues all tracks from a resolved playlist result, optionally inserting
    /// them before the existing queue when <paramref name="playNext"/> is
    /// <c>true</c>. Sets volume and sends a playlist confirmation embed.
    /// </summary>
    private async Task PlayMultipleTracksAsync(LavalinkPlayer player, TrackLoadResult tracks, bool playNext)
    {
        string playlistName = "";
        string totalTracks = "";
        string artworkUrl = "";
        string playlistUrl = "";

        if (tracks.Playlist is { } playlist)
        {
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

        var guildIdStr = Context.Guild.Id.ToString();
        var userName = Context.User.Username;

        if (playNext)
        {
            var queued = await GetPlayerAsync(connectToVoiceChannel: false);

            if (queued?.Queue.Count > 0)
            {
                var saved = queued.Queue.ToList();
                await queued.Queue.ClearAsync();

                foreach (var t in tracks.Tracks)
                {
                    await player.PlayAsync(t);
                    AddMusicTable(t, guildIdStr, userName);
                }

                foreach (var item in saved)
                {
                    await queued.Queue.AddAsync(item);
                }
            }
            else
            {
                foreach (var t in tracks.Tracks)
                {
                    await player.PlayAsync(t);
                    AddMusicTable(t, guildIdStr, userName);
                }
            }
        }
        else
        {
            foreach (var t in tracks.Tracks)
            {
                await player.PlayAsync(t);
                AddMusicTable(t, guildIdStr, userName);
            }
        }

        float volume = GetVolume(GuildId) / 100f;
        await player.SetVolumeAsync(volume);

        var embed = Embed(EmojiPlay, playNext ? "Playlist — Playing Next" : "Playlist Added", ColourSuccess)
            .WithThumbnailUrl(artworkUrl);

        if (!string.IsNullOrEmpty(playlistUrl))
        {
            embed.WithUrl(playlistUrl);
        }

        embed.AddField("Playlist", playlistName, inline: false)
             .AddField("Tracks", totalTracks, inline: true)
             .AddField("Volume", $"{volume * 100:0}%", inline: true);

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    /// Rewrites Twitter/X URLs to use the fxtwitter proxy so that Lavalink
    /// can correctly resolve and stream the media.
    /// </summary>
    private static string HandleTwitter(string query)
    {
        if (query.Contains("https://twitter.com"))
        {
            return query.Replace("twitter", "dl.fxtwitter");
        }

        if (query.Contains("https://x.com"))
        {
            return query.Replace("x.com", "dl.fxtwitter.com");
        }

        return query;
    }

    /// <summary>
    /// Factory method required by Lavalink4NET to instantiate a
    /// <see cref="CustomPlayer"/> with the provided player properties.
    /// </summary>
    private static ValueTask<CustomPlayer> CreatePlayerAsync(
        IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);
        return ValueTask.FromResult(new CustomPlayer(properties));
    }

    #endregion

    #region Embed Factories

    /// <summary>
    /// Creates a base <see cref="EmbedBuilder"/> with a consistent title,
    /// colour, footer (showing the requesting user), and current timestamp.
    /// </summary>
    private EmbedBuilder Embed(string emoji, string title, Color color) =>
        new EmbedBuilder()
            .WithTitle($"{emoji}  {title}")
            .WithColor(color)
            .WithFooter($"Requested by {Context.User.Username}", Context.User.GetAvatarUrl())
            .WithCurrentTimestamp();

    /// <summary>
    /// Builds and sends a simple description-only embed as a follow-up message.
    /// Used for short success, warning, and error responses.
    /// </summary>
    private async Task ReplyEmbedAsync(string emoji, string title, string description, Color color)
    {
        var embed = Embed(emoji, title, color).WithDescription(description);
        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    /// Builds a rich "Now Playing" embed showing track artwork, title as a
    /// hyperlink, artist, duration, source, and the number of remaining tracks.
    /// </summary>
    private EmbedBuilder BuildNowPlayingEmbed(LavalinkTrack track, int queueRemaining)
    {
        var embed = Embed(EmojiPlay, "Now Playing", ColourDefault)
            .WithThumbnailUrl(track.ArtworkUri?.ToString())
            .WithDescription($"### [{track.Title}]({track.Uri})")
            .AddField("Artist", track.Author, inline: true)
            .AddField("Duration", $"`{track.Duration:hh\\:mm\\:ss}`", inline: true)
            .AddField("Source", track.SourceName.ToUpperInvariant(), inline: true)
            .AddField("Up Next", $"{queueRemaining} track(s)", inline: true);

        return embed;
    }

    /// <summary>
    /// Builds a single page for the paginated queue display, showing the
    /// currently playing track and a numbered list of upcoming items.
    /// </summary>
    private PageBuilder QueuePage(string content, int total, LavalinkTrack current) =>
        new PageBuilder()
            .WithTitle($"{EmojiQueue}  Queue  —  {total} track(s)")
            .WithDescription($"**Now playing:** {current.Title}\n\n{content}")
            .WithColor(ColourDefault)
            .WithCurrentTimestamp();

    /// <summary>
    /// Builds the standard "Disconnected / Goodbye" embed used by both
    /// <c>/leave</c> and <c>/stop</c>.
    /// </summary>
    private EmbedBuilder LeaveEmbed() =>
        Embed(EmojiLeave, "Disconnected", ColourDefault)
            .WithDescription("Goodbye! Have a great time. 👋");

    /// <summary>
    /// Generates a 10-block Unicode progress bar representing the current
    /// volume level, e.g. <c>████████░░</c> for 80%.
    /// </summary>
    private static string VolumeBar(int volume)
    {
        int filled = volume / 10;
        return string.Create(10, filled, (span, f) =>
        {
            for (int i = 0; i < 10; i++)
            {
                span[i] = i < f ? '█' : '░';
            }
        });
    }

    #endregion

    #region DB Helpers

    /// <summary>
    /// Retrieves the stored playback volume for the given guild from the
    /// database. Returns 50 as a safe default if no record is found.
    /// </summary>
    private int GetVolume(long guildId)
    {
        var dt = new StoredProcedure().Select(Constants.Constants.discordBotConnStr, "GetVolume",
        [
            new SqlParameter("@ServerUID", guildId)
        ]);

        foreach (DataRow row in dt.Rows)
        {
            if (int.TryParse(row["Volume"]?.ToString(), out int v))
            {
                return v;
            }
        }

        return 50;
    }

    /// <summary>
    /// Inserts a record into the database indicating that the bot has connected
    /// to a voice channel, storing the guild, voice channel, text channel, and
    /// the user who triggered the connection.
    /// </summary>
    private void AddPlayerConnected(IVoiceState voiceState)
    {
        new StoredProcedure().UpdateCreate(Constants.Constants.discordBotConnStr, "AddPlayerConnected",
        [
            new SqlParameter("@ServerID", GuildId),
            new SqlParameter("@VoiceChannelID", (long)voiceState.VoiceChannel.Id),
            new SqlParameter("@TextChannelID", (long)((ITextChannel)Context.Channel).Id),
            new SqlParameter("@CreatedBy", Context.User.Id.ToString())
        ]);
    }

    /// <summary>
    /// Removes the connected-player record and clears all queued music entries
    /// for the given guild from the database.
    /// </summary>
    private void DeletePlayerConnected(long serverId)
    {
        var sp = new StoredProcedure();
        SqlParameter[] serverParam = [new SqlParameter("@ServerID", serverId)];
        sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePlayerConnected", [.. serverParam]);
        sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteMusicQueueAll", [.. serverParam]);
    }

    /// <summary>
    /// Records an audit entry in the database for a track that has been queued,
    /// capturing the guild, video identifier, author, title, URL, and the
    /// username of the person who requested it.
    /// </summary>
    private void AddMusicTable(LavalinkTrack? track, string serverId, string createdBy)
    {
        if (track is null)
        {
            return;
        }

        new StoredProcedure().UpdateCreate(Constants.Constants.discordBotConnStr, "AddMusic",
        [
            new SqlParameter("@ServerID", long.Parse(serverId)),
            new SqlParameter("@VideoID", track.Identifier),
            new SqlParameter("@Author", track.Author),
            new SqlParameter("@Title", track.Title),
            new SqlParameter("@URL", track.Uri?.OriginalString ?? ""),
            new SqlParameter("@CreatedBy", createdBy)
        ]);
    }

    #endregion
}