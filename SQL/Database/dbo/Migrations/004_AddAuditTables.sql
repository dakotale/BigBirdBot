-- =============================================================
-- Migration 004 – Add audit tables for Discord events:
--   UserJoined, UserLeft, ButtonExecuted, GuildJoined,
--   ReactionAdded, GameTrigger
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

-- ── 1. AuditUserJoined ────────────────────────────────────────
CREATE TABLE [dbo].[AuditUserJoined] (
    [ID]       [int]      IDENTITY(1,1) NOT NULL,
    [UserUID]  [bigint]   NOT NULL,
    [ServerUID][bigint]   NOT NULL,
    [JoinedOn] [datetime] NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditUserJoined] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditUserJoined]
    @UserUID   bigint,
    @ServerUID bigint
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditUserJoined] (UserUID, ServerUID)
    VALUES (@UserUID, @ServerUID);
END
GO

-- ── 2. AuditUserLeft ─────────────────────────────────────────
CREATE TABLE [dbo].[AuditUserLeft] (
    [ID]       [int]      IDENTITY(1,1) NOT NULL,
    [UserUID]  [bigint]   NOT NULL,
    [ServerUID][bigint]   NOT NULL,
    [LeftOn]   [datetime] NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditUserLeft] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditUserLeft]
    @UserUID   bigint,
    @ServerUID bigint
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditUserLeft] (UserUID, ServerUID)
    VALUES (@UserUID, @ServerUID);
END
GO

-- ── 3. AuditButtonExecuted ───────────────────────────────────
CREATE TABLE [dbo].[AuditButtonExecuted] (
    [ID]         [int]          IDENTITY(1,1) NOT NULL,
    [ButtonID]   [varchar](100) NOT NULL,
    [UserUID]    [bigint]       NOT NULL,
    [ServerUID]  [bigint]       NOT NULL,
    [ExecutedOn] [datetime]     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditButtonExecuted] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditButtonExecuted]
    @ButtonID  varchar(100),
    @UserUID   bigint,
    @ServerUID bigint
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditButtonExecuted] (ButtonID, UserUID, ServerUID)
    VALUES (@ButtonID, @UserUID, @ServerUID);
END
GO

-- ── 4. AuditGuildJoined ──────────────────────────────────────
CREATE TABLE [dbo].[AuditGuildJoined] (
    [ID]         [int]          IDENTITY(1,1) NOT NULL,
    [ServerUID]  [bigint]       NOT NULL,
    [ServerName] [varchar](100) NOT NULL,
    [JoinedOn]   [datetime]     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditGuildJoined] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditGuildJoined]
    @ServerUID  bigint,
    @ServerName varchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditGuildJoined] (ServerUID, ServerName)
    VALUES (@ServerUID, @ServerName);
END
GO

-- ── 5. AuditReactionAdded ────────────────────────────────────
CREATE TABLE [dbo].[AuditReactionAdded] (
    [ID]        [int]         IDENTITY(1,1) NOT NULL,
    [Emoji]     [varchar](50) NOT NULL,
    [MessageUID][bigint]      NOT NULL,
    [UserUID]   [bigint]      NOT NULL,
    [ChannelUID][bigint]      NOT NULL,
    [AddedOn]   [datetime]    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditReactionAdded] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditReactionAdded]
    @Emoji      varchar(50),
    @MessageUID bigint,
    @UserUID    bigint,
    @ChannelUID bigint
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditReactionAdded] (Emoji, MessageUID, UserUID, ChannelUID)
    VALUES (@Emoji, @MessageUID, @UserUID, @ChannelUID);
END
GO

-- ── 6. AuditGameTrigger ──────────────────────────────────────
CREATE TABLE [dbo].[AuditGameTrigger] (
    [ID]          [int]         IDENTITY(1,1) NOT NULL,
    [Game]        [varchar](50) NOT NULL,
    [UserUID]     [bigint]      NOT NULL,
    [ServerUID]   [bigint]      NOT NULL,
    [TriggeredOn] [datetime]    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditGameTrigger] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

CREATE PROCEDURE [dbo].[AddAuditGameTrigger]
    @Game      varchar(50),
    @UserUID   bigint,
    @ServerUID bigint
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AuditGameTrigger] (Game, UserUID, ServerUID)
    VALUES (@Game, @UserUID, @ServerUID);
END
GO

PRINT 'Migration 004 complete – Audit tables created for UserJoined, UserLeft, ButtonExecuted, GuildJoined, ReactionAdded, GameTrigger.';
GO
