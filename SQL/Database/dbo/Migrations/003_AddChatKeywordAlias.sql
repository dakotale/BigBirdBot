-- =============================================================
-- Migration 003 – Add ChatKeywordAlias table and update
--                 GetChatAction to resolve aliases.
--
-- Replaces the /keyword copy command with a true alias system
-- where multiple aliases can point to a single keyword per server.
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

-- ── 1. Create ChatKeywordAlias table ─────────────────────────
CREATE TABLE [dbo].[ChatKeywordAlias] (
    [ID]        [int]          IDENTITY(1,1) NOT NULL,
    [Alias]     [varchar](50)  NOT NULL,
    [Keyword]   [varchar](50)  NOT NULL,
    [ServerID]  [bigint]       NOT NULL,
    [CreatedOn] [datetime]     NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] [varchar](50)  NOT NULL,
    CONSTRAINT [PK_ChatKeywordAlias] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UQ_ChatKeywordAlias_Alias_Server] UNIQUE ([Alias], [ServerID])
);
GO

-- ── 2. Supporting stored procedures ──────────────────────────
CREATE PROCEDURE [dbo].[AddChatKeywordAlias]
    @Alias     varchar(50),
    @Keyword   varchar(50),
    @ServerID  bigint,
    @CreatedBy varchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM ChatKeywordAlias WHERE Alias = @Alias AND ServerID = @ServerID)
    BEGIN
        SELECT 'exists' AS Result;
        RETURN;
    END

    INSERT INTO ChatKeywordAlias (Alias, Keyword, ServerID, CreatedOn, CreatedBy)
    VALUES (@Alias, @Keyword, @ServerID, GETUTCDATE(), @CreatedBy);

    SELECT 'added' AS Result;
END
GO

CREATE PROCEDURE [dbo].[GetChatKeywordAliases]
    @Keyword  varchar(50),
    @ServerID bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Alias, Keyword, CreatedBy, CreatedOn
    FROM   ChatKeywordAlias
    WHERE  Keyword = @Keyword AND ServerID = @ServerID
    ORDER BY Alias;
END
GO

CREATE PROCEDURE [dbo].[DeleteChatKeywordAlias]
    @Alias    varchar(50),
    @ServerID bigint
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ChatKeywordAlias
    WHERE Alias = @Alias AND ServerID = @ServerID;
END
GO

-- ── 3. Update GetChatAction to resolve aliases ────────────────
ALTER PROCEDURE [dbo].[GetChatAction]
    @ServerID bigint,
    @Message  varchar(2000)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NormalizedMessage VARCHAR(2000) = LOWER(@Message);
    DECLARE @Keyword           VARCHAR(50);
    DECLARE @ChatKeywordID     INT;

    -- Try exact word match against registered keywords for this server
    ;WITH MessageWords AS (
        SELECT TRIM(value) AS Word
        FROM STRING_SPLIT(@NormalizedMessage, ' ')
    )
    SELECT TOP 1
        @Keyword       = ck.ChatKeyword,
        @ChatKeywordID = ck.ID
    FROM ChatKeyword    ck
    JOIN ChatKeywordMap ckm ON ck.ChatKeyword  = ckm.Keyword
    JOIN MessageWords   mw  ON LOWER(ck.ChatKeyword) = mw.Word
    WHERE ckm.ServerID = @ServerID
    ORDER BY NEWID();

    -- If no direct match, try aliases registered for this server
    IF @Keyword IS NULL
    BEGIN
        ;WITH MessageWords AS (
            SELECT TRIM(value) AS Word
            FROM STRING_SPLIT(@NormalizedMessage, ' ')
        )
        SELECT TOP 1
            @Keyword = cka.Keyword
        FROM ChatKeywordAlias cka
        JOIN MessageWords     mw  ON LOWER(cka.Alias) = mw.Word
        WHERE cka.ServerID = @ServerID
        ORDER BY NEWID();
    END

    -- Return a random entry from the matched keyword (direct or aliased)
    IF @Keyword IS NOT NULL
    BEGIN
        SELECT TOP 1
            ID,
            FilePath AS ChatAction,
            NSFW,
            @Keyword [Keyword]
        FROM ChatKeyword
        WHERE ChatKeyword = @Keyword
        ORDER BY NEWID();
    END
END
GO

PRINT 'Migration 003 complete – ChatKeywordAlias table created and GetChatAction updated.';
GO
