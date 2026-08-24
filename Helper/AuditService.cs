using DiscordBot.Data;
using DiscordBot.Models.Generated;

namespace DiscordBot.Helper;

/// <summary>
/// Shared EF Core replacement for Constants/Audit.cs's SQL-Server-only writes. The Postgres
/// schema for all 7 audit tables was already migrated in Phase 2 (archive schema, confirmed via
/// \d and information_schema.columns) — only the historical row data was deliberately left
/// behind in SQL Server. These tables had simply never been written to via EF until now, since
/// Audit.cs was the only thing touching them. Same DiscordbotContext-first-param pattern as
/// CreditService/ChallengeService/JackpotService/ShopHelper/ServerHelper.
///
/// NOTE: AuditLog.CreatedOn has no DB-side default (verified via information_schema — the other
/// 6 tables' timestamp columns do, e.g. ExecutedOn/JoinedOn/TriggeredOn), so it's set explicitly
/// here; the other 6 are left unset to defer to the column default, matching the DB-default-
/// reliance pattern used throughout this migration.
/// </summary>
public static class AuditService
{
    /// <summary>Records a generic command execution against a server.</summary>
    public static async Task InsertAuditAsync(DiscordbotContext db, string command, string createdBy, string serverId)
    {
        db.AuditLogs.Add(new AuditLog { Command = command, CreatedBy = createdBy, ServerUid = long.Parse(serverId), CreatedOn = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Records that a member joined a guild.</summary>
    public static async Task InsertUserJoinedAuditAsync(DiscordbotContext db, string userId, string guildId)
    {
        db.AuditUserJoineds.Add(new AuditUserJoined { UserUid = long.Parse(userId), ServerUid = long.Parse(guildId) });
        await db.SaveChangesAsync();
    }

    /// <summary>Records that a member left (or was removed from) a guild.</summary>
    public static async Task InsertUserLeftAuditAsync(DiscordbotContext db, string userId, string guildId)
    {
        db.AuditUserLefts.Add(new AuditUserLeft { UserUid = long.Parse(userId), ServerUid = long.Parse(guildId) });
        await db.SaveChangesAsync();
    }

    /// <summary>Records a button/component interaction, e.g. a pronoun-role toggle.</summary>
    public static async Task InsertButtonAuditAsync(DiscordbotContext db, string buttonId, string userId, string guildId)
    {
        db.AuditButtonExecuteds.Add(new AuditButtonExecuted { ButtonId = buttonId, UserUid = long.Parse(userId), ServerUid = long.Parse(guildId) });
        await db.SaveChangesAsync();
    }

    /// <summary>Records that the bot was added to a new guild.</summary>
    public static async Task InsertGuildJoinedAuditAsync(DiscordbotContext db, string guildId, string guildName)
    {
        db.AuditGuildJoineds.Add(new AuditGuildJoined { ServerUid = long.Parse(guildId), ServerName = guildName });
        await db.SaveChangesAsync();
    }

    /// <summary>Records an emoji reaction added to a message, e.g. a trivia answer or NSFW-flag reaction.</summary>
    public static async Task InsertReactionAuditAsync(DiscordbotContext db, string emoji, string messageId, string userId, string channelId)
    {
        db.AuditReactionAddeds.Add(new AuditReactionAdded { Emoji = emoji, MessageUid = long.Parse(messageId), UserUid = long.Parse(userId), ChannelUid = long.Parse(channelId) });
        await db.SaveChangesAsync();
    }

    /// <summary>Records that a message-triggered mini-game (Scramble, Wordle, pet word puzzle) was won.</summary>
    public static async Task InsertGameTriggerAuditAsync(DiscordbotContext db, string game, string userId, string guildId)
    {
        db.AuditGameTriggers.Add(new AuditGameTrigger { Game = game, UserUid = long.Parse(userId), ServerUid = long.Parse(guildId) });
        await db.SaveChangesAsync();
    }
}
