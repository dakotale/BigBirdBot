namespace DiscordBot.Data;

// ─────────────────────────────────────────────────────────────────────────────
// Result types returned by the core (non-keyword) EF Core services. Callers never
// see an entity or the DbContext — each service method returns one of these
// records/tuples (or a primitive), matching the pattern established by
// Data/KeywordModels.cs.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A guild's config row, as read by the scheduler and admin commands. Replaces the old <c>ServerHelper.ServerInfo</c> / <c>GetServerByID</c>.</summary>
public sealed record ServerInfo(ulong ServerUid, string ServerName, string DefaultChannelId, bool IsActive, bool AnnouncementsEnabled);

/// <summary>One row of the active-servers listing (replaces a <c>GetServers</c> row).</summary>
public sealed record ActiveServer(ulong ServerUid, string ServerName, string DefaultChannelId, bool IsActive);

/// <summary>Result of toggling a server's announcements setting (replaces <c>ToggleAnnouncements</c>'s result row).</summary>
public sealed record AnnouncementsToggleResult(bool Enabled, string Message);

/// <summary>A due one-off reminder to deliver now (replaces a <c>GetDueReminders</c> row).</summary>
public sealed record DueReminder(string UserId, string Message);

/// <summary>
/// A birthday to celebrate today (replaces a <c>GetTodaysBirthdays</c> row). <see cref="GuildId"/>
/// is left as the raw stored string (not pre-parsed) so a malformed/missing value only skips
/// that one row at the call site's per-row try/catch, matching the original behaviour.
/// </summary>
public sealed record DueBirthday(string Mention, string GuildId, string? ChannelId);

/// <summary>The currently active bonus word puzzle in a channel (replaces a <c>GetActivePetPuzzle</c>/<c>GetPetWordPuzzle</c> row).</summary>
public sealed record ActivePuzzle(int PuzzleId, string Word, DateTime ExpiresAt);

/// <summary>One guild where the music player is currently connected (replaces a <c>GetPlayerConnected</c> row).</summary>
public sealed record ConnectedPlayer(ulong ServerUid, string ServerName, ulong VoiceChannelId, ulong TextChannelId);

/// <summary>One track in a guild's persisted playback queue (replaces a <c>GetMusicQueue</c> row).</summary>
public sealed record QueuedTrack(int MusicQueueId, string Url, string CreatedBy);
