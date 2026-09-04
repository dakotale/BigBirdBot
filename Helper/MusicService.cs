using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for music playback persistence: the played-track history table, the
/// restore-on-restart queue, which voice/text channel pair the player is connected to per
/// guild, and the saved playback volume (physically a column on <c>Servers</c>, but owned
/// here since only the audio feature area touches it). Replaces <c>AddMusic</c>,
/// <c>DeleteMusicQueue</c>, <c>DeleteMusicQueueAll</c>, <c>GetMusicQueue</c>,
/// <c>AddPlayerConnected</c>, <c>DeletePlayerConnected</c>, <c>GetPlayerConnected</c>,
/// <c>GetVolume</c>, and <c>UpdateVolume</c>.
/// </summary>
public sealed class MusicService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>
    /// Logs a played track to history and enqueues it for restart-restore, using the guild's
    /// currently connected voice/text channel pair. Replaces <c>AddMusic</c>. The original
    /// procedure's scalar lookup of that pair would throw a SQL error if none existed (an
    /// unreachable precondition failure in practice, since playback always joins a voice
    /// channel first); this throws too, just via a clearer message.
    /// </summary>
    public async Task AddMusicAsync(ulong serverId, string videoId, string author, string title, string url, string createdBy)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;
        createdBy ??= "";

        var connected = await db.PlayerConnected
            .Where(p => p.ServerUid == sid)
            .Select(p => new { p.VoiceChannelId, p.TextChannelId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"No PlayerConnected row for server {serverId} — AddMusic requires an active voice connection.");

        var music = new MusicHistoryEntry
        {
            ServerUid = sid,
            VideoId = videoId,
            Author = author,
            Title = title,
            Url = url,
            CreatedOn = DateTime.Now,
            CreatedBy = createdBy
        };
        db.Music.Add(music);
        await db.SaveChangesAsync();

        db.MusicQueue.Add(new MusicQueueEntry
        {
            MusicId = music.MusicId,
            ServerUid = sid,
            VoiceChannelId = connected.VoiceChannelId,
            TextChannelId = connected.TextChannelId,
            Url = url,
            CreatedOn = DateTime.Now,
            CreatedBy = createdBy
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Removes the earliest queued entry matching a URL (one play = one dequeue). Replaces <c>DeleteMusicQueue</c>.</summary>
    public async Task DeleteQueueEntryAsync(string url)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        int? id = await db.MusicQueue
            .Where(q => q.Url == url)
            .OrderBy(q => q.MusicQueueId)
            .Select(q => (int?)q.MusicQueueId)
            .FirstOrDefaultAsync();

        if (id is null) return;
        await db.MusicQueue.Where(q => q.MusicQueueId == id).ExecuteDeleteAsync();
    }

    /// <summary>Clears a guild's entire persisted queue. Replaces <c>DeleteMusicQueueAll</c>.</summary>
    public async Task ClearQueueAsync(ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.MusicQueue.Where(q => q.ServerUid == (long)serverId).ExecuteDeleteAsync();
    }

    /// <summary>A guild's persisted queue, in enqueue order. Replaces <c>GetMusicQueue</c>.</summary>
    public async Task<IReadOnlyList<QueuedTrack>> GetQueueAsync(ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.MusicQueue
            .Where(q => q.ServerUid == (long)serverId)
            .OrderBy(q => q.MusicQueueId)
            .Select(q => new QueuedTrack(q.MusicQueueId, q.Url, q.CreatedBy))
            .ToListAsync();
    }

    /// <summary>Records the guild's connected voice/text channel pair, if not already recorded. Replaces <c>AddPlayerConnected</c>.</summary>
    public async Task AddPlayerConnectedAsync(ulong serverId, ulong voiceChannelId, ulong textChannelId, string createdBy)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;
        bool exists = await db.PlayerConnected.AnyAsync(p =>
            p.ServerUid == sid && p.VoiceChannelId == (long)voiceChannelId && p.TextChannelId == (long)textChannelId);
        if (exists) return;

        db.PlayerConnected.Add(new PlayerConnected
        {
            ServerUid = sid,
            VoiceChannelId = (long)voiceChannelId,
            TextChannelId = (long)textChannelId,
            CreatedOn = DateTime.Now,
            CreatedBy = createdBy
        });

        await db.Servers.Where(s => s.ServerUid == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlayerConnected, true));
        await db.SaveChangesAsync();
    }

    /// <summary>Clears the guild's connected-player record. Replaces <c>DeletePlayerConnected</c>.</summary>
    public async Task DeletePlayerConnectedAsync(ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;
        await db.PlayerConnected.Where(p => p.ServerUid == sid).ExecuteDeleteAsync();
        await db.Servers.Where(s => s.ServerUid == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlayerConnected, false));
    }

    /// <summary>Every guild where the player is currently connected, alphabetical by server name. Replaces <c>GetPlayerConnected</c>.</summary>
    public async Task<IReadOnlyList<ConnectedPlayer>> GetConnectedPlayersAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await (
            from pc in db.PlayerConnected
            join s in db.Servers on pc.ServerUid equals s.ServerUid
            orderby s.ServerName
            select new ConnectedPlayer((ulong)pc.ServerUid, s.ServerName, (ulong)pc.VoiceChannelId, (ulong)pc.TextChannelId))
            .ToListAsync();
    }

    /// <summary>A guild's saved playback volume, or null if the guild has no server row. Replaces <c>GetVolume</c>.</summary>
    public async Task<int?> GetVolumeAsync(ulong serverUid)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Servers
            .Where(s => s.ServerUid == (long)serverUid)
            .Select(s => (int?)s.Volume)
            .FirstOrDefaultAsync();
    }

    /// <summary>Persists a guild's playback volume; a no-op if the guild has no server row. Replaces <c>UpdateVolume</c>.</summary>
    public async Task UpdateVolumeAsync(ulong serverUid, int volume)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.Servers
            .Where(s => s.ServerUid == (long)serverUid)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Volume, volume));
    }
}
