using DiscordBot.Data;
using DiscordBot.Helper;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Integration tests for the EF Core services that replaced the last 43 stored
/// procedures (audit, autorole, scheduling, AI chat history, word puzzle, server,
/// user, pronoun, music). Skipped automatically when the database is unavailable, so
/// CI stays green. Every test scopes its data to dedicated fake ids and cleans up in
/// a <c>finally</c> (or via <c>ExecuteDeleteAsync</c> directly for tables with no
/// delete method of their own, e.g. <c>Servers</c>), matching the pattern in
/// <see cref="KeywordServiceTests"/>.
/// </summary>
[Collection("Database")]
public sealed class CoreServicesTests : IClassFixture<DatabaseFixture>
{
    // Distinct fake-id range from KeywordServiceTests to avoid any collision.
    private const ulong TestGuildId = 999_999_999_999_999_881UL;
    private const string TestUserId = "999999999999999882";

    private readonly DatabaseFixture _db;
    private readonly IDbContextFactory<BigBirdContext> _factory;

    private readonly AutoRoleService _autoRoles;
    private readonly SchedulingService _scheduling;
    private readonly WordPuzzleService _wordPuzzles;
    private readonly AIMessageService _ai;
    private readonly ServerService _servers;
    private readonly UserService _users;
    private readonly PronounService _pronouns;
    private readonly MusicService _music;
    private readonly AuditService _audit;

    public CoreServicesTests(DatabaseFixture db)
    {
        _db = db;
        _factory = new Factory(db.ConnectionString);
        _autoRoles = new AutoRoleService(_factory);
        _scheduling = new SchedulingService(_factory);
        _wordPuzzles = new WordPuzzleService(_factory);
        _ai = new AIMessageService(_factory);
        _servers = new ServerService(_factory);
        _users = new UserService(_factory);
        _pronouns = new PronounService(_factory);
        _music = new MusicService(_factory);
        _audit = new AuditService(_factory);
    }

    private sealed class Factory(string connectionString) : IDbContextFactory<BigBirdContext>
    {
        private readonly DbContextOptions<BigBirdContext> _options =
            new DbContextOptionsBuilder<BigBirdContext>().UseNpgsql(connectionString).Options;

        public BigBirdContext CreateDbContext() => new(_options);
    }

    private async Task CleanupAsync()
    {
        await using var db = _factory.CreateDbContext();
        long gid = (long)TestGuildId;

        await db.GuildAutoRoles.Where(a => a.GuildId == gid).ExecuteDeleteAsync();
        await db.Reminders.Where(r => r.UserId == TestUserId).ExecuteDeleteAsync();
        await db.Birthdays.Where(b => b.BirthdayGuild == TestGuildId.ToString()).ExecuteDeleteAsync();
        await db.BotAiMessages.Where(m => m.UserId == TestUserId).ExecuteDeleteAsync();
        await db.PetWordPuzzles.Where(p => p.ChannelId == TestGuildId.ToString()).ExecuteDeleteAsync();
        await db.PlayerConnected.Where(p => p.ServerUid == gid).ExecuteDeleteAsync();
        await db.MusicQueue.Where(q => q.ServerUid == gid).ExecuteDeleteAsync();
        await db.Music.Where(m => m.ServerUid == gid).ExecuteDeleteAsync();
        await db.Users.Where(u => u.ServerUid == gid).ExecuteDeleteAsync();
        await db.Servers.Where(s => s.ServerUid == gid).ExecuteDeleteAsync();
    }

    // ── AutoRole ─────────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task AutoRole_Set_Get_Clear_RoundTrips()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            Assert.Null(await _autoRoles.GetRoleIdAsync(TestGuildId));

            await _autoRoles.SetAsync(TestGuildId, 111);
            Assert.Equal(111UL, await _autoRoles.GetRoleIdAsync(TestGuildId));

            await _autoRoles.SetAsync(TestGuildId, 222); // replace, not duplicate
            Assert.Equal(222UL, await _autoRoles.GetRoleIdAsync(TestGuildId));

            await _autoRoles.ClearAsync(TestGuildId);
            Assert.Null(await _autoRoles.GetRoleIdAsync(TestGuildId));
        }
        finally { await CleanupAsync(); }
    }

    // ── Scheduling (reminders / birthdays) ──────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Reminder_DueLookup_MarksSentAndReturnsOnce()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            await _scheduling.AddReminderAsync(TestUserId, "zztest reminder", DateTime.UtcNow.AddMinutes(-1));

            var due = (await _scheduling.GetDueRemindersAsync()).Where(r => r.UserId == TestUserId).ToList();
            Assert.Single(due);
            Assert.Equal("zztest reminder", due[0].Message);

            // Second call must not return the same (now-sent) reminder again.
            due = (await _scheduling.GetDueRemindersAsync()).Where(r => r.UserId == TestUserId).ToList();
            Assert.Empty(due);
        }
        finally { await CleanupAsync(); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Birthday_AddsNineYearRows_AndTodaysLookupMarksSent()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            var today = DateTime.Now.Date;
            await _scheduling.AddBirthdayAsync(today, "@zztest", TestGuildId.ToString(), null);

            await using (var db = _factory.CreateDbContext())
            {
                int rowCount = await db.Birthdays.CountAsync(b => b.BirthdayGuild == TestGuildId.ToString());
                Assert.Equal(9, rowCount); // this year through +8
            }

            var due = (await _scheduling.GetTodaysBirthdaysAsync())
                .Where(b => b.GuildId == TestGuildId.ToString()).ToList();
            Assert.Single(due); // only this year's row matches today's date
            Assert.Equal("@zztest", due[0].Mention);
        }
        finally { await CleanupAsync(); }
    }

    // ── Word puzzle ──────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task WordPuzzle_Post_Active_Claim_Status_RoundTrip()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string channel = TestGuildId.ToString();
        try
        {
            Assert.Null(await _wordPuzzles.GetActivePuzzleAsync(channel));

            await _wordPuzzles.AddPuzzleAsync(channel, "zzword", DateTime.UtcNow.AddMinutes(30));

            var active = await _wordPuzzles.GetActivePuzzleAsync(channel);
            Assert.NotNull(active);
            Assert.Equal("zzword", active!.Word);

            Assert.Equal(false, await _wordPuzzles.GetClaimedStatusAsync(channel));

            await _wordPuzzles.ClaimPuzzleAsync(active.PuzzleId);

            Assert.Null(await _wordPuzzles.GetActivePuzzleAsync(channel)); // claimed → no longer active
            Assert.Equal(true, await _wordPuzzles.GetClaimedStatusAsync(channel));
        }
        finally { await CleanupAsync(); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetRandomWord_ReturnsNonEmptyWord()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string? word = await _wordPuzzles.GetRandomWordAsync();
        Assert.False(string.IsNullOrWhiteSpace(word));
    }

    // ── AI chat history ──────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task AiHistory_Add_Get_Delete_RoundTrips()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        string serverUid = TestGuildId.ToString();
        string channelId = "zzchannel";
        try
        {
            await _ai.AddMessageAsync(TestUserId, serverUid, channelId, "user", "hello");
            await _ai.AddMessageAsync(TestUserId, serverUid, channelId, "assistant", "hi there");

            var history = await _ai.GetHistoryAsync(TestUserId, serverUid, channelId);
            Assert.Equal(2, history.Count);
            Assert.Equal(("user", "hello"), history[0]);
            Assert.Equal(("assistant", "hi there"), history[1]);

            await _ai.DeleteHistoryAsync(TestUserId, serverUid, channelId);
            Assert.Empty(await _ai.GetHistoryAsync(TestUserId, serverUid, channelId));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseImageDetectionResult_ExtractsStatusAndPercentage()
    {
        var (status, pct) = AIMessageService.ParseImageDetectionResult(
            """{"status":"success","type":{"ai_generated":"0.87"}}""");

        Assert.Equal("success", status);
        Assert.Equal(87.0, pct);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseImageDetectionResult_MissingField_ReturnsNullPercentage()
    {
        var (status, pct) = AIMessageService.ParseImageDetectionResult("""{"status":"failure"}""");

        Assert.Equal("failure", status);
        Assert.Null(pct);
    }

    // ── Servers / Users / Pronouns ───────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Server_Add_Get_Toggle_RoundTrips()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            Assert.Null(await _servers.GetServerInfoAsync(TestGuildId));

            await _servers.AddServerAsync(TestGuildId, "zztest-server", 12345);
            await _servers.AddServerAsync(TestGuildId, "should-not-overwrite", 99999); // idempotent

            var info = await _servers.GetServerInfoAsync(TestGuildId);
            Assert.NotNull(info);
            Assert.Equal("zztest-server", info!.ServerName); // first insert wins, matches AddServer's IF NOT EXISTS
            Assert.True(info.IsActive);
            Assert.False(info.AnnouncementsEnabled);

            var toggled = await _servers.ToggleAnnouncementsAsync(TestGuildId, 55555);
            Assert.NotNull(toggled);
            Assert.True(toggled!.Enabled);

            var afterToggle = await _servers.GetServerInfoAsync(TestGuildId);
            Assert.True(afterToggle!.AnnouncementsEnabled);
            Assert.Equal("55555", afterToggle.DefaultChannelId);

            Assert.False(await _servers.GetEmbedFixEnabledAsync(TestGuildId));
            string? embedResult = await _servers.ToggleEmbedFixAsync(TestGuildId);
            Assert.NotNull(embedResult);
            Assert.True(await _servers.GetEmbedFixEnabledAsync(TestGuildId));

            var active = await _servers.GetActiveServersAsync();
            Assert.Contains(active, s => s.ServerUid == TestGuildId);
        }
        finally { await CleanupAsync(); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task User_AddIfMissing_IsInsertOnly_AndDeleteCascadesWhenLastRow()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            await _users.AddUserIfMissingAsync(TestUserId, "OriginalName", DateTime.UtcNow, TestGuildId, "OrigNick");
            await _users.AddUserIfMissingAsync(TestUserId, "ChangedName", DateTime.UtcNow, TestGuildId, "NewNick");

            await using (var db = _factory.CreateDbContext())
            {
                var row = await db.Users.SingleAsync(u => u.UserId == TestUserId && u.ServerUid == (long)TestGuildId);
                Assert.Equal("OriginalName", row.Username); // second call was a no-op, matching AddUser's IF NOT EXISTS
            }

            await _ai.AddMessageAsync(TestUserId, TestGuildId.ToString(), "c", "user", "will be cascade-deleted");

            await _users.DeleteUserAsync(TestUserId, TestGuildId);

            await using (var db = _factory.CreateDbContext())
            {
                Assert.False(await db.Users.AnyAsync(u => u.UserId == TestUserId));
                Assert.False(await db.BotAiMessages.AnyAsync(m => m.UserId == TestUserId)); // cascaded: was their only row
            }
        }
        finally { await CleanupAsync(); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Pronouns_ReturnsNonEmptyReferenceList()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        var all = await _pronouns.GetAllAsync();
        Assert.NotEmpty(all);
    }

    // ── Music ────────────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Music_PlayerConnected_Volume_And_MusicQueue_RoundTrip()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            await _servers.AddServerAsync(TestGuildId, "zztest-music-server", 1);

            Assert.Empty(await _music.GetConnectedPlayersAsync());
            await _music.AddPlayerConnectedAsync(TestGuildId, 10, 20, TestUserId);
            await _music.AddPlayerConnectedAsync(TestGuildId, 10, 20, TestUserId); // idempotent

            var connected = await _music.GetConnectedPlayersAsync();
            Assert.Single(connected, c => c.ServerUid == TestGuildId);

            Assert.Equal(100, await _music.GetVolumeAsync(TestGuildId)); // AddServer's default
            await _music.UpdateVolumeAsync(TestGuildId, 42);
            Assert.Equal(42, await _music.GetVolumeAsync(TestGuildId));

            await _music.AddMusicAsync(TestGuildId, "vid1", "author", "title", "https://example.com/zztest", TestUserId);
            var queue = await _music.GetQueueAsync(TestGuildId);
            Assert.Single(queue);
            Assert.Equal("https://example.com/zztest", queue[0].Url);

            await _music.DeleteQueueEntryAsync("https://example.com/zztest");
            Assert.Empty(await _music.GetQueueAsync(TestGuildId));

            await _music.DeletePlayerConnectedAsync(TestGuildId);
            Assert.DoesNotContain(await _music.GetConnectedPlayersAsync(), c => c.ServerUid == TestGuildId);
        }
        finally { await CleanupAsync(); }
    }

    // ── Audit (write-only — just verify it doesn't throw and actually inserts) ─

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Audit_EveryInsertMethod_WritesARow()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        try
        {
            await _audit.InsertAuditAsync("zztest-command", TestUserId, (long)TestGuildId);
            await _audit.InsertUserJoinedAuditAsync(ulong.Parse(TestUserId), TestGuildId);
            await _audit.InsertUserLeftAuditAsync(ulong.Parse(TestUserId), TestGuildId);
            await _audit.InsertButtonAuditAsync("zzbtn", ulong.Parse(TestUserId), TestGuildId);
            await _audit.InsertGuildJoinedAuditAsync(TestGuildId, "zztest-guild");
            await _audit.InsertReactionAuditAsync("👍", 1, ulong.Parse(TestUserId), TestGuildId);
            await _audit.InsertGameTriggerAuditAsync("petpuzzle", ulong.Parse(TestUserId), TestGuildId);

            await using var db = _factory.CreateDbContext();
            long gid = (long)TestGuildId;
            Assert.True(await db.AuditLog.AnyAsync(a => a.ServerUid == gid && a.Command == "zztest-command"));
            Assert.True(await db.AuditUserJoined.AnyAsync(a => a.ServerUid == gid));
            Assert.True(await db.AuditUserLeft.AnyAsync(a => a.ServerUid == gid));
            Assert.True(await db.AuditButtonExecuted.AnyAsync(a => a.ServerUid == gid));
            Assert.True(await db.AuditGuildJoined.AnyAsync(a => a.ServerUid == gid));
            Assert.True(await db.AuditReactionAdded.AnyAsync(a => a.ChannelUid == gid));
            Assert.True(await db.AuditGameTrigger.AnyAsync(a => a.ServerUid == gid));

            await db.AuditLog.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
            await db.AuditUserJoined.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
            await db.AuditUserLeft.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
            await db.AuditButtonExecuted.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
            await db.AuditGuildJoined.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
            await db.AuditReactionAdded.Where(a => a.ChannelUid == gid).ExecuteDeleteAsync();
            await db.AuditGameTrigger.Where(a => a.ServerUid == gid).ExecuteDeleteAsync();
        }
        finally { await CleanupAsync(); }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Audit_SkipsLoggingForOwnerId()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);
        const string ownerId = "171369791486033920";

        await _audit.InsertAuditAsync("zztest-owner-command", ownerId, (long)TestGuildId);

        await using var db = _factory.CreateDbContext();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ServerUid == (long)TestGuildId && a.Command == "zztest-owner-command"));
    }
}
