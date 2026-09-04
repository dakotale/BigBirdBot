using DiscordBot.Data;
using DiscordBot.Helper;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="KeywordService"/> (EF Core over SQL Server).
/// Skipped automatically when the database is unavailable, so CI stays green.
///
/// Every test scopes its data to a dedicated fake server / user id and a
/// <c>zztest_</c> keyword prefix, and cleans up in a <c>finally</c>, so a real
/// database is safe to run these against.
/// </summary>
[Collection("Database")]
public sealed class KeywordServiceTests : IClassFixture<DatabaseFixture>
{
    private const ulong TestServerId = 999_999_999_999_999_991UL;
    private const string TestUserId = "999999999999999992";

    private readonly DatabaseFixture _db;
    private readonly KeywordService _svc;
    private readonly IDbContextFactory<BigBirdContext> _factory;

    public KeywordServiceTests(DatabaseFixture db)
    {
        _db = db;
        _factory = new Factory(db.ConnectionString);
        _svc = new KeywordService(_factory);
    }

    private sealed class Factory(string connectionString) : IDbContextFactory<BigBirdContext>
    {
        private readonly DbContextOptions<BigBirdContext> _options =
            new DbContextOptionsBuilder<BigBirdContext>().UseSqlServer(connectionString).Options;

        public BigBirdContext CreateDbContext() => new(_options);
    }

    private static string NewKeyword() => "zztest_" + Guid.NewGuid().ToString("N")[..12];

    private async Task CleanupAsync(params string[] keywordsToRemove)
    {
        await using var db = _factory.CreateDbContext();
        foreach (var kw in keywordsToRemove)
        {
            string add = "add" + kw;
            await db.ChatKeywords.Where(k => k.Keyword == kw).ExecuteDeleteAsync();
            await db.ChatKeywordMaps.Where(m => m.AddKeyword == add).ExecuteDeleteAsync();
            await db.ChatKeywordAliases.Where(a => a.Keyword == kw).ExecuteDeleteAsync();
        }
        await db.ChatKeywordAliases.Where(a => a.ServerId == (long)TestServerId).ExecuteDeleteAsync();
        await db.UsersScheduledKeywords.Where(u => u.UserId == TestUserId).ExecuteDeleteAsync();
        await db.ChatKeywordMaps.Where(m => m.ServerId == (long)TestServerId).ExecuteDeleteAsync();
    }

    // ── ChatKeyword entries ──────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task AddEntry_RoundTrips_ThroughRecentAndInfo()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester");
            await _svc.AddEntryAsync(kw, "https://example.com/one");
            await _svc.AddEntryAsync(kw, "https://example.com/two");

            var recent = await _svc.GetRecentEntriesAsync(kw);
            Assert.Equal(2, recent.Count);
            Assert.Equal("https://example.com/two", recent[0]); // newest first

            var info = await _svc.GetInfoAsync(kw);
            Assert.NotNull(info);
            Assert.Equal(2, info!.EntryCount);
            Assert.Equal("tester", info.CreatedBy);
        }
        finally { await CleanupAsync(kw); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task AddEntry_StripsSingleQuotes()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            await _svc.AddEntryAsync(kw, "  it's a 'path'  ");
            var recent = await _svc.GetRecentEntriesAsync(kw);
            Assert.Single(recent);
            Assert.Equal("its a path", recent[0]);
        }
        finally { await CleanupAsync(kw); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Nsfw_MarkThenGet()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        string url = $"https://example.com/{kw}/pic.png";
        try
        {
            await _svc.AddEntryAsync(kw, url);
            Assert.False(await _svc.GetNsfwAsync(url));

            Assert.True(await _svc.MarkNsfwAsync(url));
            Assert.True(await _svc.GetNsfwAsync(url));

            // No entry matches this text at all.
            Assert.Null(await _svc.GetNsfwAsync("no-entry-has-this-text"));
        }
        finally { await CleanupAsync(kw); }
    }

    // ── ChatKeywordMap ───────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task AddMap_IsIdempotent_AndComputesKeyword()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester");
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester"); // second call must be a no-op

            var list = await _svc.GetKeywordsForServerAsync(TestServerId);
            var mine = list.Where(x => x.AddKeyword == "add" + kw).ToList();
            Assert.Single(mine);
            Assert.Equal(kw, mine[0].Keyword); // computed column: replace("add"+kw, "add", "")

            Assert.Equal(kw, await _svc.ResolveAddKeywordAsync("add" + kw));
            Assert.Null(await _svc.ResolveAddKeywordAsync("addnothing_" + kw));
        }
        finally { await CleanupAsync(kw); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Rename_MovesMapAndEntries()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string oldKw = NewKeyword();
        string newKw = NewKeyword();
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + oldKw, "tester");
            await _svc.AddEntryAsync(oldKw, "https://example.com/x");

            await _svc.RenameKeywordAsync(oldKw, newKw, TestServerId);

            Assert.Equal(newKw, await _svc.ResolveAddKeywordAsync("add" + newKw));
            Assert.Null(await _svc.ResolveAddKeywordAsync("add" + oldKw));

            var recent = await _svc.GetRecentEntriesAsync(newKw);
            Assert.Single(recent);
            Assert.Empty(await _svc.GetRecentEntriesAsync(oldKw));
        }
        finally { await CleanupAsync(oldKw, newKw); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task DeleteKeyword_RemovesMapEntriesAndSchedule()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester");
            await _svc.AddEntryAsync(kw, "https://example.com/x");
            await _svc.AddScheduleAsync(TestUserId, kw);

            await _svc.DeleteKeywordAsync(kw);

            Assert.Null(await _svc.ResolveAddKeywordAsync("add" + kw));
            Assert.Empty(await _svc.GetRecentEntriesAsync(kw));
            Assert.Empty(await _svc.GetUserScheduleAsync(TestUserId));
        }
        finally { await CleanupAsync(kw); }
    }

    // ── Aliases ──────────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Alias_Add_Duplicate_ListAndDelete()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        string alias = "zzalias_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Assert.True(await _svc.AddAliasAsync(alias, kw, TestServerId, "tester"));
            Assert.False(await _svc.AddAliasAsync(alias, kw, TestServerId, "tester")); // dup

            var aliases = await _svc.GetAliasesAsync(kw, TestServerId);
            Assert.Single(aliases);
            Assert.Equal(alias, aliases[0].Alias);

            await _svc.DeleteAliasAsync(alias, TestServerId);
            Assert.Empty(await _svc.GetAliasesAsync(kw, TestServerId));
        }
        finally { await CleanupAsync(kw); }
    }

    // ── GetChatAction ────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ResolveChatAction_DirectMatch_AliasMatch_AndMiss()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        string alias = "zzalias_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester");
            await _svc.AddEntryAsync(kw, "https://example.com/hit");
            await _svc.AddAliasAsync(alias, kw, TestServerId, "tester");

            var direct = await _svc.ResolveChatActionAsync(TestServerId, $"hey look a {kw} here");
            Assert.NotNull(direct);
            Assert.Equal(kw, direct!.Keyword);
            Assert.Equal("https://example.com/hit", direct.FilePath);

            var viaAlias = await _svc.ResolveChatActionAsync(TestServerId, $"try {alias} now");
            Assert.NotNull(viaAlias);
            Assert.Equal(kw, viaAlias!.Keyword);

            Assert.Null(await _svc.ResolveChatActionAsync(TestServerId, "nothing relevant here"));
            // Registered in a different server → no match.
            Assert.Null(await _svc.ResolveChatActionAsync(TestServerId + 1, $"a {kw} b"));
        }
        finally { await CleanupAsync(kw); }
    }

    // ── Scheduling ───────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Schedule_Add_List_Requeue_Remove()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            var summaries = await _svc.AddScheduleAsync(TestUserId, kw);
            Assert.Contains(summaries, s => s.KeywordsCsv.Contains(kw));

            var schedule = await _svc.GetUserScheduleAsync(TestUserId);
            Assert.Single(schedule);
            Assert.Equal(kw, schedule[0].Keyword);

            string msg = await _svc.RequeueScheduleAsync(TestUserId);
            Assert.Contains(kw, msg);

            await _svc.RemoveScheduleAsync(TestUserId, kw);
            Assert.Empty(await _svc.GetUserScheduleAsync(TestUserId));

            Assert.Equal(
                "This user does not have any scheduled thirsts to be sent out.",
                await _svc.RequeueScheduleAsync(TestUserId));
        }
        finally { await CleanupAsync(kw); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetDueDeliveries_ReturnsDueRow_AndReschedulesForward()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string kw = NewKeyword();
        try
        {
            await _svc.AddMapAsync(TestServerId, "add" + kw, "tester");
            await _svc.AddEntryAsync(kw, "https://example.com/due");

            // Force a due row in the past.
            await using (var db = _factory.CreateDbContext())
            {
                db.UsersScheduledKeywords.Add(new UsersScheduledKeyword
                {
                    UserId = TestUserId,
                    ChatKeyword = kw,
                    ScheduledDateTime = DateTime.Now.AddHours(-1)
                });
                await db.SaveChangesAsync();
            }

            var due = await _svc.GetDueDeliveriesAsync();
            var mine = due.Where(d => d.UserId == TestUserId && d.Keyword == kw).ToList();
            Assert.Single(mine);
            Assert.Equal("https://example.com/due", mine[0].FilePath);

            // Row must now be rescheduled to tomorrow between 12:00 and 23:00.
            var when = (await _svc.GetUserScheduleAsync(TestUserId)).Single().ScheduleTime;
            Assert.True(when > DateTime.Now, "rescheduled time should be in the future");
            Assert.InRange(when.Hour, 12, 23);
        }
        finally { await CleanupAsync(kw); }
    }
}
