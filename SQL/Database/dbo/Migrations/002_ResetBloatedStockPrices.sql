-- =============================================================
-- Migration 002 – Reset stocks pinned at the price cap back to
--                 sensible starting prices.
--
-- Any stock whose Price = 9999999.99 (capped by migration 001)
-- is assigned a random starting price in the $10–$500 range so
-- the market is playable again.  High24h/Low24h/PrevPrice are
-- set to match so there is no phantom gain/loss on day-one.
-- StockHistory rows for that ticker are also cleared so the
-- sparkline starts fresh.
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

-- Seed a deterministic-ish spread across the board so not every
-- ticker starts at the same price.  ABS(CHECKSUM(NEWID())) gives
-- a different random int each time the row is evaluated.
UPDATE [dbo].[Stocks]
SET
    [Price]     = ROUND(10.00 + (ABS(CHECKSUM(NEWID())) % 49100) / 100.0, 2),
    [PrevPrice] = ROUND(10.00 + (ABS(CHECKSUM(NEWID())) % 49100) / 100.0, 2),
    [High24h]   = [Price],
    [Low24h]    = [Price],
    [LastUpdated] = GETUTCDATE()
WHERE [Price] >= 9999.99;
GO

-- Fix High24h / Low24h to the newly set Price (UPDATE above may
-- use a different NEWID() evaluation for Price vs High24h, so
-- just align them now).
UPDATE [dbo].[Stocks]
SET
    [High24h] = [Price],
    [Low24h]  = [Price]
WHERE [High24h] >= 9999.99
   OR [Low24h]  >= 9999.99;
GO

-- Clear bloated history so sparklines start clean for reset tickers
DELETE FROM [dbo].[StockHistory]
WHERE [Price] >= 9999.99;
GO

PRINT 'Migration 002 complete – bloated stocks reset to $10–$491 range.';
GO
