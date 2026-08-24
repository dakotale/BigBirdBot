using System;
using System.Collections.Generic;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Data;

public partial class DiscordbotContext : DbContext
{
    public DiscordbotContext(DbContextOptions<DiscordbotContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditButtonExecuted> AuditButtonExecuteds { get; set; }

    public virtual DbSet<AuditGameTrigger> AuditGameTriggers { get; set; }

    public virtual DbSet<AuditGuildJoined> AuditGuildJoineds { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<AuditReactionAdded> AuditReactionAddeds { get; set; }

    public virtual DbSet<AuditUserJoined> AuditUserJoineds { get; set; }

    public virtual DbSet<AuditUserLeft> AuditUserLefts { get; set; }

    public virtual DbSet<Birthday> Birthdays { get; set; }

    public virtual DbSet<BlackjackGame> BlackjackGames { get; set; }

    public virtual DbSet<BotAimessage> BotAimessages { get; set; }

    public virtual DbSet<ChallengePool> ChallengePools { get; set; }

    public virtual DbSet<ChatKeyword> ChatKeywords { get; set; }

    public virtual DbSet<ChatKeywordAlias> ChatKeywordAliases { get; set; }

    public virtual DbSet<ChatKeywordMap> ChatKeywordMaps { get; set; }

    public virtual DbSet<Credit> Credits { get; set; }

    public virtual DbSet<FishLog> FishLogs { get; set; }

    public virtual DbSet<ForgedCosmetic> ForgedCosmetics { get; set; }

    public virtual DbSet<GambleLog> GambleLogs { get; set; }

    public virtual DbSet<GuildAutoRole> GuildAutoRoles { get; set; }

    public virtual DbSet<GuildQuoteConfig> GuildQuoteConfigs { get; set; }

    public virtual DbSet<Investment> Investments { get; set; }

    public virtual DbSet<JackpotEntry> JackpotEntries { get; set; }

    public virtual DbSet<JournalEntry> JournalEntries { get; set; }

    public virtual DbSet<JournalSubscription> JournalSubscriptions { get; set; }

    public virtual DbSet<Music> Musics { get; set; }

    public virtual DbSet<MusicQueue> MusicQueues { get; set; }

    public virtual DbSet<NamesReference> NamesReferences { get; set; }

    public virtual DbSet<NamesStaging> NamesStagings { get; set; }

    public virtual DbSet<PassiveJackpot> PassiveJackpots { get; set; }

    public virtual DbSet<PassiveJackpotContributor> PassiveJackpotContributors { get; set; }

    public virtual DbSet<Pet> Pets { get; set; }

    public virtual DbSet<PetCosmetic> PetCosmetics { get; set; }

    public virtual DbSet<PetEgg> PetEggs { get; set; }

    public virtual DbSet<PetJournal> PetJournals { get; set; }

    public virtual DbSet<PetWordPuzzle> PetWordPuzzles { get; set; }

    public virtual DbSet<PlayerConnected> PlayerConnecteds { get; set; }

    public virtual DbSet<PlaylistTrack> PlaylistTracks { get; set; }

    public virtual DbSet<PokerLobby> PokerLobbies { get; set; }

    public virtual DbSet<PokerPlayer> PokerPlayers { get; set; }

    public virtual DbSet<PregnancyEvent> PregnancyEvents { get; set; }

    public virtual DbSet<Pronoun> Pronouns { get; set; }

    public virtual DbSet<Quote> Quotes { get; set; }

    public virtual DbSet<Reminder> Reminders { get; set; }

    public virtual DbSet<ScrambleGame> ScrambleGames { get; set; }

    public virtual DbSet<Server> Servers { get; set; }

    public virtual DbSet<ServerPassiveJackpot> ServerPassiveJackpots { get; set; }

    public virtual DbSet<Stock> Stocks { get; set; }

    public virtual DbSet<StockHistory> StockHistories { get; set; }

    public virtual DbSet<StockHolding> StockHoldings { get; set; }

    public virtual DbSet<StockTransaction> StockTransactions { get; set; }

    public virtual DbSet<TriviaMessage> TriviaMessages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserActiveEffect> UserActiveEffects { get; set; }

    public virtual DbSet<UserDailyChallenge> UserDailyChallenges { get; set; }

    public virtual DbSet<UserInventory> UserInventories { get; set; }

    public virtual DbSet<UsersScheduledKeyword> UsersScheduledKeywords { get; set; }

    public virtual DbSet<Word> Words { get; set; }

    public virtual DbSet<WordleGame> WordleGames { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditButtonExecuted>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditButtonExecuted_pkey");

            entity.ToTable("AuditButtonExecuted", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ButtonId).HasColumnName("ButtonID");
            entity.Property(e => e.ExecutedOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.UserUid).HasColumnName("UserUID");
        });

        modelBuilder.Entity<AuditGameTrigger>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditGameTrigger_pkey");

            entity.ToTable("AuditGameTrigger", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.TriggeredOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.UserUid).HasColumnName("UserUID");
        });

        modelBuilder.Entity<AuditGuildJoined>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditGuildJoined_pkey");

            entity.ToTable("AuditGuildJoined", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.JoinedOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.ToTable("AuditLog", "archive");

            entity.HasIndex(e => new { e.Command, e.ServerUid }, "idx_16404_IDX_AUDITLOG_COMMAND_SERVERUID");

            entity.Property(e => e.AuditLogId)
                .ValueGeneratedOnAdd()
                .HasColumnName("AuditLogID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
        });

        modelBuilder.Entity<AuditReactionAdded>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditReactionAdded_pkey");

            entity.ToTable("AuditReactionAdded", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.AddedOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ChannelUid).HasColumnName("ChannelUID");
            entity.Property(e => e.MessageUid).HasColumnName("MessageUID");
            entity.Property(e => e.UserUid).HasColumnName("UserUID");
        });

        modelBuilder.Entity<AuditUserJoined>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditUserJoined_pkey");

            entity.ToTable("AuditUserJoined", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.JoinedOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.UserUid).HasColumnName("UserUID");
        });

        modelBuilder.Entity<AuditUserLeft>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AuditUserLeft_pkey");

            entity.ToTable("AuditUserLeft", "archive");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.LeftOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.UserUid).HasColumnName("UserUID");
        });

        modelBuilder.Entity<Birthday>(entity =>
        {
            entity.HasKey(e => e.BirthdayId);
            entity.ToTable("Birthday");

            entity.Property(e => e.BirthdayId)
                .ValueGeneratedOnAdd()
                .HasColumnName("BirthdayID");
        });

        modelBuilder.Entity<BlackjackGame>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("BlackjackGame_pkey");

            entity.ToTable("BlackjackGame");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Bet).HasPrecision(38);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
        });

        modelBuilder.Entity<BotAimessage>(entity =>
        {
            entity.HasKey(e => e.BotAimessageId);
            entity.ToTable("BotAIMessage");

            entity.Property(e => e.BotAimessageId)
                .ValueGeneratedOnAdd()
                .HasColumnName("BotAIMessageID");
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.CreatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<ChallengePool>(entity =>
        {
            entity.HasKey(e => e.ChallengeId).HasName("idx_16457_PRIMARY");

            entity.ToTable("ChallengePool");

            entity.HasIndex(e => e.Key, "idx_16457_UQ__Challeng__C41E0289FC41EBE2").IsUnique();

            entity.Property(e => e.ChallengeId).HasColumnName("ChallengeID");
            entity.Property(e => e.Difficulty).HasDefaultValue((short)1);
            entity.Property(e => e.TargetCount).HasDefaultValue(1);
        });

        modelBuilder.Entity<ChatKeyword>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ChatKeyword");

            entity.HasIndex(e => new { e.ChatKeyword1, e.Nsfw }, "idx_16472_IDX_CHATKEYWORD_CHATKEYWORD_NSFW");

            entity.Property(e => e.ChatKeyword1).HasColumnName("ChatKeyword");
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID");
            entity.Property(e => e.Nsfw).HasColumnName("NSFW");
        });

        modelBuilder.Entity<ChatKeywordAlias>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ChatKeywordAlias_pkey");

            entity.ToTable("ChatKeywordAlias");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
        });

        modelBuilder.Entity<ChatKeywordMap>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ChatKeywordMap");

            entity.HasIndex(e => new { e.AddKeyword, e.ServerId, e.Keyword }, "idx_16488_IDX_CHATKEYWORDMAP_ADDKEYWORD_KEYWORD_SERVERID");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
        });

        modelBuilder.Entity<Credit>(entity =>
        {
            entity.HasKey(e => e.CreditId).HasName("idx_16498_PRIMARY");

            entity.HasIndex(e => new { e.UserId, e.ServerId }, "idx_16498_UQ_Credits_User").IsUnique();

            entity.Property(e => e.CreditId).HasColumnName("CreditID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<FishLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("FishLog_pkey");

            entity.ToTable("FishLog");

            entity.Property(e => e.LogId).HasColumnName("LogID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Credits).HasPrecision(38);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<ForgedCosmetic>(entity =>
        {
            entity.HasKey(e => e.ForgeId).HasName("ForgedCosmetics_pkey");

            entity.Property(e => e.ForgeId).HasColumnName("ForgeID");
            entity.Property(e => e.ColourHex).HasDefaultValueSql("'#FFFFFF'::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.CreditsCost).HasPrecision(38);
            entity.Property(e => e.PetId).HasColumnName("PetID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<GambleLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("GambleLog_pkey");

            entity.ToTable("GambleLog");

            entity.Property(e => e.LogId).HasColumnName("LogID");
            entity.Property(e => e.Bet).HasPrecision(38);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Net).HasPrecision(38);
            entity.Property(e => e.Payout).HasPrecision(38);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<GuildAutoRole>(entity =>
        {
            entity.HasKey(e => e.GuildId).HasName("GuildAutoRole_pkey");

            entity.ToTable("GuildAutoRole");

            entity.Property(e => e.GuildId).ValueGeneratedNever();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
        });

        modelBuilder.Entity<GuildQuoteConfig>(entity =>
        {
            entity.HasKey(e => e.GuildId).HasName("idx_16537_PRIMARY");

            entity.ToTable("GuildQuoteConfig");

            entity.Property(e => e.GuildId).ValueGeneratedNever();
        });

        modelBuilder.Entity<Investment>(entity =>
        {
            entity.HasKey(e => e.InvestmentId).HasName("Investments_pkey");

            entity.Property(e => e.InvestmentId).HasColumnName("InvestmentID");
            entity.Property(e => e.Amount).HasPrecision(38);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Multiplier).HasPrecision(4, 2);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<JackpotEntry>(entity =>
        {
            entity.HasKey(e => e.EntryId).HasName("JackpotEntries_pkey");

            entity.Property(e => e.EntryId).HasColumnName("EntryID");
            entity.Property(e => e.Amount).HasPrecision(38);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.HasKey(e => e.EntryId).HasName("JournalEntries_pkey");

            entity.Property(e => e.EntryId).HasColumnName("EntryID");
            entity.Property(e => e.EntryDate).HasDefaultValueSql("((now() AT TIME ZONE 'utc'::text))::date");
            entity.Property(e => e.LoggedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<JournalSubscription>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("JournalSubscriptions_pkey");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.SubscribedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
        });

        modelBuilder.Entity<Music>(entity =>
        {
            entity.HasKey(e => e.MusicId);
            entity.ToTable("Music");

            entity.HasIndex(e => e.ServerUid, "idx_16561_IDX_MUSIC_SERVERUID");

            entity.Property(e => e.MusicId)
                .ValueGeneratedOnAdd()
                .HasColumnName("MusicID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.Url).HasColumnName("URL");
            entity.Property(e => e.VideoId).HasColumnName("VideoID");
        });

        modelBuilder.Entity<MusicQueue>(entity =>
        {
            entity.HasKey(e => e.MusicQueueId);
            entity.ToTable("MusicQueue");

            entity.Property(e => e.MusicId).HasColumnName("MusicID");
            entity.Property(e => e.MusicQueueId)
                .ValueGeneratedOnAdd()
                .HasColumnName("MusicQueueID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.TextChannelId).HasColumnName("TextChannelID");
            entity.Property(e => e.Url).HasColumnName("URL");
            entity.Property(e => e.VoiceChannelId).HasColumnName("VoiceChannelID");
        });

        modelBuilder.Entity<NamesReference>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("idx_16587_PRIMARY");

            entity.ToTable("NamesReference");
        });

        modelBuilder.Entity<NamesStaging>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("NamesStaging");
        });

        modelBuilder.Entity<PassiveJackpot>(entity =>
        {
            entity.HasKey(e => e.ServerId).HasName("idx_16598_PRIMARY");

            entity.ToTable("PassiveJackpot");

            entity.Property(e => e.ServerId)
                .ValueGeneratedNever()
                .HasColumnName("ServerID");
        });

        modelBuilder.Entity<PassiveJackpotContributor>(entity =>
        {
            entity.HasKey(e => new { e.ServerId, e.UserId }).HasName("idx_16606_PRIMARY");

            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(e => e.PetId).HasName("Pet_pkey");

            entity.ToTable("Pet");

            entity.Property(e => e.PetId).HasColumnName("PetID");
            entity.Property(e => e.Accessory1).HasDefaultValueSql("''::text");
            entity.Property(e => e.Accessory2).HasDefaultValueSql("''::text");
            entity.Property(e => e.Bio).HasDefaultValueSql("''::text");
            entity.Property(e => e.BirthDate).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Breed).HasDefaultValueSql("''::text");
            entity.Property(e => e.Energy).HasDefaultValue(80);
            entity.Property(e => e.Happiness).HasDefaultValue(80);
            entity.Property(e => e.Hunger).HasDefaultValue(80);
            entity.Property(e => e.Hygiene).HasDefaultValue(80);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Xp).HasColumnName("XP");
        });

        modelBuilder.Entity<PetCosmetic>(entity =>
        {
            entity.HasKey(e => e.CosmeticId).HasName("PetCosmetics_pkey");

            entity.Property(e => e.CosmeticId).HasColumnName("CosmeticID");
            entity.Property(e => e.AppliedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.PetId).HasColumnName("PetID");
        });

        modelBuilder.Entity<PetEgg>(entity =>
        {
            entity.HasKey(e => e.EggId).HasName("PetEggs_pkey");

            entity.Property(e => e.EggId).HasColumnName("EggID");
            entity.Property(e => e.BaseEnergy).HasDefaultValue(80);
            entity.Property(e => e.BaseHappiness).HasDefaultValue(80);
            entity.Property(e => e.BaseHunger).HasDefaultValue(80);
            entity.Property(e => e.BaseHygiene).HasDefaultValue(80);
            entity.Property(e => e.BaseXp).HasColumnName("BaseXP");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.HatchedPetId).HasColumnName("HatchedPetID");
            entity.Property(e => e.Parent1Id).HasColumnName("Parent1ID");
            entity.Property(e => e.Parent2Id).HasColumnName("Parent2ID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<PetJournal>(entity =>
        {
            entity.HasKey(e => e.JournalId).HasName("PetJournal_pkey");

            entity.ToTable("PetJournal");

            entity.Property(e => e.JournalId).HasColumnName("JournalID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.PetId).HasColumnName("PetID");

            entity.HasOne(d => d.Pet).WithMany(p => p.PetJournals)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PetJournal_Pet");
        });

        modelBuilder.Entity<PetWordPuzzle>(entity =>
        {
            entity.HasKey(e => e.PuzzleId).HasName("idx_16646_PRIMARY");

            entity.ToTable("PetWordPuzzle");

            entity.Property(e => e.PuzzleId).HasColumnName("PuzzleID");
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
        });

        modelBuilder.Entity<PlayerConnected>(entity =>
        {
            entity.HasKey(e => e.PlayerId);
            entity.ToTable("PlayerConnected");

            entity.Property(e => e.PlayerId)
                .ValueGeneratedOnAdd()
                .HasColumnName("PlayerID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.TextChannelId).HasColumnName("TextChannelID");
            entity.Property(e => e.VoiceChannelId).HasColumnName("VoiceChannelID");
        });

        modelBuilder.Entity<PlaylistTrack>(entity =>
        {
            // Not scaffolded — see the class-level comment on PlaylistTrack for why.
            entity.HasKey(e => new { e.UserId, e.ServerId, e.Name, e.Position });
            entity.ToTable("PlaylistTrack");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
        });

        modelBuilder.Entity<PokerLobby>(entity =>
        {
            entity.HasKey(e => e.GameId).HasName("PokerLobby_pkey");

            entity.ToTable("PokerLobby");

            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.BetPerPlayer).HasPrecision(38);
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.Community).HasDefaultValueSql("''::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("''::text")
                .HasColumnName("MessageID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.Status).HasDefaultValueSql("'waiting'::text");
        });

        modelBuilder.Entity<PokerPlayer>(entity =>
        {
            entity.HasKey(e => e.PlayerId).HasName("idx_16676_PRIMARY");

            entity.ToTable("PokerPlayer");

            entity.HasIndex(e => e.GameId, "idx_16676_IX_PokerPlayer_Game");

            entity.Property(e => e.PlayerId).HasColumnName("PlayerID");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<PregnancyEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PregnancyEvents_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<Pronoun>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID");
            entity.Property(e => e.Pronoun1).HasColumnName("Pronoun");
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.QuoteId).HasName("Quotes_pkey");

            entity.Property(e => e.SavedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.ReminderId).HasName("idx_16704_PRIMARY");

            entity.HasIndex(e => new { e.Sent, e.RemindAtUtc }, "idx_16704_IX_Reminders_Due");

            entity.Property(e => e.ReminderId).HasColumnName("ReminderID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<ScrambleGame>(entity =>
        {
            entity.HasKey(e => e.ChannelId).HasName("idx_16716_PRIMARY");

            entity.ToTable("ScrambleGame");

            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
        });

        modelBuilder.Entity<Server>(entity =>
        {
            // ServerId (int identity) is the real auto-increment PK column; the app usually
            // queries by ServerUid (the Discord snowflake) instead, but that's a LINQ filter
            // concern, independent of what EF treats as the entity's key.
            entity.HasKey(e => e.ServerId);

            entity.HasIndex(e => new { e.ServerUid, e.DefaultChannelId, e.Volume, e.FixEmbed, e.IsPlayerConnected, e.IsActive }, "idx_16731_IDX_SERVERS");

            entity.Property(e => e.DefaultChannelId).HasColumnName("DefaultChannelID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ServerId)
                .ValueGeneratedOnAdd()
                .HasColumnName("ServerID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.Volume).HasDefaultValue(100);
        });

        modelBuilder.Entity<ServerPassiveJackpot>(entity =>
        {
            entity.HasKey(e => e.ServerId).HasName("ServerPassiveJackpot_pkey");

            entity.ToTable("ServerPassiveJackpot");

            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Pool).HasPrecision(38);
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.Ticker).HasName("Stocks_pkey");

            entity.Property(e => e.High24h)
                .HasPrecision(18, 2)
                .HasDefaultValue(100.00m);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Low24h)
                .HasPrecision(18, 2)
                .HasDefaultValue(100.00m);
            entity.Property(e => e.PrevPrice)
                .HasPrecision(18, 2)
                .HasDefaultValue(100.00m);
            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .HasDefaultValue(100.00m);
            entity.Property(e => e.Trend).HasPrecision(5, 4);
            entity.Property(e => e.Volatility)
                .HasPrecision(5, 4)
                .HasDefaultValue(0.05m);
        });

        modelBuilder.Entity<StockHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("StockHistory_pkey");

            entity.ToTable("StockHistory");

            entity.Property(e => e.HistoryId).HasColumnName("HistoryID");
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
        });

        modelBuilder.Entity<StockHolding>(entity =>
        {
            entity.HasKey(e => e.HoldingId).HasName("idx_16755_PRIMARY");

            entity.HasIndex(e => new { e.UserId, e.ServerId, e.Ticker }, "idx_16755_UQ_Holdings").IsUnique();

            entity.Property(e => e.HoldingId).HasColumnName("HoldingID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.TxId).HasName("StockTransactions_pkey");

            entity.Property(e => e.TxId).HasColumnName("TxID");
            entity.Property(e => e.PriceEach).HasPrecision(12, 2);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.TotalCost).HasPrecision(38);
            entity.Property(e => e.TxTime).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<TriviaMessage>(entity =>
        {
            // The underlying table has no PRIMARY KEY constraint (true in the original SQL
            // Server schema too, not something the migration changed), so the scaffolder
            // marked this HasNoKey() — but the app always looks up/deletes by
            // TriviaMessageID as if it were unique (one row per Discord message being
            // scored). HasNoKey() entities can't be Add/Update/Removed via the DbSet API at
            // all, which the EF Core conversion of Program.cs's trivia-reaction handler
            // needs, so this declares TriviaMessageId as EF's key (this only affects how EF
            // reasons about the entity — it does not add a DB-level constraint).
            entity.HasKey(e => e.TriviaMessageId);
            entity.ToTable("TriviaMessage");

            entity.Property(e => e.TriviaMessageId).HasColumnName("TriviaMessageID").ValueGeneratedNever();
        });

        modelBuilder.Entity<User>(entity =>
        {
            // A Discord user has one row per server they're in — (UserId, ServerUid) is the
            // real natural key, matching how AddUser/DeleteUser check/filter in the source SQL.
            entity.HasKey(e => new { e.UserId, e.ServerUid });

            entity.HasIndex(e => new { e.UserId, e.ServerUid, e.PronounId }, "idx_16825_IDX_USERS_USERID_SERVERUID_PRONOUNID");

            entity.Property(e => e.PronounId).HasColumnName("PronounID");
            entity.Property(e => e.ServerUid).HasColumnName("ServerUID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<UserActiveEffect>(entity =>
        {
            entity.HasKey(e => e.EffectId).HasName("UserActiveEffects_pkey");

            entity.Property(e => e.EffectId).HasColumnName("EffectID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.StackCount).HasDefaultValue(1);
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<UserDailyChallenge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("idx_16798_PRIMARY");

            entity.HasIndex(e => new { e.UserId, e.ServerId, e.ChallengeDate }, "idx_16798_IX_UserDaily_Date");

            entity.HasIndex(e => new { e.UserId, e.ServerId, e.ChallengeDate }, "idx_16798_UQ_UserDailyChallenge").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Challenge1Id).HasColumnName("Challenge1ID");
            entity.Property(e => e.Challenge2Id).HasColumnName("Challenge2ID");
            entity.Property(e => e.Challenge3Id).HasColumnName("Challenge3ID");
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<UserInventory>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("UserInventory_pkey");

            entity.ToTable("UserInventory");

            entity.Property(e => e.InventoryId).HasColumnName("InventoryID");
            entity.Property(e => e.AcquiredAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.ServerId).HasColumnName("ServerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<UsersScheduledKeyword>(entity =>
        {
            // NOTE: this table has no real primary key or unique constraint (confirmed via \d —
            // just UserID/ChatKeyword/ScheduledDateTime columns, no PK). (UserId, ChatKeyword) is
            // declared here only so DbSet.Add(...) has something to work with for inserts — it is
            // NOT actually unique: a user can have 2+ rows for the same keyword (nothing prevents
            // it in HandleAddAsync). Never do a tracked read -> mutate -> SaveChanges against this
            // DbSet for UPDATE or DELETE — duplicate "key" rows make EF's affected-row-count check
            // throw DbUpdateConcurrencyException. Always use ExecuteUpdate(Async)/ExecuteDeleteAsync
            // (bulk, filter-based, no key resolution) instead — see Program.cs's
            // GetUsersScheduledKeyword/RequeueUserSchedule and Keyword.cs's Handle*Async methods.
            entity.HasKey(e => new { e.UserId, e.ChatKeyword });
            entity.ToTable("UsersScheduledKeyword");

            entity.HasIndex(e => new { e.ChatKeyword, e.ScheduledDateTime }, "idx_16835_IDX_USERSSCHEDULEDKEYWORD_CHATKEYWORD_SCHEDULEDDATETI");

            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<Word>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID");
            entity.Property(e => e.Word1).HasColumnName("Word");
        });

        modelBuilder.Entity<WordleGame>(entity =>
        {
            entity.HasKey(e => e.ChannelId).HasName("WordleGame_pkey");

            entity.ToTable("WordleGame");

            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(now() AT TIME ZONE 'utc'::text)");
            entity.Property(e => e.Guesses).HasDefaultValueSql("''::text");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
