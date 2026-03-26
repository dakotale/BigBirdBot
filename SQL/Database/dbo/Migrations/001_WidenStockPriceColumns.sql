-- =============================================================
-- Migration 001 – Widen stock-price columns from DECIMAL(12,2)
--                 to DECIMAL(18,2) to accommodate large values
--                 that accumulated before the price-cap was added.
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

-- ── Stocks table ──────────────────────────────────────────────
ALTER TABLE [dbo].[Stocks]
    ALTER COLUMN [Price]     DECIMAL(18, 2) NOT NULL;
GO

ALTER TABLE [dbo].[Stocks]
    ALTER COLUMN [PrevPrice] DECIMAL(18, 2) NOT NULL;
GO

ALTER TABLE [dbo].[Stocks]
    ALTER COLUMN [High24h]   DECIMAL(18, 2) NOT NULL;
GO

ALTER TABLE [dbo].[Stocks]
    ALTER COLUMN [Low24h]    DECIMAL(18, 2) NOT NULL;
GO

-- ── StockHistory table ────────────────────────────────────────
ALTER TABLE [dbo].[StockHistory]
    ALTER COLUMN [Price]     DECIMAL(18, 2) NOT NULL;
GO

-- ── StockHoldings (AvgBuyPrice) ───────────────────────────────
ALTER TABLE [dbo].[StockHoldings]
    ALTER COLUMN [AvgBuyPrice] DECIMAL(18, 2) NOT NULL;
GO

-- ── Optional: reset any prices that are already above the new
--    in-code cap of $9,999,999.99 back to a sane starting value.
--    Remove this block if you want to preserve legacy prices.
UPDATE [dbo].[Stocks]
SET
    [Price]     = CASE WHEN [Price]     > 9999999.99 THEN 9999999.99 ELSE [Price]     END,
    [PrevPrice] = CASE WHEN [PrevPrice] > 9999999.99 THEN 9999999.99 ELSE [PrevPrice] END,
    [High24h]   = CASE WHEN [High24h]   > 9999999.99 THEN 9999999.99 ELSE [High24h]   END,
    [Low24h]    = CASE WHEN [Low24h]    > 9999999.99 THEN 9999999.99 ELSE [Low24h]    END
WHERE
    [Price] > 9999999.99
    OR [PrevPrice] > 9999999.99
    OR [High24h]   > 9999999.99
    OR [Low24h]    > 9999999.99;
GO

DELETE FROM [dbo].[StockHistory]
WHERE [Price] > 9999999.99;
GO

PRINT 'Migration 001 complete – stock price columns widened to DECIMAL(18,2).';
GO
