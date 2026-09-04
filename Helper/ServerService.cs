using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for a guild's <c>Servers</c> configuration row — the bot's root per-guild
/// settings table. Replaces <c>AddServer</c>, <c>GetServerByID</c>, <c>GetServers</c>,
/// <c>ToggleAnnouncements</c>, <c>GetEmbedBroken</c>, and <c>UpdateBrokenEmbed</c> (playback
/// volume also lives on this table but is owned by <see cref="MusicService"/>, matching the
/// audio feature area it serves). Supersedes the old ADO.NET <c>Helper/ServerHelper.cs</c>.
/// </summary>
public sealed class ServerService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Fetches a guild's config row, or null if it has no server record yet. Replaces <c>GetServerByID</c> / <c>ServerHelper.GetServerInfo</c>.</summary>
    public async Task<ServerInfo?> GetServerInfoAsync(ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Servers
            .Where(s => s.ServerUid == (long)serverId)
            .Select(s => new ServerInfo(
                (ulong)s.ServerUid, s.ServerName,
                s.DefaultChannelId != null ? s.DefaultChannelId.Value.ToString() : "",
                s.IsActive, s.AnnouncementsEnabled))
            .FirstOrDefaultAsync();
    }

    /// <summary>Every active server. Replaces <c>GetServers</c>.</summary>
    public async Task<IReadOnlyList<ActiveServer>> GetActiveServersAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Servers
            .Where(s => s.IsActive)
            .Select(s => new ActiveServer(
                (ulong)s.ServerUid, s.ServerName,
                s.DefaultChannelId != null ? s.DefaultChannelId.Value.ToString() : "",
                s.IsActive))
            .ToListAsync();
    }

    /// <summary>Registers a new guild if it isn't already known. Replaces <c>AddServer</c>.</summary>
    public async Task AddServerAsync(ulong serverUid, string serverName, ulong defaultChannelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long uid = (long)serverUid;
        if (await db.Servers.AnyAsync(s => s.ServerUid == uid)) return;

        db.Servers.Add(new Server
        {
            ServerUid = uid,
            ServerName = serverName,
            DefaultChannelId = (long)defaultChannelId,
            Volume = 100,
            FixEmbed = false,
            IsPlayerConnected = false,
            IsActive = true,
            CreatedOn = DateTime.Now,
            AnnouncementsEnabled = false
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Flips a server's announcements flag; when turning it on, also sets the channel
    /// announcements should post in. Returns null if the server has no row (the caller
    /// supplies the "unknown error" fallback text, as before). Replaces <c>ToggleAnnouncements</c>.
    /// </summary>
    public async Task<AnnouncementsToggleResult?> ToggleAnnouncementsAsync(ulong serverUid, ulong channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerUid == (long)serverUid);
        if (server is null) return null;

        bool enabled = !server.AnnouncementsEnabled;
        server.AnnouncementsEnabled = enabled;
        if (enabled) server.DefaultChannelId = (long)channelId;

        await db.SaveChangesAsync();

        string message = enabled
            ? "Announcements enabled. Timed events (word puzzles, jackpot results) will be posted in this channel."
            : "Announcements disabled. The bot will no longer post timed events in this server.";
        return new AnnouncementsToggleResult(enabled, message);
    }

    /// <summary>
    /// Whether the link-embed fixer is enabled for a server. Replaces <c>GetEmbedBroken</c>
    /// (defaults to <c>false</c> when the server has no row, equivalent to the original's
    /// "no rows returned" case in every reachable scenario since the caller already confirmed
    /// the server exists before this is ever called).
    /// </summary>
    public async Task<bool> GetEmbedFixEnabledAsync(ulong serverUid)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Servers
            .Where(s => s.ServerUid == (long)serverUid)
            .Select(s => (bool?)s.FixEmbed)
            .FirstOrDefaultAsync() ?? false;
    }

    /// <summary>
    /// Toggles the link-embed fixer for a server and returns the confirmation text, or null if
    /// the server has no row (matches <c>IF @ServerID IS NULL RETURN;</c> — no rows). Replaces
    /// <c>UpdateBrokenEmbed</c>.
    /// </summary>
    public async Task<string?> ToggleEmbedFixAsync(ulong serverUid)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerUid == (long)serverUid);
        if (server is null) return null;

        server.FixEmbed = !server.FixEmbed;
        await db.SaveChangesAsync();

        return server.FixEmbed
            ? "The bot will now embed Twitter, Reddit, and Bluesky links."
            : "The bot will no longer embed Twitter, Reddit, and Bluesky links.";
    }
}
