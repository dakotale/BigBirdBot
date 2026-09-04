namespace DiscordBot.Data;

// ─────────────────────────────────────────────────────────────────────────────
// Entity types for the audit-log feature area. Every insert-only table Constants/
// Audit.cs used to write to via ADO.NET — no reads happen anywhere in the bot,
// so these exist purely to satisfy EF Core's mapping requirements for an insert.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A generic slash-command execution record. Table <c>dbo.AuditLog</c>.</summary>
public sealed class AuditLogEntry
{
    public int AuditLogId { get; set; }
    public string Command { get; set; } = "";
    public long ServerUid { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}

/// <summary>A button/component interaction record (e.g. a pronoun-role toggle). Table <c>dbo.AuditButtonExecuted</c>.</summary>
public sealed class AuditButtonExecuted
{
    public int Id { get; set; }
    public string ButtonId { get; set; } = "";
    public long UserUid { get; set; }
    public long ServerUid { get; set; }
    public DateTime ExecutedOn { get; set; }
}

/// <summary>Records that the bot was added to a new guild. Table <c>dbo.AuditGuildJoined</c>.</summary>
public sealed class AuditGuildJoined
{
    public int Id { get; set; }
    public long ServerUid { get; set; }
    public string ServerName { get; set; } = "";
    public DateTime JoinedOn { get; set; }
}

/// <summary>Records an emoji reaction added to one of the bot's posts. Table <c>dbo.AuditReactionAdded</c>.</summary>
public sealed class AuditReactionAdded
{
    public int Id { get; set; }
    public string Emoji { get; set; } = "";
    public long MessageUid { get; set; }
    public long UserUid { get; set; }
    public long ChannelUid { get; set; }
    public DateTime AddedOn { get; set; }
}

/// <summary>Records that a member joined a guild. Table <c>dbo.AuditUserJoined</c>.</summary>
public sealed class AuditUserJoined
{
    public int Id { get; set; }
    public long UserUid { get; set; }
    public long ServerUid { get; set; }
    public DateTime JoinedOn { get; set; }
}

/// <summary>Records that a member left (or was removed from) a guild. Table <c>dbo.AuditUserLeft</c>.</summary>
public sealed class AuditUserLeft
{
    public int Id { get; set; }
    public long UserUid { get; set; }
    public long ServerUid { get; set; }
    public DateTime LeftOn { get; set; }
}

/// <summary>Records that a message-triggered mini-game (only the bonus word puzzle remains) was won. Table <c>dbo.AuditGameTrigger</c>.</summary>
public sealed class AuditGameTrigger
{
    public int Id { get; set; }
    public string Game { get; set; } = "";
    public long UserUid { get; set; }
    public long ServerUid { get; set; }
    public DateTime TriggeredOn { get; set; }
}
