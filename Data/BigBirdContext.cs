using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Data;

/// <summary>
/// EF Core context for BigBirdBot, introduced with the keyword feature area.
///
/// The bot's SQL Server database already exists and is managed outside EF Core
/// (SSMS scripts + hand-written migration files under <c>SQL/Database</c>), so there
/// are <b>no EF migrations</b> — every entity is mapped explicitly to its existing
/// table and columns here. New feature areas add their tables to this context as
/// they are moved off stored procedures.
///
/// Registered via <c>AddDbContextFactory</c> (see <c>Program.ConfigureServices</c>):
/// the factory is a singleton and each unit of work creates and disposes its own
/// context, which suits a Discord bot with no per-request scope.
/// </summary>
public sealed class BigBirdContext(DbContextOptions<BigBirdContext> options) : DbContext(options)
{
    public DbSet<ChatKeyword> ChatKeywords => Set<ChatKeyword>();
    public DbSet<ChatKeywordMap> ChatKeywordMaps => Set<ChatKeywordMap>();
    public DbSet<ChatKeywordAlias> ChatKeywordAliases => Set<ChatKeywordAlias>();
    public DbSet<UsersScheduledKeyword> UsersScheduledKeywords => Set<UsersScheduledKeyword>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatKeyword>(e =>
        {
            e.ToTable("ChatKeyword");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Keyword).HasColumnName("ChatKeyword").HasMaxLength(50);
            e.Property(x => x.FilePath).HasColumnName("FilePath");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("datetime");
            e.Property(x => x.Nsfw).HasColumnName("NSFW");
        });

        modelBuilder.Entity<ChatKeywordMap>(e =>
        {
            e.ToTable("ChatKeywordMap");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.AddKeyword).HasColumnName("AddKeyword").HasMaxLength(50);
            e.Property(x => x.ServerId).HasColumnName("ServerID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("datetime");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
            // Computed in the database; EF reads/filters it but never writes it.
            e.Property(x => x.Keyword).HasColumnName("Keyword")
                .HasComputedColumnSql("replace([AddKeyword],'add','')");
        });

        modelBuilder.Entity<ChatKeywordAlias>(e =>
        {
            e.ToTable("ChatKeywordAlias");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Alias).HasColumnName("Alias").HasMaxLength(50);
            e.Property(x => x.Keyword).HasColumnName("Keyword").HasMaxLength(50);
            e.Property(x => x.ServerId).HasColumnName("ServerID");
            e.Property(x => x.CreatedOn).HasColumnName("CreatedOn").HasColumnType("datetime");
            e.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(50);
        });

        modelBuilder.Entity<UsersScheduledKeyword>(e =>
        {
            e.ToTable("UsersScheduledKeyword");
            // No key column in the table; the row is uniquely identified by all three.
            e.HasKey(x => new { x.UserId, x.ChatKeyword, x.ScheduledDateTime });
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.ChatKeyword).HasColumnName("ChatKeyword").HasMaxLength(100);
            e.Property(x => x.ScheduledDateTime).HasColumnName("ScheduledDateTime").HasColumnType("datetime");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => new { x.UserId, x.ServerUid });
            e.Property(x => x.UserId).HasColumnName("UserID").HasMaxLength(50);
            e.Property(x => x.Username).HasColumnName("Username").HasMaxLength(200);
            e.Property(x => x.ServerUid).HasColumnName("ServerUID");
        });
    }
}
