-- =============================================================================
-- Migration 014 - Decommission the daily journaling feature (/journal)
--
-- The /journal command (subscribe, unsubscribe, done, status) and its DM
-- reminder scheduler tick were removed from the bot. Their stored procedures
-- are no longer called and are dropped here.
--
-- ALL TABLES ARE KEPT FOR ARCHIVAL and are NOT touched (JournalSubscriptions,
-- JournalEntries both remain, as does the unrelated PetJournal table from the
-- earlier pet-system strip).
--
-- Note: the AI color-palette command (/palette) was also removed in this pass,
-- but it never used a stored procedure (pure Anthropic API + image render), so
-- there is nothing to drop for it. Same for the /playlist command — its
-- SavePlaylistTrack/GetPlaylistTracks/GetUserPlaylists/DeletePlaylist calls
-- never had backing procedures in this database.
--
-- Run once against the live DiscordBot database. Safe to re-run.
-- =============================================================================
USE [DiscordBot];
GO

DROP PROCEDURE IF EXISTS [dbo].[UpsertJournalSubscription];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteJournalSubscription];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetDueJournalReminders];
GO
DROP PROCEDURE IF EXISTS [dbo].[LogJournalEntry];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetJournalStatus];
GO
