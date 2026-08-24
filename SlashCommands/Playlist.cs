using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.EntityFrameworkCore;
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
public sealed class Playlist(IAudioService audioService, DiscordbotContext db, IServiceProvider services)
    : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId   => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourOk    = EmbedColors.Blue;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourRed   = EmbedColors.Red;
    private static readonly Color ColourGold  = EmbedColors.Gold;


    // =========================================================================
    // /playlist save <name>
    // =========================================================================

    /// <summary>Snapshots the currently-playing track plus the rest of the queue (by URI) into a named playlist, overwriting any existing playlist with the same name.</summary>
    [SlashCommand("save", "Save the current queue as a named playlist.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task SaveAsync(
        [MinLength(1), MaxLength(64),
         Summary("name", "A name for this playlist, e.g. \"Chill Vibes\"")] string name)
    {
        await DeferAsync(ephemeral: true);

        name = name.Trim();

        var options = new CustomPlayerOptions
        {
            SelfMute = true,
            TextChannel = Context.Channel as ITextChannel,
            Services = services
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
            // Delete existing playlist with same name (overwrite semantics), then save each
            // track in order — staged as one set of changes, saved together.
            db.PlaylistTracks.RemoveRange(db.PlaylistTracks
                .Where(p => p.UserId == UserId && p.ServerId == ServerId && EF.Functions.ILike(p.Name, name)));

            int position = 0;
            foreach (var (title, uri) in tracks)
            {
                db.PlaylistTracks.Add(new PlaylistTrack
                {
                    UserId = UserId,
                    ServerId = ServerId,
                    Name = name,
                    Position = position++,
                    TrackTitle = title,
                    TrackUri = uri
                });
            }

            await db.SaveChangesAsync();

            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "💾  Playlist Saved",
                $"Saved **{tracks.Count} track{(tracks.Count == 1 ? "" : "s")}** as **\"{name}\"**.\n" +
                $"Use `/playlist load {name}` to restore it.",
                ColourGreen, footer: Username, footerIconUrl: AvatarUrl).Build(), ephemeral: true);
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

    /// <summary>Joins the caller's voice channel (if needed) and re-resolves + queues every saved track URI from a named playlist, reporting any tracks that failed to load.</summary>
    [SlashCommand("load", "Load a saved playlist into the current queue.")]
    [CommandContextType(InteractionContextType.Guild)]
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
        List<PlaylistTrack> tracksToLoad;
        try
        {
            tracksToLoad = await db.PlaylistTracks.AsNoTracking()
                .Where(p => p.UserId == UserId && p.ServerId == ServerId && EF.Functions.ILike(p.Name, name))
                .OrderBy(p => p.Position)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build());
            return;
        }

        if (tracksToLoad.Count == 0)
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
            TextChannel = Context.Channel as ITextChannel,
            Services = services
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

        foreach (var row in tracksToLoad)
        {
            string uri = row.TrackUri;
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

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "📂  Playlist Loaded", sb.ToString(), ColourOk, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    // =========================================================================
    // /playlist list
    // =========================================================================

    /// <summary>Lists the user's saved playlists for this server with each one's track count.</summary>
    [SlashCommand("list", "Show all your saved playlists.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true);

        List<(string Name, int TrackCount)> playlists;
        try
        {
            playlists = await db.PlaylistTracks.AsNoTracking()
                .Where(p => p.UserId == UserId && p.ServerId == ServerId)
                .GroupBy(p => p.Name)
                .Select(g => new ValueTuple<string, int>(g.Key, g.Count()))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build(), ephemeral: true);
            return;
        }

        if (playlists.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist",
                "You have no saved playlists. Use `/playlist save <name>` to create one.",
                Username).Build(), ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        foreach (var (pName, trackCount) in playlists)
            sb.AppendLine($"📀 **{pName}** — {trackCount} track{(trackCount == 1 ? "" : "s")}");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🎶  {Username}'s Playlists", sb.ToString(), ColourGold,
            footer: "Use /playlist load <name> to queue one up", footerIconUrl: AvatarUrl).Build(), ephemeral: true);
    }


    // =========================================================================
    // /playlist delete <name>
    // =========================================================================

    /// <summary>Deletes one of the user's saved playlists by name, after verifying it exists.</summary>
    [SlashCommand("delete", "Delete one of your saved playlists.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task DeleteAsync(
        [MinLength(1), MaxLength(64),
         Summary("name", "Name of the playlist to delete")] string name)
    {
        await DeferAsync(ephemeral: true);

        name = name.Trim();

        try
        {
            // Check it exists first
            var matching = await db.PlaylistTracks
                .Where(p => p.UserId == UserId && p.ServerId == ServerId && EF.Functions.ILike(p.Name, name))
                .ToListAsync();

            if (matching.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Playlist",
                    $"No playlist named **\"{name}\"** found.",
                    Username).Build(), ephemeral: true);
                return;
            }

            db.PlaylistTracks.RemoveRange(matching);
            await db.SaveChangesAsync();

            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "🗑️  Playlist Deleted", $"Playlist **\"{name}\"** has been deleted.",
                ColourRed, footer: Username, footerIconUrl: AvatarUrl).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Playlist", ex.Message, Username).Build(), ephemeral: true);
        }
    }

    /// <summary>Factory delegate passed to Lavalink4NET's player-retrieval API to construct a new <see cref="CustomPlayer"/> when one doesn't already exist.</summary>
    private static ValueTask<CustomPlayer> CreatePlayerAsync(
        IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);
        return ValueTask.FromResult(new CustomPlayer(properties));
    }
}
