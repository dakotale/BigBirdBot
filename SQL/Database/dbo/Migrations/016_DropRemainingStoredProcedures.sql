-- =============================================================================
-- Migration 016 - Drop the last 43 stored procedures (full EF Core migration)
--
-- Every remaining feature area that still used ADO.NET stored procedures has now
-- moved to EF Core (see Data/*.cs, Helper/*Service.cs): audit logging, autorole,
-- reminders/birthdays, AI chat history, the bonus word puzzle, server config,
-- users, pronouns, and music/audio. Constants/StoredProcedure.cs (the ADO.NET
-- helper) and its dedicated test suite were removed from the codebase, since
-- nothing calls it anymore. This is the last DROP PROCEDURE migration expected
-- from this conversion effort.
--
-- ALL TABLES ARE KEPT FOR ARCHIVAL and are NOT touched.
--
-- Run once against the live DiscordBot database. Safe to re-run.
-- =============================================================================
USE [DiscordBot];
GO

-- Audit logging
DROP PROCEDURE IF EXISTS [dbo].[AddAudit];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditButtonExecuted];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditGameTrigger];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditGuildJoined];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditReactionAdded];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditUserJoined];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddAuditUserLeft];
GO

-- AutoRole
DROP PROCEDURE IF EXISTS [dbo].[DeleteGuildAutoRole];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetGuildAutoRole];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpsertGuildAutoRole];
GO

-- Scheduling (reminders / birthdays)
DROP PROCEDURE IF EXISTS [dbo].[AddBirthday];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetTodaysBirthdays];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddReminder];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetDueReminders];
GO

-- AI chat (/chat, /detectaibyattachment)
DROP PROCEDURE IF EXISTS [dbo].[AddBotAIMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteBotAIMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetBotAIMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetAIJSONImageReturn];
GO

-- Bonus word puzzle
DROP PROCEDURE IF EXISTS [dbo].[AddPetWordPuzzle];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClaimPetPuzzle];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetActivePetPuzzle];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetWordPuzzle];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPuzzleClaimedStatus];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetRandomWord];
GO

-- Servers
DROP PROCEDURE IF EXISTS [dbo].[AddServer];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetServerByID];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetServers];
GO
DROP PROCEDURE IF EXISTS [dbo].[ToggleAnnouncements];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetEmbedBroken];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateBrokenEmbed];
GO

-- Users
DROP PROCEDURE IF EXISTS [dbo].[AddUser];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteUser];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateUserLastSeen];
GO

-- Pronouns
DROP PROCEDURE IF EXISTS [dbo].[GetPronouns];
GO

-- Music / audio
DROP PROCEDURE IF EXISTS [dbo].[AddMusic];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteMusicQueue];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteMusicQueueAll];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetMusicQueue];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddPlayerConnected];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeletePlayerConnected];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPlayerConnected];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetVolume];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateVolume];
GO
