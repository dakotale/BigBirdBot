using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /playlist save — snapshot the current queue to the database.
/// /playlist load — restore a saved playlist into the current queue.
/// /playlist list — list your saved playlists.
/// /playlist delete — remove a saved playlist.
///
/// Playlists are per-user per-server and store the track identifier (URI/URL)
/// that Lavalink originally resolved, allowing them to be re-queued on load.
/// </summary>
[Group("playlist", "Save and load named playlists from the current queue.")]
public sealed class Playlist(IAudioService audioService)
    : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId   => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourOk    = new(88, 101, 242);
    private static readonly Color ColourGreen = new(87, 242, 135);
    private static readonly Color ColourRed   = new(237, 66, 69);
    private static readonly Color ColourGold  = new(255, 215, 0);


    // =========================================================================
    // /playlist save <name>
    // =========================================================================

    [SlashCommand("save", "Save the current queue as a named playlist.")]
    [EnabledInDm(false)]
    public async Task SaveAsync(
        [MinLength(1), MaxLength(64),
         Summary("name", "A name for this playlist, e.g. \"Chill Vibes\"")] string name)
    {
        await DeferAsync(ephemeral: true);

        name = name.Trim();

        var options = new CustomPlayerOptions
        {
            SelfMute = true,
            TextChannel = Context.Channel as ITextChannel
        };

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.None);

        // Fetch current player
        var activePlayers = await audioService.Players.RetrieveAsync<CustomPlayer, CustomPlayerOptions>(Context, CreatePlayerAsync, options, retrieveOptions);
        var player = activePlayers.Player; // May be null if not connected, but we won't create a new one

        if (player is null)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", "I'm not connected to a voice channel — nothing to save.", Username).Build(),
                ephemeral: true);
            return;
        }

        var queue = player.Queue;
        var currentTrack = player.CurrentTrack;

        if (currentTrack is null && queue.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", "The queue is empty — nothing to save.", Username).Build(),
                ephemeral: true);
            return;
        }

        // Collect all tracks: current first, then queued
        var tracks = new List<(string title, string uri)>();

        if (currentTrack is not null && currentTrack.Uri is not null)
            tracks.Add((currentTrack.Title, currentTrack.Uri.ToString()));

        foreach (var item in queue)
        {
            if (item.Track?.Uri is not null)
                tracks.Add((item.Track.Title, item.Track.Uri.ToString()));
        }

        if (tracks.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", "No tracks with resolvable URIs found in the queue.", Username).Build(),
                ephemeral: true);
            return;
        }

        try
        {
            // Delete existing playlist with same name (overwrite semantics)
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePlaylist",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@Name",     name)
            ]);

            // Save each track in order
            int position = 0;
            foreach (var (title, uri) in tracks)
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "SavePlaylistTrack",
                [
                    new SqlParameter("@UserID",    UserId),
                    new SqlParameter("@ServerID",  ServerId),
                    new SqlParameter("@Name",      name),
                    new SqlParameter("@Position",  position++),
                    new SqlParameter("@TrackTitle", title),
                    new SqlParameter("@TrackUri",  uri)
                ]);
            }

            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("💾  Playlist Saved")
                .WithColor(ColourGreen)
                .WithDescription(
                    $"Saved **{tracks.Count} track{(tracks.Count == 1 ? "" : "s")}** as **\"{name}\"**.\n" +
                    $"Use `/playlist load {name}` to restore it.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build(), ephemeral: true);
        }
    }


    // =========================================================================
    // /playlist load <name>
    // =========================================================================

    [SlashCommand("load", "Load a saved playlist into the current queue.")]
    [EnabledInDm(false)]
    public async Task LoadAsync(
        [MinLength(1), MaxLength(64),
         Summary("name", "Name of the playlist to load")] string name)
    {
        await DeferAsync();

        name = name.Trim();

        if (Context.User is not IVoiceState { VoiceChannel: not null })
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", "You must be in a voice channel to load a playlist.", Username).Build());
            return;
        }

        // Load tracks from DB
        DataTable dt;
        try
        {
            dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPlaylistTracks",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@Name",     name)
            ]);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build());
            return;
        }

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist",
                $"No playlist named **\"{name}\"** found. Use `/playlist list` to see your playlists.",
                Username).Build());
            return;
        }

        // Get or create the player (same pattern as Audio.cs / SaveAsync above)
        var playerOptions = new CustomPlayerOptions
        {
            SelfMute = true,
            TextChannel = Context.Channel as ITextChannel
        };
        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.Join);

        var result = await audioService.Players
            .RetrieveAsync<CustomPlayer, CustomPlayerOptions>(
                Context, CreatePlayerAsync, playerOptions, retrieveOptions);

        if (!result.IsSuccess)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", "Could not connect to your voice channel.", Username).Build());
            return;
        }

        var player = result.Player;
        int queued = 0;
        int failed = 0;

        foreach (DataRow row in dt.Rows)
        {
            string uri = row["TrackUri"].ToString()!;
            try
            {
                TrackSearchMode searchMode = TrackSearchMode.None;
                var track = await audioService.Tracks.LoadTrackAsync(uri, searchMode);
                if (track is not null)
                {
                    await player.PlayAsync(track);
                    queued++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Queued **{queued} track{(queued == 1 ? "" : "s")}** from **\"{name}\"**.");
        if (failed > 0)
            sb.AppendLine($"⚠️ {failed} track{(failed == 1 ? "" : "s")} could not be loaded (removed from source?).");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("📂  Playlist Loaded")
            .WithColor(ColourOk)
            .WithDescription(sb.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    // =========================================================================
    // /playlist list
    // =========================================================================

    [SlashCommand("list", "Show all your saved playlists.")]
    [EnabledInDm(false)]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true);

        DataTable dt;
        try
        {
            dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetUserPlaylists",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId)
            ]);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build(), ephemeral: true);
            return;
        }

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist",
                "You have no saved playlists. Use `/playlist save <name>` to create one.",
                Username).Build(), ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        foreach (DataRow row in dt.Rows)
        {
            string pName   = row["Name"].ToString()!;
            int trackCount = int.Parse(row["TrackCount"].ToString()!);
            sb.AppendLine($"📀 **{pName}** — {trackCount} track{(trackCount == 1 ? "" : "s")}");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎶  {Username}'s Playlists")
            .WithColor(ColourGold)
            .WithDescription(sb.ToString())
            .WithFooter("Use /playlist load <name> to queue one up", AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);
    }


    // =========================================================================
    // /playlist delete <name>
    // =========================================================================

    [SlashCommand("delete", "Delete one of your saved playlists.")]
    [EnabledInDm(false)]
    public async Task DeleteAsync(
        [MinLength(1), MaxLength(64),
         Summary("name", "Name of the playlist to delete")] string name)
    {
        await DeferAsync(ephemeral: true);

        name = name.Trim();

        try
        {
            // Check it exists first
            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetUserPlaylists",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId)
            ]);

            bool exists = dt.Rows.Cast<DataRow>()
                .Any(r => string.Equals(r["Name"].ToString(), name, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Playlist",
                    $"No playlist named **\"{name}\"** found.",
                    Username).Build(), ephemeral: true);
                return;
            }

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePlaylist",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@Name",     name)
            ]);

            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🗑️  Playlist Deleted")
                .WithColor(ColourRed)
                .WithDescription($"Playlist **\"{name}\"** has been deleted.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build(), ephemeral: true);
        }
    }

    private static ValueTask<CustomPlayer> CreatePlayerAsync(
        IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);
        return ValueTask.FromResult(new CustomPlayer(properties));
    }
}
