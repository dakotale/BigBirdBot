-- =============================================================================
-- Migration 010 - Drop keyword stored procedures
--
-- The keyword feature area has been moved off stored procedures onto EF Core
-- (Helper/KeywordService.cs + Data/BigBirdContext.cs). These 22 procedures are
-- no longer called by the bot and can be dropped.
--
-- TABLES ARE NOT TOUCHED. ChatKeyword, ChatKeywordMap, ChatKeywordAlias and
-- UsersScheduledKeyword stay exactly as they are - EF Core maps to them directly.
--
-- Unrelated procedures that also write to these tables as part of a larger
-- operation (dbo.DeleteUser, dbo.DeactiveServer) are NOT affected - they issue
-- their own DELETE statements and do not call any procedure dropped here.
--
-- Run once against the live DiscordBot database, after the new bot build is
-- deployed. Safe to re-run (IF EXISTS guards).
-- =============================================================================
USE [DiscordBot];
GO

-- ChatKeyword entries -----------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddChatKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteChatKeywordURL];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordRecent];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordInfo];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetKeywordNSFW];
GO
DROP PROCEDURE IF EXISTS [dbo].[MarkKeywordNSFW];
GO

-- ChatKeywordMap (keyword registration) ---------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddChatKeywordMap];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteChatKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[RenameChatKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordsByServer];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordMap];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordAll];   -- was already unused (no caller)
GO

-- ChatKeywordAlias -----------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddChatKeywordAlias];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetChatKeywordAliases];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteChatKeywordAlias];
GO

-- Message-trigger lookup ----------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetChatAction];
GO

-- UsersScheduledKeyword (recurring DM deliveries) --------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddUsersScheduledKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteUsersScheduledKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetUsersScheduledKeywords];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateUsersScheduledKeywordRequeue];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetUsersScheduledKeyword];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetScheduledEventUsers];
GO

PRINT 'Migration 010 complete - 22 keyword stored procedures dropped.';
GO
