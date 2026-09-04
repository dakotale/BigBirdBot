-- =============================================================================
-- Migration 012 - Decommission economy, pets, gambling, shop, stock, forge,
--                 challenges, quotes
--
-- These features were removed from the bot. Their stored procedures are no
-- longer called and are dropped here.
--
-- ALL TABLES ARE KEPT FOR ARCHIVAL and are NOT touched (Credits, Pet, PetCosmetics,
-- Stocks, Holdings, StockHistory, Inventory, UserActiveEffects, ForgedCosmetics,
-- ChallengePool, UserChallenges, Quotes, GuildQuoteConfig, Investments, JackpotEntry,
-- PassiveJackpot, BlackjackGame, Pregnancy, PetEgg, GambleLog, FishLog, ... ).
--
-- The hourly word puzzle is unaffected: GetRandomWord, AddPetWordPuzzle,
-- GetPetWordPuzzle, GetActivePetPuzzle, ClaimPetPuzzle, GetPuzzleClaimedStatus stay.
-- Game procs (poker/wordle/scramble/trivia) are dropped by migration 011.
--
-- Run once against the live DiscordBot database. Safe to re-run.
-- =============================================================================
USE [DiscordBot];
GO

-- Credits / economy ---------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddCredits];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeductCredits];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetCredits];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetCreditLeaderboard];
GO
DROP PROCEDURE IF EXISTS [dbo].[EnsureCreditAccount];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddLifetimeEarned];
GO
DROP PROCEDURE IF EXISTS [dbo].[ResetLifetimeEarned];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetStreakInfo];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateDailyStreak];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetDailyLoss];
GO
DROP PROCEDURE IF EXISTS [dbo].[HalveAllBalances];
GO
DROP PROCEDURE IF EXISTS [dbo].[ZeroAllBalances];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetUserStats];
GO

-- Gambling logs / fishing ---------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddGambleLog];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetGambleStats];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddFishLog];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetFishStats];
GO

-- Jackpots ------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddJackpotEntry];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetJackpotEntries];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetJackpotTotal];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClearJackpot];
GO
DROP PROCEDURE IF EXISTS [dbo].[DrawPassiveJackpot];
GO
DROP PROCEDURE IF EXISTS [dbo].[FeedPassiveJackpot];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClaimPassiveJackpot];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPassiveJackpot];
GO

-- Blackjack -----------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddBlackjackGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetBlackjackByUser];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateBlackjackGame];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateBlackjackMessageID];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeleteBlackjackGame];
GO

-- Stock market --------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetAllStocks];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetStockDetail];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetStockHistory];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetStockTransactions];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPortfolio];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetHolding];
GO
DROP PROCEDURE IF EXISTS [dbo].[BuyStock];
GO
DROP PROCEDURE IF EXISTS [dbo].[SellStock];
GO
DROP PROCEDURE IF EXISTS [dbo].[ApplyStockTick];
GO
DROP PROCEDURE IF EXISTS [dbo].[TickStockPrices];
GO
DROP PROCEDURE IF EXISTS [dbo].[ResetStockDayRange];
GO

-- Shop / inventory / active effects -----------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddToInventory];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeductFromInventory];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetInventoryItem];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetUserInventory];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddActiveEffect];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetActiveEffect];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetAllActiveEffects];
GO
DROP PROCEDURE IF EXISTS [dbo].[ConsumeActiveEffect];
GO
DROP PROCEDURE IF EXISTS [dbo].[CleanExpiredEffects];
GO

-- Forge ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddForgedCosmetic];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetForgedCosmetics];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetTotalForged];
GO

-- Daily challenges ----------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[GetOrAssignDailyChallenges];
GO
DROP PROCEDURE IF EXISTS [dbo].[IncrementChallengeProgress];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClaimChallengeBonus];
GO

-- Quotes --------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[InsertQuote];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetRandomQuote];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetQuotesByUser];
GO
DROP PROCEDURE IF EXISTS [dbo].[SearchQuotes];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateQuoteArchiveUrl];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetGuildQuoteConfig];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpsertGuildQuoteConfig];
GO

-- Investments ---------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddInvestment];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClaimInvestment];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPendingInvestment];
GO

-- Pets ----------------------------------------------------------------------
DROP PROCEDURE IF EXISTS [dbo].[AddPet];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetsByUser];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetActivePet];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetByID];
GO
DROP PROCEDURE IF EXISTS [dbo].[SetActivePet];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePetStats];
GO
DROP PROCEDURE IF EXISTS [dbo].[DecayPetStats];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddPetXP];
GO
DROP PROCEDURE IF EXISTS [dbo].[DeletePet];
GO
DROP PROCEDURE IF EXISTS [dbo].[RenamePet];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePetBio];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePetPicture];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdatePetAccessory];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetLeaderboard];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetJournal];
GO
DROP PROCEDURE IF EXISTS [dbo].[AddPetJournalEntry];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetCosmetics];
GO
DROP PROCEDURE IF EXISTS [dbo].[SetPetCosmetic];
GO
DROP PROCEDURE IF EXISTS [dbo].[RemovePetCosmetic];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPetExplore];
GO
DROP PROCEDURE IF EXISTS [dbo].[SetPetExplore];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClearPetExplore];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetAllActivePets];
GO
DROP PROCEDURE IF EXISTS [dbo].[WakePet];
GO

-- Breeding / eggs / pregnancy / child support -------------------------------
DROP PROCEDURE IF EXISTS [dbo].[ApplyEggStats];
GO
DROP PROCEDURE IF EXISTS [dbo].[CreatePetEgg];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetEggByID];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetPendingEggs];
GO
DROP PROCEDURE IF EXISTS [dbo].[HatchEgg];
GO
DROP PROCEDURE IF EXISTS [dbo].[CreatePregnancy];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetActivePregnancy];
GO
DROP PROCEDURE IF EXISTS [dbo].[ClearPregnancy];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetMaturePregnancies];
GO
DROP PROCEDURE IF EXISTS [dbo].[MarkPregnancyBorn];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetUserBornChildCount];
GO
DROP PROCEDURE IF EXISTS [dbo].[GetDueChildSupport];
GO
DROP PROCEDURE IF EXISTS [dbo].[UpdateChildSupportDate];
GO

PRINT 'Migration 012 complete - 103 stored procedures dropped (tables kept).';
GO
