namespace DiscordBot.Data;

// ─────────────────────────────────────────────────────────────────────────────
// Entity types for the keyword feature area (first area migrated off stored
// procedures). The database already exists and is managed outside EF Core, so
// every property is mapped explicitly to its existing column in
// BigBirdContext.OnModelCreating — there are no migrations.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One stored response for a chat keyword: a local file path, a URL, or plain text.
/// A keyword can have many rows; <see cref="GetChatAction"/>-style lookups pick one at random.
/// Table <c>dbo.ChatKeyword</c>.
/// </summary>
public sealed class ChatKeyword
{
    public int Id { get; set; }

    /// <summary>The keyword this entry belongs to. Column <c>ChatKeyword</c>.</summary>
    public string Keyword { get; set; } = "";

    /// <summary>File path, URL, or plain text served when the keyword is triggered.</summary>
    public string FilePath { get; set; } = "";

    public DateTime CreatedOn { get; set; }

    /// <summary>Column <c>NSFW</c> — set when a member ❌-reacts a served entry.</summary>
    public bool Nsfw { get; set; }
}

/// <summary>
/// Registers a keyword's <c>-add&lt;keyword&gt;</c> trigger word for one server.
/// Table <c>dbo.ChatKeywordMap</c>.
/// </summary>
public sealed class ChatKeywordMap
{
    public int Id { get; set; }

    /// <summary>The trigger word, e.g. <c>addcat</c>.</summary>
    public string AddKeyword { get; set; } = "";

    public long ServerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public string? CreatedBy { get; set; }

    /// <summary>
    /// Computed column <c>replace([AddKeyword],'add','')</c> — read-only, never written by EF.
    /// Note this replaces <i>every</i> "add" substring, not just the leading prefix.
    /// </summary>
    public string Keyword { get; set; } = "";
}

/// <summary>
/// An extra trigger word that serves entries from an existing keyword, scoped to one server.
/// Table <c>dbo.ChatKeywordAlias</c> (added by migration 003).
/// </summary>
public sealed class ChatKeywordAlias
{
    public int Id { get; set; }

    public string Alias { get; set; } = "";

    /// <summary>The keyword whose entries this alias serves.</summary>
    public string Keyword { get; set; } = "";

    public long ServerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = "";
}

/// <summary>
/// A recurring DM delivery of a keyword's entries to one user ("thirst").
/// Table <c>dbo.UsersScheduledKeyword</c> — no surrogate key in the database, so the
/// three columns together form the entity key.
/// </summary>
public sealed class UsersScheduledKeyword
{
    /// <summary>Discord user id as a decimal string. Column <c>UserID</c>.</summary>
    public string UserId { get; set; } = "";

    /// <summary>The keyword to deliver. Column <c>ChatKeyword</c>.</summary>
    public string ChatKeyword { get; set; } = "";

    public DateTime ScheduledDateTime { get; set; }
}
