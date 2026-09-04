using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Data;

/// <summary>
/// EF Core context for BigBirdBot, introduced with the keyword feature area and later
/// expanded to cover every remaining stored-procedure-backed table. Originally targeted
/// SQL Server; migrated onto PostgreSQL (see <c>SQL/Database/postgres/001_InitialSchema.sql</c>)
/// so the bot can run cross-platform.
///
/// The database already exists and is managed outside EF Core (hand-written schema/
/// migration scripts under <c>SQL/Database</c>), so there are <b>no EF migrations</b> —
/// every entity is mapped explicitly to its existing table and columns here.
///
/// Registered via <c>AddDbContextFactory</c> (see <c>Program.ConfigureServices</c>):
/// the factory is a singleton and each unit of work creates and disposes its own
/// context, which suits a Discord bot with no per-request scope. Every insert/update
/// below sets its own timestamp columns explicitly in C# (<c>DateTime.Now</c> or
/// <c>DateTime.UtcNow</c>, matching each source procedure's <c>GETDATE()</c> vs
/// <c>GETUTCDATE()</c>/<c>SYSUTCDATETIME()</c> choice) rather than relying on the
/// database's column defaults, since every column those defaults would have filled is
/// otherwise omitted from its owning procedure's INSERT list anyway.
/// </summary>
public sealed class BigBirdContext(DbContextOptions<BigBirdContext> options) : DbContext(options)
{
    // Every timestamp column in the schema is "timestamp without time zone". Npgsql's
    // default (post-6.0) maps DateTime to "timestamp with time zone" and demands Kind=Utc,
    // so this restores the legacy mapping. Must run before any Npgsql type mapping is
    // initialized, hence set in this type's static constructor rather than in Program.cs.
    static BigBirdContext() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    // ── Keywords ─────────────────────────────────────────────────────────────
    public DbSet<ChatKeyword> ChatKeywords => Set<ChatKeyword>();
    public DbSet<ChatKeywordMap> ChatKeywordMaps => Set<ChatKeywordMap>();
    public DbSet<ChatKeywordAlias> ChatKeywordAliases => Set<ChatKeywordAlias>();
    public DbSet<UsersScheduledKeyword> UsersScheduledKeywords => Set<UsersScheduledKeyword>();

    // ── Users / Servers ──────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<Pronoun> Pronouns => Set<Pronoun>();

    // ── Audit ────────────────────────────────────────────────────────────────
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<AuditButtonExecuted> AuditButtonExecuted => Set<AuditButtonExecuted>();
    public DbSet<AuditGuildJoined> AuditGuildJoined => Set<AuditGuildJoined>();
    public DbSet<AuditReactionAdded> AuditReactionAdded => Set<AuditReactionAdded>();
    public DbSet<AuditUserJoined> AuditUserJoined => Set<AuditUserJoined>();
    public DbSet<AuditUserLeft> AuditUserLeft => Set<AuditUserLeft>();
    public DbSet<AuditGameTrigger> AuditGameTrigger => Set<AuditGameTrigger>();

    // ── AutoRole ─────────────────────────────────────────────────────────────
    public DbSet<GuildAutoRole> GuildAutoRoles => Set<GuildAutoRole>();

    // ── Scheduling (reminders / birthdays) ──────────────────────────────────
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Birthday> Birthdays => Set<Birthday>();

    // ── AI chat history ──────────────────────────────────────────────────────
    public DbSet<BotAiMessage> BotAiMessages => Set<BotAiMessage>();

    // ── Bonus word puzzle ────────────────────────────────────────────────────
    public DbSet<PetWordPuzzle> PetWordPuzzles => Set<PetWordPuzzle>();
    public DbSet<Word> Words => Set<Word>();

    // ── Music / audio ────────────────────────────────────────────────────────
    public DbSet<MusicHistoryEntry> Music => Set<MusicHistoryEntry>();
    public DbSet<MusicQueueEntry> MusicQueue => Set<MusicQueueEntry>();
    public DbSet<PlayerConnected> PlayerConnected => Set<PlayerConnected>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatKeyword>(e =>
        {
            e.ToTable("ChatKeyword");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Keyword).HasColumnName("ChatKeyword").HasMaxLength(50);
            e.Property(x => x.FilePath).HasColumnName("FilePath");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.Nsfw).HasColumnName("NSFW");
        });

        modelBuilder.Entity<ChatKeywordMap>(e =>
        {
            e.ToTable("ChatKeywordMap");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.AddKeyword).HasColumnName("AddKeyword").HasMaxLength(50);
            e.Property(x => x.ServerId).HasColumnName("ServerID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
            // Computed in the database; EF reads/filters it but never writes it.
            e.Property(x => x.Keyword).HasColumnName("Keyword")
                .HasComputedColumnSql("replace(\"AddKeyword\",'add','')");
        });

        modelBuilder.Entity<ChatKeywordAlias>(e =>
        {
            e.ToTable("ChatKeywordAlias");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Alias).HasColumnName("Alias").HasMaxLength(50);
            e.Property(x => x.Keyword).HasColumnName("Keyword").HasMaxLength(50);
            e.Property(x => x.ServerId).HasColumnName("ServerID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });

        modelBuilder.Entity<UsersScheduledKeyword>(e =>
        {
            e.ToTable("UsersScheduledKeyword");
            // No key column in the table; the row is uniquely identified by all three.
            e.HasKey(x => new { x.UserId, x.ChatKeyword, x.ScheduledDateTime });
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.ChatKeyword).HasColumnName("ChatKeyword").HasMaxLength(100);
            e.Property(x => x.ScheduledDateTime).HasColumnName("ScheduledDateTime").HasColumnType("timestamp");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => new { x.UserId, x.ServerUid });
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.Username).HasColumnName("Username").HasMaxLength(200);
            e.Property(x => x.JoinDate).HasColumnName("JoinDate").HasColumnType("timestamp");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.Nickname).HasColumnName("Nickname").HasMaxLength(200);
            e.Property(x => x.PronounId).HasColumnName("PronounID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.DeletedOn).HasColumnName("DeletedOn").HasColumnType("timestamp");
            e.Property(x => x.LastSeen).HasColumnName("LastSeen").HasColumnType("timestamp");
        });

        modelBuilder.Entity<Server>(e =>
        {
            e.ToTable("Servers");
            e.HasKey(x => x.ServerId);
            e.Property(x => x.ServerId).HasColumnName("ServerID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.ServerName).HasColumnName("ServerName").HasMaxLength(200);
            e.Property(x => x.DefaultChannelId).HasColumnName("DefaultChannelID");
            e.Property(x => x.Volume).HasColumnName("Volume");
            e.Property(x => x.FixEmbed).HasColumnName("FixEmbed");
            e.Property(x => x.IsPlayerConnected).HasColumnName("IsPlayerConnected");
            e.Property(x => x.IsActive).HasColumnName("IsActive");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.AnnouncementsEnabled).HasColumnName("AnnouncementsEnabled");
        });

        modelBuilder.Entity<Pronoun>(e =>
        {
            e.ToTable("Pronouns");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.PronounText).HasColumnName("Pronoun").HasMaxLength(100);
        });

        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.ToTable("AuditLog");
            e.HasKey(x => x.AuditLogId);
            e.Property(x => x.AuditLogId).HasColumnName("AuditLogID");
            e.Property(x => x.Command).HasColumnName("Command").HasMaxLength(50);
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });

        modelBuilder.Entity<AuditButtonExecuted>(e =>
        {
            e.ToTable("AuditButtonExecuted");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ButtonId).HasColumnName("ButtonID").HasMaxLength(100);
            e.Property(x => x.UserUid).HasColumnName("UserUID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.ExecutedOn).HasColumnName("ExecutedOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<AuditGuildJoined>(e =>
        {
            e.ToTable("AuditGuildJoined");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.ServerName).HasColumnName("ServerName").HasMaxLength(100);
            e.Property(x => x.JoinedOn).HasColumnName("JoinedOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<AuditReactionAdded>(e =>
        {
            e.ToTable("AuditReactionAdded");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Emoji).HasColumnName("Emoji").HasMaxLength(50);
            e.Property(x => x.MessageUid).HasColumnName("MessageUID");
            e.Property(x => x.UserUid).HasColumnName("UserUID");
            e.Property(x => x.ChannelUid).HasColumnName("ChannelUID");
            e.Property(x => x.AddedOn).HasColumnName("AddedOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<AuditUserJoined>(e =>
        {
            e.ToTable("AuditUserJoined");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.UserUid).HasColumnName("UserUID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.JoinedOn).HasColumnName("JoinedOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<AuditUserLeft>(e =>
        {
            e.ToTable("AuditUserLeft");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.UserUid).HasColumnName("UserUID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.LeftOn).HasColumnName("LeftOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<AuditGameTrigger>(e =>
        {
            e.ToTable("AuditGameTrigger");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Game).HasColumnName("Game").HasMaxLength(50);
            e.Property(x => x.UserUid).HasColumnName("UserUID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.TriggeredOn).HasColumnName("TriggeredOn").HasColumnType("timestamp");
        });

        modelBuilder.Entity<GuildAutoRole>(e =>
        {
            e.ToTable("GuildAutoRole");
            e.HasKey(x => x.GuildId);
            e.Property(x => x.GuildId).HasColumnName("GuildId");
            e.Property(x => x.RoleId).HasColumnName("RoleId");
            e.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2");
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.ToTable("Reminders");
            e.HasKey(x => x.ReminderId);
            e.Property(x => x.ReminderId).HasColumnName("ReminderID");
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.Message).HasColumnName("Message").HasMaxLength(1000);
            e.Property(x => x.RemindAtUtc).HasColumnName("RemindAtUtc").HasColumnType("timestamp");
            e.Property(x => x.Sent).HasColumnName("Sent");
        });

        modelBuilder.Entity<Birthday>(e =>
        {
            e.ToTable("Birthday");
            e.HasKey(x => x.BirthdayId);
            e.Property(x => x.BirthdayId).HasColumnName("BirthdayID");
            e.Property(x => x.BirthdayDate).HasColumnName("BirthdayDate").HasColumnType("timestamp");
            e.Property(x => x.BirthdayUser).HasColumnName("BirthdayUser").HasMaxLength(200);
            e.Property(x => x.BirthdayGuild).HasColumnName("BirthdayGuild").HasMaxLength(200);
            e.Property(x => x.Sent).HasColumnName("Sent");
            e.Property(x => x.BirthdayChannel).HasColumnName("BirthdayChannel").HasMaxLength(200);
        });

        modelBuilder.Entity<BotAiMessage>(e =>
        {
            e.ToTable("BotAIMessage");
            e.HasKey(x => x.BotAiMessageId);
            e.Property(x => x.BotAiMessageId).HasColumnName("BotAIMessageID");
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.ServerUid).HasColumnName("ServerUID").HasMaxLength(50);
            e.Property(x => x.ChatRole).HasColumnName("ChatRole").HasMaxLength(10);
            e.Property(x => x.ChatMessage).HasColumnName("ChatMessage");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.ChannelId).HasColumnName("ChannelID").HasMaxLength(50);
        });

        modelBuilder.Entity<PetWordPuzzle>(e =>
        {
            e.ToTable("PetWordPuzzle");
            e.HasKey(x => x.PuzzleId);
            e.Property(x => x.PuzzleId).HasColumnName("PuzzleID");
            e.Property(x => x.ChannelId).HasColumnName("ChannelID").HasMaxLength(50);
            e.Property(x => x.Word).HasColumnName("Word").HasMaxLength(100);
            e.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamp");
            e.Property(x => x.Claimed).HasColumnName("Claimed");
        });

        modelBuilder.Entity<Word>(e =>
        {
            e.ToTable("Words");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Text).HasColumnName("Word").HasMaxLength(10);
        });

        modelBuilder.Entity<MusicHistoryEntry>(e =>
        {
            e.ToTable("Music");
            e.HasKey(x => x.MusicId);
            e.Property(x => x.MusicId).HasColumnName("MusicID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.VideoId).HasColumnName("VideoID").HasMaxLength(200);
            e.Property(x => x.Author).HasColumnName("Author").HasMaxLength(200);
            e.Property(x => x.Title).HasColumnName("Title").HasMaxLength(400);
            e.Property(x => x.Url).HasColumnName("URL").HasMaxLength(1000);
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });

        modelBuilder.Entity<MusicQueueEntry>(e =>
        {
            e.ToTable("MusicQueue");
            e.HasKey(x => x.MusicQueueId);
            e.Property(x => x.MusicQueueId).HasColumnName("MusicQueueID");
            e.Property(x => x.MusicId).HasColumnName("MusicID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.VoiceChannelId).HasColumnName("VoiceChannelID");
            e.Property(x => x.TextChannelId).HasColumnName("TextChannelID");
            e.Property(x => x.Url).HasColumnName("URL").HasMaxLength(1000);
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });

        modelBuilder.Entity<PlayerConnected>(e =>
        {
            e.ToTable("PlayerConnected");
            e.HasKey(x => x.PlayerId);
            e.Property(x => x.PlayerId).HasColumnName("PlayerID");
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
            e.Property(x => x.VoiceChannelId).HasColumnName("VoiceChannelID");
            e.Property(x => x.TextChannelId).HasColumnName("TextChannelID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });
    }

    // The app sets timestamp columns with a mix of DateTime.Now and DateTime.UtcNow
    // (matching each source procedure's original GETDATE() vs GETUTCDATE() choice), and
    // SQL Server's "datetime" stored either one the same way, ignoring Kind entirely.
    // Postgres's "timestamp without time zone" is stricter and rejects Kind=Utc, so this
    // strips Kind on every DateTime property (both directions) to reproduce that
    // SQL Server behavior instead of hunting down every call site.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UnspecifiedKindConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UnspecifiedKindNullableConverter>();
    }

    private sealed class UnspecifiedKindConverter : ValueConverter<DateTime, DateTime>
    {
        public UnspecifiedKindConverter() : base(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified))
        {
        }
    }

    private sealed class UnspecifiedKindNullableConverter : ValueConverter<DateTime?, DateTime?>
    {
        public UnspecifiedKindNullableConverter() : base(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v)
        {
        }
    }
}
