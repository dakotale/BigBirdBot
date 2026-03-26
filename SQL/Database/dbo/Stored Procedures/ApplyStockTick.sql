USE [DiscordBot]
GO

-- Apply a pre-calculated new price for one ticker
CREATE OR ALTER PROCEDURE [dbo].[ApplyStockTick]
    @Ticker     VARCHAR(8),
    @NewPrice   DECIMAL(18, 2)   -- widened from (12,2); matches StockHelper.MaxPrice ceiling
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Stocks
    SET     PrevPrice   = Price,
            Price       = @NewPrice,
            High24h     = CASE WHEN @NewPrice > High24h THEN @NewPrice ELSE High24h END,
            Low24h      = CASE WHEN @NewPrice < Low24h  THEN @NewPrice ELSE Low24h  END,
            LastUpdated = GETUTCDATE()
    WHERE   Ticker = @Ticker;

    -- Archive to history
    INSERT INTO StockHistory (Ticker, Price) VALUES (@Ticker, @NewPrice);

    -- Keep only last 10 entries per ticker
    DELETE FROM StockHistory
    WHERE  Ticker = @Ticker
      AND  HistoryID NOT IN (
            SELECT TOP 10 HistoryID
            FROM   StockHistory
            WHERE  Ticker = @Ticker
            ORDER BY RecordedAt DESC
      );
END
GO
