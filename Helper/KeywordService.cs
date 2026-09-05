using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// All keyword-area database access, using EF Core (PostgreSQL). Replaces the former
/// <c>ChatKeyword*</c> / <c>UsersScheduledKeyword</c> / <c>GetChatAction</c> /
/// <c>GetScheduledEventUsers</c> stored procedures.
///
/// Registered as a singleton; every method opens and disposes its own
/// <see cref="BigBirdContext"/> via the injected <see cref="IDbContextFactory{TContext}"/>,
/// so it is safe to call from interaction modules, the singleton <c>BotHost</c>, and
/// background scheduler tasks alike. Callers only ever see primitives and the records
/// in <c>Data/KeywordModels.cs</c> — never an entity or the context.
///
/// Behaviour is a faithful port of the original procedures, including their quirks
/// (see the plan / commit message); nothing is "fixed" here beyond what the port
/// does incidentally.
/// </summary>
public sealed class KeywordService(IDbContextFactory<BigBirdContext> contextFactory)
{
    // ── ChatKeyword entries ──────────────────────────────────────────────────

    /// <summary>
    /// Adds one entry (file path / URL / text) under a keyword. Replaces <c>AddChatKeyword</c>
    /// (which also took a <c>@UserID</c> that it never stored — there is no user column).
    /// </summary>
    public async Task AddEntryAsync(string keyword, string value)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        db.ChatKeywords.Add(new ChatKeyword
        {
            Keyword = keyword,
            FilePath = value.Replace("'", "").Trim(),
            CreatedOn = DateTime.Now,
            Nsfw = false
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Removes entries matching an exact stored value (and keyword). Replaces <c>DeleteChatKeywordURL</c>.</summary>
    public async Task DeleteEntryAsync(string filePath, string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.ChatKeywords
            .Where(k => k.FilePath == filePath && k.Keyword == keyword)
            .ExecuteDeleteAsync();
    }

    /// <summary>Removes one entry by its row id — used when a dead link or missing local file is hit at serve time.</summary>
    public async Task DeleteEntryByIdAsync(int id)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.ChatKeywords.Where(k => k.Id == id).ExecuteDeleteAsync();
    }

    /// <summary>Every local-file entry (id + stored <c>file:</c> value), for the reconcile job.</summary>
    public async Task<IReadOnlyList<(int Id, string Value)>> GetLocalFileEntriesAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var rows = await db.ChatKeywords
            .Where(k => !k.FilePath.StartsWith("http"))
            .Select(k => new { k.Id, k.FilePath })
            .ToListAsync();

        return rows
            .Where(r => KeywordFiles.IsLocalFile(r.FilePath))
            .Select(r => (r.Id, r.FilePath))
            .ToList();
    }

    /// <summary>Bulk-removes entries by row id. Returns the number deleted.</summary>
    public async Task<int> DeleteEntriesByIdAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return 0;

        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.ChatKeywords.Where(k => ids.Contains(k.Id)).ExecuteDeleteAsync();
    }

    /// <summary>Returns a keyword's entry paths, newest first. Replaces <c>GetChatKeywordRecent</c>.</summary>
    public async Task<IReadOnlyList<string>> GetRecentEntriesAsync(string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.ChatKeywords
            .Where(k => k.Keyword == keyword)
            .OrderByDescending(k => k.Id)
            .Select(k => k.FilePath)
            .ToListAsync();
    }

    /// <summary>
    /// Entry count + creator for a keyword, or <c>null</c> when the keyword is not
    /// registered in any server's map. Replaces <c>GetChatKeywordInfo</c> (the app only
    /// ever read the first row).
    /// </summary>
    public async Task<KeywordInfo?> GetInfoAsync(string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        int entryCount = await db.ChatKeywords.CountAsync(k => k.Keyword == keyword);

        var map = await db.ChatKeywordMaps
            .Where(m => m.Keyword == keyword)
            .Select(m => new { m.Keyword, m.CreatedBy })
            .FirstOrDefaultAsync();

        return map is null ? null : new KeywordInfo(map.Keyword, entryCount, map.CreatedBy);
    }

    /// <summary>
    /// NSFW flag of the first keyword entry whose file path contains <paramref name="message"/>,
    /// or <c>null</c> when none match. Replaces <c>GetKeywordNSFW</c> (leading-wildcard LIKE, unchanged).
    /// </summary>
    public async Task<bool?> GetNsfwAsync(string message)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.ChatKeywords
            .Where(k => EF.Functions.Like(k.FilePath, "%" + message + "%"))
            .Select(k => (bool?)k.Nsfw)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Marks every not-yet-flagged keyword entry whose file path contains <paramref name="message"/>
    /// as NSFW; returns <c>true</c> if any entry matches the text at all. Replaces <c>MarkKeywordNSFW</c>.
    /// </summary>
    public async Task<bool> MarkNsfwAsync(string message)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        string pattern = "%" + message + "%";

        await db.ChatKeywords
            .Where(k => EF.Functions.Like(k.FilePath, pattern) && !k.Nsfw)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Nsfw, true));

        return await db.ChatKeywords.AnyAsync(k => EF.Functions.Like(k.FilePath, pattern));
    }

    // ── ChatKeywordMap (keyword registration) ────────────────────────────────

    /// <summary>
    /// Registers a keyword's trigger word for a server if it isn't already. Replaces
    /// <c>AddChatKeywordMap</c> (whose <c>@Keyword</c> parameter was unused — the
    /// <c>Keyword</c> column is computed from <c>AddKeyword</c>).
    /// </summary>
    public async Task AddMapAsync(ulong serverId, string addKeyword, string createdBy)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        if (await db.ChatKeywordMaps.AnyAsync(m => m.ServerId == sid && m.AddKeyword == addKeyword))
            return;

        db.ChatKeywordMaps.Add(new ChatKeywordMap
        {
            AddKeyword = addKeyword,
            ServerId = sid,
            CreatedOn = DateTime.Now,
            CreatedBy = createdBy
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a keyword everywhere: its server map rows, all its entries, and any
    /// scheduled deliveries of it. Replaces <c>DeleteChatKeyword</c>.
    ///
    /// The original procedure declared <c>@Keyword</c> as <c>int</c>, so it threw
    /// "conversion failed" for every non-numeric keyword — i.e. this command never
    /// worked for normal keywords. This port implements the procedure's evident
    /// intent (delete by keyword name) and matches its lack of a <c>ServerID</c>
    /// filter on the entry/rename side.
    /// </summary>
    public async Task DeleteKeywordAsync(string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        string addKeyword = "add" + keyword;

        await db.ChatKeywordMaps.Where(m => m.AddKeyword == addKeyword).ExecuteDeleteAsync();
        await db.ChatKeywords.Where(k => k.Keyword == keyword).ExecuteDeleteAsync();
        await db.UsersScheduledKeywords.Where(u => u.ChatKeyword == keyword).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Renames a keyword: updates the server's trigger word and the keyword name on
    /// every entry. Replaces <c>RenameChatKeyword</c> (the entry-side update has no
    /// <c>ServerID</c> filter — cross-server, as before).
    /// </summary>
    public async Task RenameKeywordAsync(string oldName, string newName, ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;
        string newAddKeyword = "add" + newName;

        await db.ChatKeywordMaps
            .Where(m => m.Keyword == oldName && m.ServerId == sid)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AddKeyword, newAddKeyword));

        // Local-file entries are stored as "file:<keyword>/<name>" and the folder is moved
        // on disk to match, so rewrite the prefix before the keyword name itself changes.
        string oldPrefix = $"file:{oldName}/";
        string newPrefix = $"file:{newName}/";
        await db.ChatKeywords
            .Where(k => k.Keyword == oldName && k.FilePath.StartsWith(oldPrefix))
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.FilePath,
                k => newPrefix + k.FilePath.Substring(oldPrefix.Length)));

        await db.ChatKeywords
            .Where(k => k.Keyword == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Keyword, newName));
    }

    /// <summary>Every keyword registered in a server, oldest first. Replaces <c>GetChatKeywordsByServer</c>.</summary>
    public async Task<IReadOnlyList<KeywordListEntry>> GetKeywordsForServerAsync(ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        return await db.ChatKeywordMaps
            .Where(m => m.ServerId == sid)
            .OrderBy(m => m.Id)
            .Select(m => new KeywordListEntry(m.Keyword, m.AddKeyword, m.CreatedBy))
            .ToListAsync();
    }

    /// <summary>
    /// Resolves a trigger word (e.g. <c>addcat</c>) to its keyword name (e.g. <c>cat</c>),
    /// or <c>null</c> if it isn't a registered trigger. Replaces <c>GetChatKeywordMap</c>.
    /// </summary>
    public async Task<string?> ResolveAddKeywordAsync(string addKeyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.ChatKeywordMaps
            .Where(m => m.AddKeyword == addKeyword)
            .Select(m => m.Keyword)
            .FirstOrDefaultAsync();
    }

    // ── ChatKeywordAlias ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates an alias for a keyword in a server. Returns <c>false</c> if the alias
    /// already exists in that server (nothing is written), <c>true</c> if it was added.
    /// Replaces <c>AddChatKeywordAlias</c>.
    /// </summary>
    public async Task<bool> AddAliasAsync(string alias, string keyword, ulong serverId, string createdBy)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        if (await db.ChatKeywordAliases.AnyAsync(a => a.Alias == alias && a.ServerId == sid))
            return false;

        db.ChatKeywordAliases.Add(new ChatKeywordAlias
        {
            Alias = alias,
            Keyword = keyword,
            ServerId = sid,
            CreatedOn = DateTime.UtcNow, // alias subsystem uses UTC (matches the original GETUTCDATE())
            CreatedBy = createdBy
        });

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Every alias pointing at a keyword in a server, ordered by alias. Replaces <c>GetChatKeywordAliases</c>.</summary>
    public async Task<IReadOnlyList<AliasEntry>> GetAliasesAsync(string keyword, ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        return await db.ChatKeywordAliases
            .Where(a => a.Keyword == keyword && a.ServerId == sid)
            .OrderBy(a => a.Alias)
            .Select(a => new AliasEntry(a.Alias, a.Keyword, a.CreatedBy, a.CreatedOn))
            .ToListAsync();
    }

    /// <summary>Removes an alias from a server. Replaces <c>DeleteChatKeywordAlias</c>.</summary>
    public async Task DeleteAliasAsync(string alias, ulong serverId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        await db.ChatKeywordAliases
            .Where(a => a.Alias == alias && a.ServerId == sid)
            .ExecuteDeleteAsync();
    }

    // ── Message-trigger lookup ───────────────────────────────────────────────

    /// <summary>
    /// Given a chat message, finds a registered keyword (or alias) whose name appears
    /// as a whole word in the message and returns one random entry for it, or <c>null</c>
    /// when nothing matches. Replaces <c>GetChatAction</c>.
    ///
    /// Like the procedure, a direct keyword match is weighted by entry count (a keyword
    /// with more entries is proportionally more likely to be the one picked), and
    /// aliases are only consulted when there is no direct match.
    /// </summary>
    public async Task<ChatActionEntry?> ResolveChatActionAsync(ulong serverId, string message)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverId;

        string[] words = message.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .Distinct()
            .ToArray();

        if (words.Length == 0)
            return null;

        // Direct match: keyword is registered for this server and its lowercased name
        // is one of the message words. One row per entry → entry-count weighting.
        var directCandidates = await db.ChatKeywords
            .Where(k => words.Contains(k.Keyword.ToLower())
                     && db.ChatKeywordMaps.Any(m => m.ServerId == sid && m.Keyword == k.Keyword))
            .Select(k => k.Keyword)
            .ToListAsync();

        string? matched = directCandidates.Count > 0
            ? directCandidates[Random.Shared.Next(directCandidates.Count)]
            : null;

        if (matched is null)
        {
            var aliasCandidates = await db.ChatKeywordAliases
                .Where(a => a.ServerId == sid && words.Contains(a.Alias.ToLower()))
                .Select(a => a.Keyword)
                .ToListAsync();

            matched = aliasCandidates.Count > 0
                ? aliasCandidates[Random.Shared.Next(aliasCandidates.Count)]
                : null;
        }

        if (matched is null)
            return null;

        var entries = await db.ChatKeywords
            .Where(k => k.Keyword == matched)
            .Select(k => new { k.Id, k.FilePath, k.Nsfw })
            .ToListAsync();

        if (entries.Count == 0)
            return null;

        var chosen = entries[Random.Shared.Next(entries.Count)];
        return new ChatActionEntry(chosen.Id, chosen.FilePath, chosen.Nsfw, matched);
    }

    // ── UsersScheduledKeyword (recurring DM deliveries) ──────────────────────

    /// <summary>
    /// Schedules a keyword delivery for a user ~1 minute out (bumped to ~2 minutes if
    /// another row already occupies that clock minute), then returns the user's full
    /// schedule grouped by (keyword, time). Replaces <c>AddUsersScheduledKeyword</c>.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleSummary>> AddScheduleAsync(string userId, string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTime.Now;
        var candidate = now.AddMinutes(1);
        var minuteStart = new DateTime(candidate.Year, candidate.Month, candidate.Day,
                                       candidate.Hour, candidate.Minute, 0);
        var minuteEnd = minuteStart.AddMinutes(1);

        bool minuteTaken = await db.UsersScheduledKeywords
            .AnyAsync(u => u.ScheduledDateTime >= minuteStart && u.ScheduledDateTime < minuteEnd);

        var scheduledFor = minuteTaken ? now.AddMinutes(2) : candidate;

        db.UsersScheduledKeywords.Add(new UsersScheduledKeyword
        {
            UserId = userId,
            ChatKeyword = keyword,
            ScheduledDateTime = scheduledFor
        });
        await db.SaveChangesAsync();

        var rows = await db.UsersScheduledKeywords
            .Where(u => u.UserId == userId)
            .Select(u => new { u.ChatKeyword, u.ScheduledDateTime })
            .ToListAsync();

        return rows
            .GroupBy(r => new { r.ChatKeyword, r.ScheduledDateTime })
            .OrderBy(g => g.Key.ScheduledDateTime)
            .Select(g => new ScheduleSummary(
                g.Key.ScheduledDateTime,
                string.Join(", ", g.Select(x => x.ChatKeyword))))
            .ToList();
    }

    /// <summary>Cancels a user's scheduled delivery of one keyword. Replaces <c>DeleteUsersScheduledKeyword</c>.</summary>
    public async Task RemoveScheduleAsync(string userId, string keyword)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.UsersScheduledKeywords
            .Where(u => u.UserId == userId && u.ChatKeyword == keyword)
            .ExecuteDeleteAsync();
    }

    /// <summary>Every scheduled delivery configured for a user. Replaces <c>GetUsersScheduledKeywords</c>.</summary>
    public async Task<IReadOnlyList<UserScheduleEntry>> GetUserScheduleAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.UsersScheduledKeywords
            .Where(u => u.UserId == userId)
            .Select(u => new UserScheduleEntry(u.ChatKeyword, u.ScheduledDateTime))
            .ToListAsync();
    }

    /// <summary>
    /// Requeues all of a user's scheduled deliveries for ~1 minute out and returns a
    /// human-readable confirmation (or a "nothing scheduled" message). Replaces
    /// <c>UpdateUsersScheduledKeywordRequeue</c>.
    /// </summary>
    public async Task<string> RequeueScheduleAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var keywords = await db.UsersScheduledKeywords
            .Where(u => u.UserId == userId)
            .Select(u => u.ChatKeyword)
            .ToListAsync();

        if (keywords.Count == 0)
            return "This user does not have any scheduled thirsts to be sent out.";

        var newTime = DateTime.Now.AddMinutes(1);

        await db.UsersScheduledKeywords
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ScheduledDateTime, newTime));

        return $"The user was added successfully and the following keywords ({string.Join(", ", keywords)}) " +
               $"will be sent at {newTime:HH:mm:ss}";
    }

    /// <summary>
    /// Finds every due delivery, reschedules the affected users forward to a random
    /// slot tomorrow between 12:00 and 23:00, and returns what to send now. Replaces
    /// the (mutating) <c>GetUsersScheduledKeyword</c> procedure.
    ///
    /// Faithful to the procedure's quirks: one shared random time for the whole batch;
    /// <b>all</b> of a due user's rows are rescheduled, not just the due ones; keywords
    /// with no entries are silently skipped; and two hard-coded users get an extra
    /// "DOTO MONDAY" / "MATIKANEFUKUKITARU FRIDAY" delivery on those weekdays.
    /// </summary>
    public async Task<IReadOnlyList<DueKeywordDelivery>> GetDueDeliveriesAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTime.Now;

        var due = await db.UsersScheduledKeywords
            .Where(u => u.ScheduledDateTime <= now)
            .Select(u => new { u.UserId, u.ChatKeyword })
            .ToListAsync();

        if (due.Count == 0)
            return Array.Empty<DueKeywordDelivery>();

        // One shared random slot tomorrow, 12:00–23:00 (matches DATEADD(SECOND, RAND % 39600, noon)).
        var noonToday = now.Date.AddHours(12);
        const int windowSeconds = 11 * 60 * 60;
        var rescheduleTo = noonToday.AddDays(1).AddSeconds(Random.Shared.Next(windowSeconds));

        var dueUserIds = due.Select(d => d.UserId).Distinct().ToList();

        await db.UsersScheduledKeywords
            .Where(u => dueUserIds.Contains(u.UserId))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ScheduledDateTime, rescheduleTo));

        var results = new List<DueKeywordDelivery>();
        var entriesByKeyword = new Dictionary<string, List<(int Id, string Path)>>();

        foreach (var d in due)
        {
            if (!entriesByKeyword.TryGetValue(d.ChatKeyword, out var entries))
            {
                entries = (await db.ChatKeywords
                    .Where(k => k.Keyword == d.ChatKeyword)
                    .Select(k => new { k.Id, k.FilePath })
                    .ToListAsync())
                    .Select(k => (k.Id, k.FilePath))
                    .ToList();
                entriesByKeyword[d.ChatKeyword] = entries;
            }

            if (entries.Count == 0)
                continue; // CROSS APPLY drops keywords with no entries

            var chosen = entries[Random.Shared.Next(entries.Count)];
            results.Add(new DueKeywordDelivery(d.UserId, chosen.Path, d.ChatKeyword, chosen.Id));
        }

        AddWeekdaySpecials(now, dueUserIds, results);
        return results;
    }

    /// <summary>Hard-coded Monday/Friday bonus deliveries for two specific users (from <c>GetUsersScheduledKeyword</c>).</summary>
    private static void AddWeekdaySpecials(
        DateTime now, IReadOnlyCollection<string> dueUserIds, List<DueKeywordDelivery> results)
    {
        string[] specialUserIds = ["233611778351824896", "171369791486033920"];

        (string url, string label)? special = now.DayOfWeek switch
        {
            DayOfWeek.Monday => ("https://www.youtube.com/watch?v=QxCSQ0j-SFM", "DOTO MONDAY"),
            DayOfWeek.Friday => ("https://www.youtube.com/watch?v=MGxMxko9hww", "MATIKANEFUKUKITARU FRIDAY"),
            _ => null
        };

        if (special is not { } s)
            return;

        foreach (string uid in specialUserIds)
            if (dueUserIds.Contains(uid))
                results.Add(new DueKeywordDelivery(uid, s.url, s.label));
    }

    // ── Owner listing ────────────────────────────────────────────────────────

    /// <summary>
    /// Every scheduled delivery across all users, joined to the user's name and ordered
    /// by time. Replaces <c>GetScheduledEventUsers</c> — the join is on <c>UserID</c>
    /// alone, so a user in multiple servers still fans out to one row per server.
    /// </summary>
    public async Task<IReadOnlyList<ScheduledEventUser>> GetAllScheduledEventUsersAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await (
            from usk in db.UsersScheduledKeywords
            join u in db.Users on usk.UserId equals u.UserId
            orderby usk.ScheduledDateTime
            select new ScheduledEventUser(u.Username, usk.ChatKeyword, usk.ScheduledDateTime))
            .ToListAsync();
    }
}
