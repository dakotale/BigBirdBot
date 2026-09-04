-- =============================================================================
-- Migration 013 - Drop dead stored procedures
--
-- These four procedures have no caller anywhere in the codebase. They are not
-- part of the economy/pets/games strips (010-012) - they are older procedures
-- that were superseded or belonged to a feature removed earlier:
--
--   AddMusicQueue         - superseded by AddMusic, which now writes both the
--                           Music and MusicQueue tables in one call.
--   GetMusicQueueByTrack  - never called; the queue is read by GetMusicQueue.
--   GetAllServerUsers     - only used by the removed /revolt command
--                           (SELECT UserID FROM Credits). Missed by migration 012.
--   DeactiveServer        - marks Servers.IsActive = 0 and prunes a guild's
--                           keyword/music/user rows. The bot has no LeftGuild
--                           handler, so nothing ever invoked it.
--
-- ALL TABLES ARE KEPT FOR ARCHIVAL and are NOT touched (MusicQueue, Music,
-- Servers, Credits, ChatKeywordMap, Users all remain).
--
-- The surviving music-queue procs stay: AddMusic, GetMusicQueue,
-- DeleteMusicQueue, DeleteMusicQueueAll.
--
-- Run once against the live DiscordBot database. Safe to re-run.
-- =============================================================================
USE [DiscordBot];
GO

DROP PROCEDURE IF EXISTS [dbo].[AddMusicQueue];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetMusicQueueByTrack];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetAllServerUsers];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeactiveServer];
GO
