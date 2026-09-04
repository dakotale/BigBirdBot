-- =============================================================================
-- Migration 015 - Drop tables for permanently removed features
--
-- Every prior migration in this series (010-014) deliberately left tables in
-- place "for archival" while dropping only the stored procedures that used
-- them. This migration is different: it drops the TABLES themselves, now that
-- a full database backup exists. Do not run this without a verified backup -
-- it is not reversible otherwise.
--
-- Scope: every table with zero references from any stored procedure, view,
-- function, or C# code in this repository. Verified by cross-referencing
-- sys.sql_expression_dependencies for all 43 remaining stored procedures
-- against sys.tables, plus a full-repo grep for raw table access (there is
-- none - the app only ever hits the database through named stored procedures
-- or, for the keyword area, EF Core against ChatKeyword/ChatKeywordMap/
-- ChatKeywordAlias/UsersScheduledKeyword/Users, none of which are touched
-- here). There are no views, functions, or triggers left in the database.
-- The single foreign key in the whole schema (FK_PetJournal_Pet) is
-- respected by ordering PetJournal before Pet below.
--
-- Row counts at the time this script was written (informational only):
--   BlackjackGame 0, ChallengePool 18, Credits 153, FishLog 3,
--   ForgedCosmetics 0, GambleLog 1924, GuildQuoteConfig 1, Investments 17,
--   JackpotEntries 0, JournalEntries 4, JournalSubscriptions 1,
--   NamesReference 90538, NamesStaging 31904, PassiveJackpot 2,
--   PassiveJackpotContributors 0, Pet 125, PetCosmetics 3, PetEggs 0,
--   PetJournal 559, PokerLobby 1, PokerPlayer 4, PregnancyEvents 3,
--   Quotes 1, ScrambleGame 0, ServerPassiveJackpot 1, StockHistory 250,
--   StockHoldings 17, Stocks 25, StockTransactions 113, TriviaMessage 3,
--   UserActiveEffects 8, UserDailyChallenges 11, UserInventory 30,
--   WordleGame 0.
--
-- Run once against the live DiscordBot database, after taking a backup.
-- Safe to re-run (IF EXISTS guards).
-- =============================================================================
USE [DiscordBot];
GO

-- Games (Trivia, Wordle, Scramble, Poker /duel) - procs dropped in migration 011
DROP TABLE IF EXISTS [dbo].[TriviaMessage];
GO
DROP TABLE IF EXISTS [dbo].[WordleGame];
GO
DROP TABLE IF EXISTS [dbo].[ScrambleGame];
GO
DROP TABLE IF EXISTS [dbo].[PokerPlayer];
GO
DROP TABLE IF EXISTS [dbo].[PokerLobby];
GO

-- Economy / gambling (credits, jackpots, investing, blackjack, fishing) -
-- procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[GambleLog];
GO
DROP TABLE IF EXISTS [dbo].[FishLog];
GO
DROP TABLE IF EXISTS [dbo].[BlackjackGame];
GO
DROP TABLE IF EXISTS [dbo].[Investments];
GO
DROP TABLE IF EXISTS [dbo].[JackpotEntries];
GO
DROP TABLE IF EXISTS [dbo].[PassiveJackpotContributors];
GO
DROP TABLE IF EXISTS [dbo].[ServerPassiveJackpot];
GO
DROP TABLE IF EXISTS [dbo].[PassiveJackpot];
GO
DROP TABLE IF EXISTS [dbo].[Credits];
GO

-- Shop (inventory + active effects) - procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[UserActiveEffects];
GO
DROP TABLE IF EXISTS [dbo].[UserInventory];
GO

-- Stock market - procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[StockTransactions];
GO
DROP TABLE IF EXISTS [dbo].[StockHoldings];
GO
DROP TABLE IF EXISTS [dbo].[StockHistory];
GO
DROP TABLE IF EXISTS [dbo].[Stocks];
GO

-- Forge (custom cosmetics) - procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[ForgedCosmetics];
GO

-- Daily challenges - procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[UserDailyChallenges];
GO
DROP TABLE IF EXISTS [dbo].[ChallengePool];
GO

-- Quotes - procs dropped in migration 012
DROP TABLE IF EXISTS [dbo].[GuildQuoteConfig];
GO
DROP TABLE IF EXISTS [dbo].[Quotes];
GO

-- Pets / breeding - procs dropped in migration 012.
-- PetJournal dropped before Pet to satisfy FK_PetJournal_Pet.
DROP TABLE IF EXISTS [dbo].[PetJournal];
GO
DROP TABLE IF EXISTS [dbo].[PregnancyEvents];
GO
DROP TABLE IF EXISTS [dbo].[PetEggs];
GO
DROP TABLE IF EXISTS [dbo].[PetCosmetics];
GO
DROP TABLE IF EXISTS [dbo].[Pet];
GO

-- Daily journaling (/journal) - procs dropped in migration 014
DROP TABLE IF EXISTS [dbo].[JournalEntries];
GO
DROP TABLE IF EXISTS [dbo].[JournalSubscriptions];
GO

-- Orphaned reference data - NOT tied to any feature strip above. No stored
-- procedure, migration, or line of C# anywhere in this repository's history
-- references either table. Columns (Name / Sex / Count) resemble bulk
-- name-frequency data, possibly staged for a pet-name generator that never
-- shipped. Flagging separately in case you want to keep or export these.
DROP TABLE IF EXISTS [dbo].[NamesStaging];
GO
DROP TABLE IF EXISTS [dbo].[NamesReference];
GO
