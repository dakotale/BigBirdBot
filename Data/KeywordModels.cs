namespace DiscordBot.Data;

// ─────────────────────────────────────────────────────────────────────────────
// Result types returned by KeywordService. Callers never see EF entities or the
// DbContext — each service method returns one of these records (or a primitive).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A single keyword entry chosen for delivery (replaces <c>GetChatAction</c>'s result row).</summary>
public sealed record ChatActionEntry(int Id, string FilePath, bool Nsfw, string Keyword);

/// <summary>Summary stats for a keyword (replaces <c>GetChatKeywordInfo</c>).</summary>
public sealed record KeywordInfo(string Keyword, int EntryCount, string? CreatedBy);

/// <summary>One registered keyword for a server (replaces a <c>GetChatKeywordsByServer</c> row).</summary>
public sealed record KeywordListEntry(string Keyword, string AddKeyword, string? CreatedBy);

/// <summary>One alias pointing at a keyword (replaces a <c>GetChatKeywordAliases</c> row).</summary>
public sealed record AliasEntry(string Alias, string Keyword, string? CreatedBy, DateTime CreatedOn);

/// <summary>One (keyword, time) grouping of a user's schedule, returned after adding one (replaces an <c>AddUsersScheduledKeyword</c> row).</summary>
public sealed record ScheduleSummary(DateTime ScheduleTime, string KeywordsCsv);

/// <summary>One of a user's scheduled keyword deliveries (replaces a <c>GetUsersScheduledKeywords</c> row).</summary>
public sealed record UserScheduleEntry(string Keyword, DateTime ScheduleTime);

/// <summary>A due scheduled delivery to send now (replaces a <c>GetUsersScheduledKeyword</c> row).</summary>
public sealed record DueKeywordDelivery(string UserId, string FilePath, string Keyword, int? EntryId = null);

/// <summary>One row of the owner-only schedule listing (replaces a <c>GetScheduledEventUsers</c> row).</summary>
public sealed record ScheduledEventUser(string Username, string Keyword, DateTime ScheduledFor);
