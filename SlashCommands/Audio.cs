using System.Runtime.CompilerServices;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
/// Supports interactive button controls on Now Playing embeds.
/// </summary>
public sealed class Audio(IAudioService audioService, InteractiveService interactiveService, MusicService music)
    : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private const string EmojiMusic = "🎵";
    private const string EmojiPlay = "▶️";
    private const string EmojiPause = "⏸️";
    private const string EmojiStop = "⏹️";
    private const string EmojiSkip = "⏭️";
    private const string EmojiVolume = "🔊";
    private const string EmojiVolDown = "🔉";
    private const string EmojiQueue = "📋";
    private const string EmojiLoop = "🔁";
    private const string EmojiShuffle = "🔀";
    private const string EmojiJoin = "👋";
    private const string EmojiLeave = "🚪";
    private const string EmojiSeek = "⏩";
    private const string EmojiError = "❌";
    private const string EmojiSuccess = "✅";

    private static readonly Color ColourDefault = EmbedColors.Blue;
    private static readonly Color ColourSuccess = EmbedColors.Green;
    private static readonly Color ColourError = EmbedColors.Red;
    private static readonly Color ColourWarning = EmbedColors.Yellow;

    private const string BtnPause = "audio:pause";
    private const string BtnResume = "audio:resume";
    private const string BtnSkip = "audio:skip";
    private const string BtnStop = "audio:stop";
    private const string BtnShuffle = "audio:shuffle";
    private const string BtnVolUp = "audio:vol_up";
    private const string BtnVolDown = "audio:vol_down";
    private const string BtnQueueB = "audio:queue";
    private const string BtnLoop1 = "audio:loop1";


    /// <summary>Typed guild ID — avoids repeated casts throughout the class.</summary>
    private long GuildId => (long)Context.Guild.Id;

    // =========================================================================
    // Slash Commands
    // =========================================================================

    #region Slash Commands

    /// <summary>Joins the voice channel the invoking user is currently in.</summary>
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
        await AddPlayerConnectedAsync(voiceState);

        var player = await GetPlayerAsync(connectToVoiceChannel: true);

        if (player is null || player.ConnectionState.IsConnected)
        {
            await ReplyEmbedAsync(EmojiJoin, "Join", "I'm already connected to a voice channel!", ColourWarning);
            return;
        }

        int vol = await GetVolumeAsync(GuildId);
        var embed = MakeEmbed(EmojiJoin, "Joined", ColourSuccess)
            .WithDescription($"Ready to play! Current volume is **{vol}%**.")
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>Disconnects the bot from its current voice channel.</summary>
    [SlashCommand("leave", "Leaves the voice channel.")]
    public async Task LeaveAsync()
    {
        await DeferAsync();

        var connected = await audioService.Players.GetPlayerAsync(Context.Guild);
        if (connected is not null)
            await connected.DisconnectAsync();
        else if (await GetPlayerAsync() is null)
            return;

        await DeletePlayerConnectedAsync(GuildId);
        await FollowupAsync(embed: LeaveEmbed().Build());
    }

    /// <summary>
    /// Plays a track or playlist from YouTube, Spotify, SoundCloud, Twitch, or Twitter/X.
    /// Automatically joins the user's voice channel if the bot is not already connected.
    /// </summary>
    [SlashCommand("play", "Play a track or playlist from YouTube, Spotify, SoundCloud, etc.", runMode: RunMode.Async)]
    public async Task PlayAsync([MinLength(1)] string query)
    {
        await DeferAsync();
        query = HandleTwitter(query);

        var player = (await audioService.Players.RetrieveAsync(Context)).Player
                     ?? await EnsureJoinedAsync();

        if (player is null) return;

        await QueueAndPlayAsync(player, query, playNext: false);
    }

    /// <summary>Identical to <c>/play</c> but inserts the track immediately after the current one.</summary>
    [SlashCommand("playnext", "Same as /play but inserts the track next in queue.", runMode: RunMode.Async)]
    public async Task PlayNextAsync([MinLength(1)] string query)
    {
        await DeferAsync();
        query = HandleTwitter(query);

        var player = (await audioService.Players.RetrieveAsync(Context)).Player
                     ?? await EnsureJoinedAsync();

        if (player is null) return;

        await QueueAndPlayAsync(player, query, playNext: true);
    }

    /// <summary>Immediately skips the currently playing track.</summary>
    [SlashCommand("forceskip", "Skips the current track.")]
    public async Task ForceSkipAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiSkip, "Skip", "Nothing is playing right now.", ColourWarning);
            return;
        }

        await player.SkipAsync();

        await (player.CurrentItem is { } next
            ? ReplyEmbedAsync(EmojiSkip, "Skipped", $"Now playing: **{next.Track.Title}**", ColourSuccess)
            : ReplyEmbedAsync(EmojiSkip, "Skipped", "Queue is now empty.", ColourDefault));
    }

    /// <summary>Resumes a paused track.</summary>
    [SlashCommand("resume", "Resumes the paused track.")]
    public async Task ResumeAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State is not PlayerState.Paused)
        {
            await ReplyEmbedAsync(EmojiPlay, "Resume", "The player isn't paused.", ColourWarning);
            return;
        }

        await player.ResumeAsync();
        await ReplyNowPlayingAsync(player, paused: false);
    }

    /// <summary>Pauses the currently playing track.</summary>
    [SlashCommand("pause", "Pauses the current track.")]
    public async Task PauseAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State is PlayerState.Paused)
        {
            await ReplyEmbedAsync(EmojiPause, "Pause", "Already paused.", ColourWarning);
            return;
        }

        await player.PauseAsync();
        await ReplyNowPlayingAsync(player, paused: true);
    }

    /// <summary>Stops playback, clears the queue, and disconnects.</summary>
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
            if (player is null) return;

            if (player.CurrentItem is null)
            {
                await ReplyEmbedAsync(EmojiStop, "Stop", "Nothing is playing.", ColourWarning);
                return;
            }

            await player.StopAsync();
            await player.DisconnectAsync();
        }

        await DeletePlayerConnectedAsync(GuildId);
        await FollowupAsync(embed: LeaveEmbed().Build());
    }

    /// <summary>Sets the playback volume (0–100) and persists it to the database.</summary>
    [SlashCommand("volume", "Set playback volume (0–100).")]
    public async Task VolumeAsync([MinValue(0), MaxValue(100)] int volume)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        await player.SetVolumeAsync(volume / 100f);

        await music.UpdateVolumeAsync((ulong)GuildId, volume);

        var embed = MakeEmbed(EmojiVolume, "Volume", ColourSuccess)
            .AddField("Level", $"{VolumeBar(volume)}  **{volume}%**");

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>Displays an embed for the currently playing track, with playback buttons.</summary>
    [SlashCommand("nowplaying", "Shows the currently playing track.")]
    public async Task NowPlayingAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State is not PlayerState.Playing)
        {
            await ReplyEmbedAsync(EmojiPlay, "Now Playing", "Nothing is playing right now.", ColourWarning);
            return;
        }

        await FollowupAsync(
            embed: BuildNowPlayingEmbed(player.CurrentTrack!, player.Queue.Count).Build(),
            components: BuildPlaybackButtons(paused: false));
    }

    /// <summary>
    /// Shows the queue as paginated embeds (10 tracks per page).
    /// Uses plain follow-up messages instead of the Fergun.Interactive paginator
    /// to avoid a SelectMenuBuilder constructor mismatch with the current Discord.Net build.
    /// </summary>
    [SlashCommand("queue", "Shows the upcoming tracks.")]
    public async Task GetQueueAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State != PlayerState.Playing)
        {
            await ReplyEmbedAsync(EmojiQueue, "Queue", "Nothing is playing right now.", ColourWarning);
            return;
        }

        var current = player.CurrentTrack!;

        if (player.Queue.Count == 0)
        {
            await FollowupAsync(
                embed: BuildNowPlayingEmbed(current, 0).Build(),
                components: BuildPlaybackButtons(paused: false));
            return;
        }

        // Split into pages of 10 tracks
        var pages = new List<string>();
        var sb = new System.Text.StringBuilder();
        int i = 0;
        int total = player.Queue.Count;

        foreach (var item in player.Queue)
        {
            i++;
            sb.AppendLine($"`{i:00}.` [{item.Track.Title}]({item.Track.Uri}) — `{item.Track.Duration:hh\\:mm\\:ss}`");

            if (i % 10 == 0 || i == total)
            {
                pages.Add(sb.ToString());
                sb.Clear();
            }
        }

        int pageCount = pages.Count;

        // First page is the deferred follow-up
        await FollowupAsync(embed: BuildQueuePageEmbed(pages[0], total, current, page: 1, pageCount).Build());

        // Additional pages as separate follow-ups (Discord allows up to 5 total)
        for (int p = 1; p < Math.Min(pageCount, 5); p++)
            await FollowupAsync(embed: BuildQueuePageEmbed(pages[p], total, current, page: p + 1, pageCount).Build());
    }

    /// <summary>Re-queues the current track N additional times.</summary>
    [SlashCommand("loop", "Queues the current track N more times.")]
    public async Task LoopAsync([MinValue(1)] int times)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiLoop, "Loop", "Nothing is playing to loop.", ColourWarning);
            return;
        }

        var track = player.CurrentTrack!;

        for (int i = 0; i < times; i++)
            await player.PlayAsync(track);

        await ReplyEmbedAsync(EmojiLoop, "Loop",
            $"**{track.Title}** will repeat **{times}** more time(s).", ColourSuccess);
    }

    /// <summary>Re-queues the current track one additional time.</summary>
    [SlashCommand("repeat", "Queues the current track one more time.")]
    public async Task RepeatAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentItem is null)
        {
            await ReplyEmbedAsync(EmojiLoop, "Repeat", "Nothing is playing to repeat.", ColourWarning);
            return;
        }

        var track = player.CurrentTrack!;
        await player.PlayAsync(track);
        await ReplyEmbedAsync(EmojiLoop, "Repeat", $"**{track.Title}** added to queue again.", ColourSuccess);
    }

    /// <summary>Swaps two tracks in the queue by their 0-based index positions.</summary>
    [SlashCommand("swap", "Swaps two tracks in the queue by position.")]
    public async Task SwapAsync([MinValue(0)] int posA, [MinValue(0)] int posB)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        try
        {
            var list = player.Queue.ToList();
            (list[posA], list[posB]) = (list[posB], list[posA]);

            await player.Queue.ClearAsync();
            foreach (var item in list)
                await player.Queue.AddAsync(item);

            await ReplyEmbedAsync(EmojiSuccess, "Swap",
                $"Swapped **#{posA + 1}** and **#{posB + 1}** in the queue.", ColourSuccess);
        }
        catch
        {
            await ReplyEmbedAsync(EmojiError, "Swap Failed",
                "One or both positions don't exist in the queue.", ColourError);
        }
    }

    /// <summary>Randomly shuffles all queued tracks.</summary>
    [SlashCommand("shuffle", "Randomises the queue.")]
    public async Task ShuffleAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.Queue.Count < 2)
        {
            await ReplyEmbedAsync(EmojiShuffle, "Shuffle",
                "Need at least 2 tracks in the queue to shuffle.", ColourWarning);
            return;
        }

        await player.Queue.ShuffleAsync();
        await ReplyEmbedAsync(EmojiShuffle, "Shuffled",
            $"**{player.Queue.Count}** tracks shuffled.", ColourSuccess);
    }

    /// <summary>Clears all queued tracks and removes them from the database.</summary>
    [SlashCommand("clear", "Removes all tracks from the queue.")]
    public async Task ClearQueueAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.Queue.Count < 1)
        {
            await ReplyEmbedAsync(EmojiQueue, "Clear", "The queue is already empty.", ColourWarning);
            return;
        }

        int count = player.Queue.Count;
        await player.Queue.ClearAsync();

        await music.ClearQueueAsync((ulong)GuildId);

        await ReplyEmbedAsync(EmojiQueue, "Queue Cleared", $"Removed **{count}** track(s).", ColourSuccess);
    }

    /// <summary>Removes a specific track from the queue at a 1-based position.</summary>
    [SlashCommand("remove", "Removes a specific track from the queue by position.")]
    public async Task RemoveAsync([MinValue(1)] int position)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

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
            await ReplyEmbedAsync(EmojiError, "Remove Failed",
                $"No track at position **#{position}**.", ColourError);
        }
    }

    /// <summary>Seeks to a timestamp (hh:mm:ss) within the current track.</summary>
    [SlashCommand("seek", "Jumps to a timestamp in the current track (e.g. 00:01:30).")]
    public async Task SeekAsync([MinLength(1)] string timestamp)
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

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

    // =========================================================================
    // Button Component Interactions
    // =========================================================================

    #region Button Interactions

    /// <summary>Pause button handler — pauses playback and refreshes the Now Playing message in place.</summary>
    [ComponentInteraction(BtnPause)]
    public async Task OnPauseButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State is PlayerState.Paused)
        {
            await RespondWithWarningUpdateAsync("Already paused.");
            return;
        }

        await player.PauseAsync();
        await UpdateNowPlayingMessageAsync(player, paused: true);
    }

    /// <summary>Resume button handler — resumes playback and refreshes the Now Playing message in place.</summary>
    [ComponentInteraction(BtnResume)]
    public async Task OnResumeButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.State is not PlayerState.Paused)
        {
            await RespondWithWarningUpdateAsync("The player isn't paused.");
            return;
        }

        await player.ResumeAsync();
        await UpdateNowPlayingMessageAsync(player, paused: false);
    }

    /// <summary>Skip button handler — skips the current track and refreshes the Now Playing message, or shows an empty-queue message if nothing follows.</summary>
    [ComponentInteraction(BtnSkip)]
    public async Task OnSkipButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentItem is null)
        {
            await RespondWithWarningUpdateAsync("Nothing is playing right now.");
            return;
        }

        await player.SkipAsync();

        if (player.CurrentItem is { } next)
            await UpdateNowPlayingMessageAsync(player, paused: false);
        else
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = MakeEmbed(EmojiSkip, "Skipped", ColourDefault)
                                   .WithDescription("Queue is now empty.")
                                   .Build();
                m.Components = new ComponentBuilder().Build();
            });
    }

    /// <summary>Stop button handler — stops playback, disconnects, and swaps the message to the Disconnected embed with no buttons.</summary>
    [ComponentInteraction(BtnStop)]
    public async Task OnStopButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        await player.StopAsync();
        await player.DisconnectAsync();
        await DeletePlayerConnectedAsync(GuildId);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = LeaveEmbed().Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    /// <summary>Shuffle button handler — shuffles the queue and refreshes the Now Playing message in place.</summary>
    [ComponentInteraction(BtnShuffle)]
    public async Task OnShuffleButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.Queue.Count < 2)
        {
            await RespondWithWarningUpdateAsync("Need at least 2 tracks queued to shuffle.");
            return;
        }

        await player.Queue.ShuffleAsync();
        await UpdateNowPlayingMessageAsync(player, player.State is PlayerState.Paused);
    }

    /// <summary>Loop ×1 button handler — re-queues the current track once and refreshes the Now Playing message in place.</summary>
    [ComponentInteraction(BtnLoop1)]
    public async Task OnLoop1ButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentTrack is { } track)
            await player.PlayAsync(track);

        await UpdateNowPlayingMessageAsync(player, player.State is PlayerState.Paused);
    }

    /// <summary>Volume-up button handler — raises volume by 10%.</summary>
    [ComponentInteraction(BtnVolUp)]
    public async Task OnVolUpButtonAsync()
    {
        await DeferAsync();
        await AdjustVolumeAsync(delta: +10);
    }

    /// <summary>Volume-down button handler — lowers volume by 10%.</summary>
    [ComponentInteraction(BtnVolDown)]
    public async Task OnVolDownButtonAsync()
    {
        await DeferAsync();
        await AdjustVolumeAsync(delta: -10);
    }

    /// <summary>Queue button handler — shows the first 10 upcoming tracks ephemerally.</summary>
    [ComponentInteraction(BtnQueueB)]
    public async Task OnQueueButtonAsync()
    {
        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        if (player.Queue.Count == 0)
        {
            await FollowupAsync(
                embed: BuildNowPlayingEmbed(player.CurrentTrack!, 0).Build(),
                components: BuildPlaybackButtons(player.State is PlayerState.Paused),
                ephemeral: true);
            return;
        }

        var sb = new System.Text.StringBuilder();
        int i = 0;

        foreach (var item in player.Queue.Take(10))
        {
            i++;
            sb.AppendLine($"`{i:00}.` [{item.Track.Title}]({item.Track.Uri}) — `{item.Track.Duration:hh\\:mm\\:ss}`");
        }

        if (player.Queue.Count > 10)
            sb.AppendLine($"*… and {player.Queue.Count - 10} more. Use `/queue` for full list.*");

        var embed = MakeEmbed(EmojiQueue, $"Queue — {player.Queue.Count} track(s)", ColourDefault)
            .WithDescription($"**Now playing:** {player.CurrentTrack!.Title}\n\n{sb}");

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    #endregion

    #region Private Helpers

    /// <summary>Retrieves (optionally creating/joining) the guild's Lavalink player, replying with a friendly error and returning null if the user or bot isn't in a voice channel.</summary>
    private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var options = new CustomPlayerOptions
        {
            SelfMute = true,
            TextChannel = Context.Channel as ITextChannel,
            MusicService = music
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

    /// <summary>Joins the invoking user's voice channel if not already connected, then returns the player.</summary>
    private async Task<LavalinkPlayer?> EnsureJoinedAsync()
    {
        if (Context.User is not IVoiceState { VoiceChannel: not null } voiceState)
        {
            await ReplyEmbedAsync(EmojiError, "Error", "You must be in a voice channel.", ColourError);
            return null;
        }

        await audioService.StartAsync();
        await Task.Delay(3_000);
        await AddPlayerConnectedAsync(voiceState);
        return await GetPlayerAsync(connectToVoiceChannel: true);
    }

    /// <summary>Resolves a search query or URL to one or more tracks via Lavalink and dispatches to the single-track or playlist queuing path.</summary>
    private async Task QueueAndPlayAsync(LavalinkPlayer player, string query, bool playNext)
    {
        var tracks = await audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

        if (tracks.IsFailed)
        {
            await ReplyEmbedAsync(EmojiError, "Not Found", $"No results for `{query}`.", ColourError);
            return;
        }

        bool isPlaylist = Uri.IsWellFormedUriString(query, UriKind.Absolute) && tracks.Count > 1;

        await (isPlaylist
            ? PlayMultipleTracksAsync(player, tracks, playNext)
            : PlaySingleTrackAsync(player, tracks.Track, playNext));
    }

    /// <summary>Queues a single resolved track (or inserts it next), applies the guild's saved volume, records it in the music history table, and replies with a track-detail embed.</summary>
    private async Task PlaySingleTrackAsync(LavalinkPlayer player, LavalinkTrack track, bool playNext)
    {
        await AddMusicTableAsync(track, Context.Guild.Id.ToString(), Context.User.Username);

        string artist = ExtractAdditionalInfo(track, "artistUrl");
        string albumName = ExtractAdditionalInfo(track, "albumName");

        if (playNext)
            await InsertAsNextAsync(player, singleTrack: track);
        else
            await player.PlayAsync(track);

        float volume = await GetVolumeAsync(GuildId) / 100f;
        await player.SetVolumeAsync(volume);

        var queueCount = (await GetPlayerAsync(connectToVoiceChannel: false))?.Queue.Count ?? 0;
        string displayArt = string.IsNullOrWhiteSpace(artist) ? track.Author : artist;

        var embed = MakeEmbed(EmojiPlay, playNext ? "Playing Next" : "Added to Queue", ColourSuccess)
            .WithThumbnailUrl(track.ArtworkUri?.ToString())
            .AddField("Track", $"[{track.Title}]({track.Uri})", inline: false)
            .AddField("Artist", displayArt, inline: true)
            .AddField("Duration", $"`{track.Duration:hh\\:mm\\:ss}`", inline: true)
            .AddField("Source", track.SourceName.ToUpperInvariant(), inline: true);

        if (!string.IsNullOrWhiteSpace(albumName))
            embed.AddField("Album", albumName, inline: true);

        embed.AddField("Volume", $"{volume * 100:0}%", inline: true)
             .AddField("In Queue", $"{queueCount}", inline: true);

        await FollowupAsync(
            embed: embed.Build(),
            components: BuildPlaybackButtons(paused: false));
    }

    /// <summary>
    /// Queues all tracks from a resolved playlist.
    /// All embed field values are guarded against null/empty before use —
    /// Discord rejects blank field values with ArgumentException.
    /// </summary>
    private async Task PlayMultipleTracksAsync(LavalinkPlayer player, TrackLoadResult tracks, bool playNext)
    {
        // Default every value up front; only overwrite when the source is non-blank.
        string playlistName = "Unknown Playlist";
        string totalTracks = tracks.Count.ToString();
        string artworkUrl = "";
        string playlistUrl = "";

        if (tracks.Playlist is { } playlist)
        {
            if (!string.IsNullOrWhiteSpace(playlist.Name))
                playlistName = playlist.Name;

            foreach (var (key, value) in playlist.AdditionalInformation)
            {
                string str = value.ToString();
                switch (key)
                {
                    case "totalTracks" when !string.IsNullOrWhiteSpace(str):
                        totalTracks = str; break;
                    case "artworkUrl" when !string.IsNullOrWhiteSpace(str):
                        artworkUrl = str; break;
                    case "url" when !string.IsNullOrWhiteSpace(str):
                        playlistUrl = str; break;
                }
            }
        }

        string guildIdStr = Context.Guild.Id.ToString();
        string userName = Context.User.Username;

        if (playNext)
            await InsertPlaylistAsNextAsync(player, tracks, guildIdStr, userName);
        else
            await EnqueueAllAsync(player, tracks, guildIdStr, userName);

        float volume = await GetVolumeAsync(GuildId) / 100f;
        await player.SetVolumeAsync(volume);

        var embed = MakeEmbed(EmojiPlay, playNext ? "Playlist — Playing Next" : "Playlist Added", ColourSuccess);

        if (!string.IsNullOrWhiteSpace(artworkUrl))
            embed.WithThumbnailUrl(artworkUrl);

        if (!string.IsNullOrWhiteSpace(playlistUrl))
            embed.WithUrl(playlistUrl);

        embed.AddField("Playlist", playlistName, inline: false)
             .AddField("Tracks", totalTracks, inline: true)
             .AddField("Volume", $"{volume * 100:0}%", inline: true);

        await FollowupAsync(
            embed: embed.Build(),
            components: BuildPlaybackButtons(paused: false));
    }


    /// <summary>Inserts a single track immediately after the currently playing one by temporarily draining and restoring the rest of the queue.</summary>
    private async Task InsertAsNextAsync(LavalinkPlayer player, LavalinkTrack singleTrack)
    {
        var queued = await GetPlayerAsync(connectToVoiceChannel: false);

        if (queued?.Queue.Count > 0)
        {
            var saved = queued.Queue.ToList();
            await queued.Queue.ClearAsync();
            await player.PlayAsync(singleTrack);
            foreach (var item in saved)
                await queued.Queue.AddAsync(item);
        }
        else
        {
            await player.PlayAsync(singleTrack);
        }
    }

    /// <summary>Inserts an entire resolved playlist immediately after the currently playing track by temporarily draining and restoring the rest of the queue.</summary>
    private async Task InsertPlaylistAsNextAsync(
        LavalinkPlayer player, TrackLoadResult tracks, string guildIdStr, string userName)
    {
        var queued = await GetPlayerAsync(connectToVoiceChannel: false);

        if (queued?.Queue.Count > 0)
        {
            var saved = queued.Queue.ToList();
            await queued.Queue.ClearAsync();
            await EnqueueAllAsync(player, tracks, guildIdStr, userName);
            foreach (var item in saved)
                await queued.Queue.AddAsync(item);
        }
        else
        {
            await EnqueueAllAsync(player, tracks, guildIdStr, userName);
        }
    }

    /// <summary>Queues every track in a resolved playlist and records each one in the music history table.</summary>
    private async Task EnqueueAllAsync(
        LavalinkPlayer player, TrackLoadResult tracks, string guildIdStr, string userName)
    {
        foreach (var t in tracks.Tracks)
        {
            await player.PlayAsync(t);
            await AddMusicTableAsync(t, guildIdStr, userName);
        }
    }


    /// <summary>Nudges the guild's volume by a delta (clamped 0–100), persists it, and refreshes the Now Playing message.</summary>
    private async Task AdjustVolumeAsync(int delta)
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        int current = await GetVolumeAsync(GuildId);
        int newVol = Math.Clamp(current + delta, 0, 100);

        await player.SetVolumeAsync(newVol / 100f);

        await music.UpdateVolumeAsync((ulong)GuildId, newVol);

        await UpdateNowPlayingMessageAsync(player, player.State is PlayerState.Paused);
    }


    /// <summary>Posts a fresh Now Playing embed with playback buttons as the interaction followup.</summary>
    private async Task ReplyNowPlayingAsync(QueuedLavalinkPlayer player, bool paused)
    {
        if (player.CurrentTrack is null) return;

        await FollowupAsync(
            embed: BuildNowPlayingEmbed(player.CurrentTrack, player.Queue.Count).Build(),
            components: BuildPlaybackButtons(paused));
    }

    /// <summary>Replaces the original response with an updated Now Playing embed and playback buttons — used by button handlers to refresh in place.</summary>
    private async Task UpdateNowPlayingMessageAsync(LavalinkPlayer player, bool paused)
    {
        if (player.CurrentTrack is null) return;

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildNowPlayingEmbed(player.CurrentTrack, 0).Build();
            m.Components = BuildPlaybackButtons(paused);
        });
    }

    /// <summary>Replaces the original response with a plain warning embed — used when a button action can't be completed.</summary>
    private async Task RespondWithWarningUpdateAsync(string message)
    {
        await ModifyOriginalResponseAsync(m =>
            m.Embed = MakeEmbed(EmojiError, "Warning", ColourWarning)
                          .WithDescription(message)
                          .Build());
    }


    /// <summary>Rewrites twitter.com/x.com URLs to their dl.fxtwitter.com equivalent so Lavalink can resolve them.</summary>
    private static string HandleTwitter(string query) =>
        query switch
        {
            _ when query.Contains("https://twitter.com") => query.Replace("twitter", "dl.fxtwitter"),
            _ when query.Contains("https://x.com") => query.Replace("x.com", "dl.fxtwitter.com"),
            _ => query
        };

    /// <summary>Reads one key out of a track's AdditionalInformation dictionary, or an empty string if absent.</summary>
    private static string ExtractAdditionalInfo(LavalinkTrack track, string key) =>
        track.AdditionalInformation is { Count: > 0 } info && info.TryGetValue(key, out var val)
            ? val.ToString() ?? ""
            : "";

    /// <summary>Factory delegate passed to Lavalink4NET's player-retrieval API to construct a new <see cref="CustomPlayer"/> when one doesn't already exist.</summary>
    private static ValueTask<CustomPlayer> CreatePlayerAsync(
        IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);
        return ValueTask.FromResult(new CustomPlayer(properties));
    }

    #endregion

    // =========================================================================
    // Embed / Component Factories
    // =========================================================================

    #region Embed & Component Factories

    /// <summary>Builds the shared base embed (emoji+title, color, requester footer) used by every audio command reply.</summary>
    private EmbedBuilder MakeEmbed(string emoji, string title, Color color) =>
        _embed.BuildSimpleEmbed(
            $"{emoji}  {title}", "", color,
            footer: $"Requested by {Context.User.Username}", footerIconUrl: Context.User.GetAvatarUrl());

    /// <summary>Posts a simple emoji/title/description embed as the interaction followup — the common case for command replies that aren't Now Playing.</summary>
    private async Task ReplyEmbedAsync(string emoji, string title, string description, Color color) =>
        await FollowupAsync(embed: MakeEmbed(emoji, title, color).WithDescription(description).Build());

    /// <summary>Builds the Now Playing embed: artwork thumbnail, title/artist/duration/source fields, and remaining queue count.</summary>
    private EmbedBuilder BuildNowPlayingEmbed(LavalinkTrack track, int queueRemaining) =>
        MakeEmbed(EmojiPlay, "Now Playing", ColourDefault)
            .WithThumbnailUrl(track.ArtworkUri?.ToString())
            .WithDescription($"### [{track.Title}]({track.Uri})")
            .AddField("Artist", track.Author, inline: true)
            .AddField("Duration", $"`{track.Duration:hh\\:mm\\:ss}`", inline: true)
            .AddField("Source", track.SourceName.ToUpperInvariant(), inline: true)
            .AddField("Up Next", $"{queueRemaining} track(s)", inline: true);

    /// <summary>
    /// Builds a single-page queue embed. Does NOT use PageBuilder — the Fergun.Interactive
    /// paginator is avoided due to a SelectMenuBuilder constructor mismatch with
    /// the current Discord.Net build.
    /// </summary>
    private EmbedBuilder BuildQueuePageEmbed(
        string content, int total, LavalinkTrack current, int page, int pageCount)
    {
        string title = pageCount > 1
            ? $"{EmojiQueue}  Queue  —  {total} track(s)  (Page {page}/{pageCount})"
            : $"{EmojiQueue}  Queue  —  {total} track(s)";

        return _embed.BuildSimpleEmbed(
            title, $"**Now playing:** {current.Title}\n\n{content}", ColourDefault,
            footer: $"Requested by {Context.User.Username}", footerIconUrl: Context.User.GetAvatarUrl());
    }

    /// <summary>Builds the two-row playback control button set (Pause/Resume, Skip, Stop, Shuffle, Loop, Volume, Queue), swapping Pause for Resume when paused.</summary>
    private static MessageComponent BuildPlaybackButtons(bool paused) =>
        new ComponentBuilder()
            .WithButton(
                paused ? "Resume" : "Pause",
                paused ? BtnResume : BtnPause,
                paused ? ButtonStyle.Success : ButtonStyle.Primary,
                new Emoji(paused ? EmojiPlay : EmojiPause), row: 0)
            .WithButton("Skip", BtnSkip, ButtonStyle.Secondary, new Emoji(EmojiSkip), row: 0)
            .WithButton("Stop", BtnStop, ButtonStyle.Danger, new Emoji(EmojiStop), row: 0)
            .WithButton("Shuffle", BtnShuffle, ButtonStyle.Secondary, new Emoji(EmojiShuffle), row: 0)
            .WithButton("Loop ×1", BtnLoop1, ButtonStyle.Secondary, new Emoji(EmojiLoop), row: 0)
            .WithButton("Vol −", BtnVolDown, ButtonStyle.Secondary, new Emoji(EmojiVolDown), row: 1)
            .WithButton("Vol +", BtnVolUp, ButtonStyle.Secondary, new Emoji(EmojiVolume), row: 1)
            .WithButton("Queue", BtnQueueB, ButtonStyle.Secondary, new Emoji(EmojiQueue), row: 1)
            .Build();

    /// <summary>Builds the shared "disconnected" embed posted on /leave, /stop, and the Stop button.</summary>
    private EmbedBuilder LeaveEmbed() =>
        MakeEmbed(EmojiLeave, "Disconnected", ColourDefault)
            .WithDescription("Goodbye! Have a great time. 👋");

    /// <summary>Renders a 10-segment block-character bar representing a 0–100 volume level.</summary>
    private static string VolumeBar(int volume)
    {
        int filled = volume / 10;
        return string.Create(10, filled, static (span, f) =>
        {
            span.Fill('░');
            span[..f].Fill('█');
        });
    }

    #endregion

    // =========================================================================
    // Database Helpers
    // =========================================================================

    #region DB Helpers

    /// <summary>Reads the guild's saved playback volume, defaulting to 50 if none is stored.</summary>
    private async Task<int> GetVolumeAsync(long guildId) =>
        await music.GetVolumeAsync((ulong)guildId) ?? 50;

    /// <summary>Records that the bot has connected to a voice/text channel pair in this guild.</summary>
    private Task AddPlayerConnectedAsync(IVoiceState voiceState) =>
        music.AddPlayerConnectedAsync(
            (ulong)GuildId,
            voiceState.VoiceChannel.Id,
            ((ITextChannel)Context.Channel).Id,
            Context.User.Id.ToString());

    /// <summary>Clears the guild's connected-player record and any leftover queued-track rows.</summary>
    private async Task DeletePlayerConnectedAsync(long serverId)
    {
        await music.DeletePlayerConnectedAsync((ulong)serverId);
        await music.ClearQueueAsync((ulong)serverId);
    }

    /// <summary>Logs a played track to the music history table.</summary>
    private async Task AddMusicTableAsync(LavalinkTrack? track, string serverId, string createdBy)
    {
        if (track is null) return;

        await music.AddMusicAsync(
            ulong.Parse(serverId), track.Identifier, track.Author, track.Title,
            track.Uri?.OriginalString ?? "", createdBy);
    }

    #endregion
}
