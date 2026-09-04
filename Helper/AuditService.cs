using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for the bot's audit-log tables. Replaces <c>Constants/Audit.cs</c> (the
/// <c>AddAudit</c>/<c>AddAuditUserJoined</c>/etc. procedures) — every method here is a plain
/// insert; nothing in the bot ever reads these tables back.
///
/// Timestamp columns are set explicitly rather than left to a database default: <see cref="InsertAuditAsync"/>
/// uses local time (<c>DateTime.Now</c>), matching the original <c>AddAudit</c> procedure's
/// <c>GETDATE()</c>; every other method here uses UTC (<c>DateTime.UtcNow</c>), matching
/// those procedures' <c>GETUTCDATE()</c> column defaults.
/// </summary>
public sealed class AuditService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>
    /// The bot owner's user id. <c>AddAudit</c> silently skipped logging for this one user —
    /// preserved here since it's the observed behaviour, not something to "fix".
    /// </summary>
    private const string OwnerId = "171369791486033920";

    /// <summary>Records a generic slash-command execution against a server (or DM channel). Replaces <c>AddAudit</c>.</summary>
    public async Task InsertAuditAsync(string command, string createdBy, long? serverId)
    {
        if (createdBy is null || createdBy == OwnerId) return; // matches AddAudit's IF guard

        await using var db = await contextFactory.CreateDbContextAsync();

        db.AuditLog.Add(new AuditLogEntry
        {
            Command = command,
            ServerUid = serverId ?? 0,
            CreatedOn = DateTime.Now,
            CreatedBy = createdBy
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Records that a member joined a guild. Replaces <c>AddAuditUserJoined</c>.</summary>
    public Task InsertUserJoinedAuditAsync(ulong userId, ulong guildId) =>
        AddAsync(new AuditUserJoined { UserUid = (long)userId, ServerUid = (long)guildId, JoinedOn = DateTime.UtcNow });

    /// <summary>Records that a member left (or was removed from) a guild. Replaces <c>AddAuditUserLeft</c>.</summary>
    public Task InsertUserLeftAuditAsync(ulong userId, ulong guildId) =>
        AddAsync(new AuditUserLeft { UserUid = (long)userId, ServerUid = (long)guildId, LeftOn = DateTime.UtcNow });

    /// <summary>Records a button/component interaction, e.g. a pronoun-role toggle. Replaces <c>AddAuditButtonExecuted</c>.</summary>
    public Task InsertButtonAuditAsync(string buttonId, ulong userId, ulong guildId) =>
        AddAsync(new AuditButtonExecuted { ButtonId = buttonId, UserUid = (long)userId, ServerUid = (long)guildId, ExecutedOn = DateTime.UtcNow });

    /// <summary>Records that the bot was added to a new guild. Replaces <c>AddAuditGuildJoined</c>.</summary>
    public Task InsertGuildJoinedAuditAsync(ulong guildId, string guildName) =>
        AddAsync(new AuditGuildJoined { ServerUid = (long)guildId, ServerName = guildName, JoinedOn = DateTime.UtcNow });

    /// <summary>Records an emoji reaction added to a message, e.g. an NSFW-flag reaction. Replaces <c>AddAuditReactionAdded</c>.</summary>
    public Task InsertReactionAuditAsync(string emoji, ulong messageId, ulong userId, ulong channelId) =>
        AddAsync(new AuditReactionAdded { Emoji = emoji, MessageUid = (long)messageId, UserUid = (long)userId, ChannelUid = (long)channelId, AddedOn = DateTime.UtcNow });

    /// <summary>Records that a message-triggered event (the bonus word puzzle) was won. Replaces <c>AddAuditGameTrigger</c>.</summary>
    public Task InsertGameTriggerAuditAsync(string game, ulong userId, ulong guildId) =>
        AddAsync(new AuditGameTrigger { Game = game, UserUid = (long)userId, ServerUid = (long)guildId, TriggeredOn = DateTime.UtcNow });

    /// <summary>Shared single-row insert + save, used by every audit method above.</summary>
    private async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync();
    }
}
