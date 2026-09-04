-- =============================================================================
-- Migration 011 - Decommission the games section
--
-- The /game minigames (Trivia, Wordle, Scramble, Poker) and the /duel command
-- have been removed from the bot. Their stored procedures and functions are no
-- longer called and are dropped here.
--
-- TABLES ARE KEPT FOR ARCHIVAL and are NOT touched:
--   WordleGame, ScrambleGame, PokerLobby, PokerPlayer, TriviaMessage
--
-- The hourly Bonus Word Puzzle stays and is unaffected - its procs
-- (GetRandomWord, AddPetWordPuzzle, GetPetWordPuzzle, GetActivePetPuzzle,
-- ClaimPetPuzzle, GetPuzzleClaimedStatus) and tables (PetWordPuzzle, Words)
-- are untouched.
--
-- Run once against the live DiscordBot database. Safe to re-run.
-- =============================================================================
USE [DiscordBot];
GO

-- Wordle -----------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetWordleByChannel];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddWordleGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateWordleGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteWordleGame];
GO

-- Scramble -------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetScrambleByChannel];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddScrambleGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteScrambleGame];
GO

-- Poker --------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetPokerGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPokerGameById];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPokerPlayers];
GO
DROP PROCEDURE IF EXISTS [dbo].[CreatePokerGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddPokerPlayer];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePokerDeck];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePokerMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePokerStatus];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeletePokerGame];
GO

-- Trivia -----------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetTrivia];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetTriviaToken];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetTriviaMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddTriviaMessage];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteTriviaMessage];
GO
DROP FUNCTION  IF EXISTS [dbo].[GetTriviaTable];
GO
DROP FUNCTION  IF EXISTS [dbo].[GetTriviaTokenFromAPI];
GO

-- Daily challenge pool -------------------------------------------------
-- "Win a poker hand" (win_poker_1) could only be completed via /game poker.
-- Remove it so GetOrAssignDailyChallenges stops handing out a dead challenge.
DELETE FROM dbo.ChallengePool WHERE [Key] = 'win_poker_1';
GO

PRINT 'Migration 011 complete - 21 game procedures + 2 functions dropped, win_poker_1 challenge removed.';
GO
