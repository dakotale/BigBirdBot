-- ============================================================
-- Migration 006: Auto-Role, Quote System
-- ============================================================

-- ── Auto-Role ────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GuildAutoRole')
CREATE TABLE dbo.GuildAutoRole
(
    GuildId  BIGINT       NOT NULL PRIMARY KEY,
    RoleId   BIGINT       NOT NULL,
    UpdatedAt DATETIME2   NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- ── Quote System ─────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GuildQuoteConfig')
CREATE TABLE dbo.GuildQuoteConfig
(
    GuildId          BIGINT NOT NULL PRIMARY KEY,
    ArchiveChannelId BIGINT NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Quotes')
CREATE TABLE dbo.Quotes
(
    QuoteId            INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    GuildId            BIGINT        NOT NULL,
    AuthorId           BIGINT        NOT NULL,
    AuthorUsername     NVARCHAR(100) NOT NULL,
    SavedByUserId      BIGINT        NOT NULL,
    SavedByUsername    NVARCHAR(100) NOT NULL,
    Content            NVARCHAR(2000) NOT NULL,
    OriginalMessageUrl NVARCHAR(512) NOT NULL,
    ArchiveMessageUrl  NVARCHAR(512)     NULL,
    AttachmentUrl      NVARCHAR(512)     NULL,
    SavedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Quotes_GuildId_OriginalMessageUrl' AND object_id = OBJECT_ID('dbo.Quotes'))
CREATE UNIQUE INDEX UX_Quotes_GuildId_OriginalMessageUrl
    ON dbo.Quotes (GuildId, OriginalMessageUrl);
GO

-- ── Stored Procedures ────────────────────────────────────────

-- Auto-Role: upsert
IF OBJECT_ID('dbo.UpsertGuildAutoRole', 'P') IS NOT NULL DROP PROCEDURE dbo.UpsertGuildAutoRole;
GO
CREATE PROCEDURE dbo.UpsertGuildAutoRole
    @GuildId BIGINT,
    @RoleId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.GuildAutoRole AS t
    USING (SELECT @GuildId AS GuildId, @RoleId AS RoleId) AS s
        ON t.GuildId = s.GuildId
    WHEN MATCHED THEN
        UPDATE SET RoleId = s.RoleId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (GuildId, RoleId) VALUES (s.GuildId, s.RoleId);
END;
GO

-- Auto-Role: delete
IF OBJECT_ID('dbo.DeleteGuildAutoRole', 'P') IS NOT NULL DROP PROCEDURE dbo.DeleteGuildAutoRole;
GO
CREATE PROCEDURE dbo.DeleteGuildAutoRole
    @GuildId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.GuildAutoRole WHERE GuildId = @GuildId;
END;
GO

-- Auto-Role: get
IF OBJECT_ID('dbo.GetGuildAutoRole', 'P') IS NOT NULL DROP PROCEDURE dbo.GetGuildAutoRole;
GO
CREATE PROCEDURE dbo.GetGuildAutoRole
    @GuildId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT GuildId, RoleId FROM dbo.GuildAutoRole WHERE GuildId = @GuildId;
END;
GO

-- Quote: upsert archive channel
IF OBJECT_ID('dbo.UpsertGuildQuoteConfig', 'P') IS NOT NULL DROP PROCEDURE dbo.UpsertGuildQuoteConfig;
GO
CREATE PROCEDURE dbo.UpsertGuildQuoteConfig
    @GuildId          BIGINT,
    @ArchiveChannelId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.GuildQuoteConfig AS t
    USING (SELECT @GuildId AS GuildId, @ArchiveChannelId AS ArchiveChannelId) AS s
        ON t.GuildId = s.GuildId
    WHEN MATCHED THEN
        UPDATE SET ArchiveChannelId = s.ArchiveChannelId
    WHEN NOT MATCHED THEN
        INSERT (GuildId, ArchiveChannelId) VALUES (s.GuildId, s.ArchiveChannelId);
END;
GO

-- Quote: get config
IF OBJECT_ID('dbo.GetGuildQuoteConfig', 'P') IS NOT NULL DROP PROCEDURE dbo.GetGuildQuoteConfig;
GO
CREATE PROCEDURE dbo.GetGuildQuoteConfig
    @GuildId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT GuildId, ArchiveChannelId FROM dbo.GuildQuoteConfig WHERE GuildId = @GuildId;
END;
GO

-- Quote: insert (returns QuoteId + Duplicate flag)
IF OBJECT_ID('dbo.InsertQuote', 'P') IS NOT NULL DROP PROCEDURE dbo.InsertQuote;
GO
CREATE PROCEDURE dbo.InsertQuote
    @GuildId            BIGINT,
    @AuthorId           BIGINT,
    @AuthorUsername     NVARCHAR(100),
    @SavedByUserId      BIGINT,
    @SavedByUsername    NVARCHAR(100),
    @Content            NVARCHAR(2000),
    @OriginalMessageUrl NVARCHAR(512),
    @ArchiveMessageUrl  NVARCHAR(512)  = NULL,
    @AttachmentUrl      NVARCHAR(512)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Quotes WHERE GuildId = @GuildId AND OriginalMessageUrl = @OriginalMessageUrl)
    BEGIN
        SELECT QuoteId, 1 AS Duplicate FROM dbo.Quotes WHERE GuildId = @GuildId AND OriginalMessageUrl = @OriginalMessageUrl;
        RETURN;
    END

    INSERT INTO dbo.Quotes
        (GuildId, AuthorId, AuthorUsername, SavedByUserId, SavedByUsername, Content, OriginalMessageUrl, ArchiveMessageUrl, AttachmentUrl)
    VALUES
        (@GuildId, @AuthorId, @AuthorUsername, @SavedByUserId, @SavedByUsername, @Content, @OriginalMessageUrl, @ArchiveMessageUrl, @AttachmentUrl);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS QuoteId, 0 AS Duplicate;
END;
GO

-- Quote: random
IF OBJECT_ID('dbo.GetRandomQuote', 'P') IS NOT NULL DROP PROCEDURE dbo.GetRandomQuote;
GO
CREATE PROCEDURE dbo.GetRandomQuote
    @GuildId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 * FROM dbo.Quotes WHERE GuildId = @GuildId ORDER BY NEWID();
END;
GO

-- Quote: search by text
IF OBJECT_ID('dbo.SearchQuotes', 'P') IS NOT NULL DROP PROCEDURE dbo.SearchQuotes;
GO
CREATE PROCEDURE dbo.SearchQuotes
    @GuildId BIGINT,
    @Query   NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Quotes
    WHERE GuildId = @GuildId AND Content LIKE N'%' + @Query + N'%'
    ORDER BY SavedAt DESC;
END;
GO

-- Quote: by user
IF OBJECT_ID('dbo.GetQuotesByUser', 'P') IS NOT NULL DROP PROCEDURE dbo.GetQuotesByUser;
GO
CREATE PROCEDURE dbo.GetQuotesByUser
    @GuildId  BIGINT,
    @AuthorId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Quotes
    WHERE GuildId = @GuildId AND AuthorId = @AuthorId
    ORDER BY SavedAt DESC;
END;
GO

-- Quote: update archive URL after posting
IF OBJECT_ID('dbo.UpdateQuoteArchiveUrl', 'P') IS NOT NULL DROP PROCEDURE dbo.UpdateQuoteArchiveUrl;
GO
CREATE PROCEDURE dbo.UpdateQuoteArchiveUrl
    @QuoteId          INT,
    @ArchiveMessageUrl NVARCHAR(512)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Quotes SET ArchiveMessageUrl = @ArchiveMessageUrl WHERE QuoteId = @QuoteId;
END;
GO
