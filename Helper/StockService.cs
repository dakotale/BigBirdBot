using DiscordBot.Data;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// Shared EF Core stock-price operations, mirroring the source ApplyStockTick proc exactly.
/// Used by Shop.cs's Market Crash item and Program.cs's stock-price scheduler.
/// </summary>
public static class StockService
{
    /// <summary>
    /// Applies a pre-calculated new price to one ticker: updates Price/PrevPrice/High24h/Low24h/
    /// LastUpdated and archives the tick to StockHistory, pruned to the 10 most recent rows.
    /// No-op if the ticker doesn't exist.
    /// </summary>
    public static async Task ApplyTickAsync(DiscordbotContext db, string ticker, decimal newPrice)
    {
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Ticker == ticker);
        if (stock is null) return;

        stock.PrevPrice = stock.Price;
        stock.Price = newPrice;
        if (newPrice > stock.High24h) stock.High24h = newPrice;
        if (newPrice < stock.Low24h) stock.Low24h = newPrice;
        stock.LastUpdated = DateTime.UtcNow;

        db.StockHistories.Add(new StockHistory { Ticker = ticker, Price = newPrice });
        await db.SaveChangesAsync();

        var oldIds = await db.StockHistories.AsNoTracking()
            .Where(h => h.Ticker == ticker)
            .OrderByDescending(h => h.RecordedAt)
            .Skip(10)
            .Select(h => h.HistoryId)
            .ToListAsync();
        if (oldIds.Count > 0)
        {
            db.StockHistories.RemoveRange(db.StockHistories.Where(h => oldIds.Contains(h.HistoryId)));
            await db.SaveChangesAsync();
        }
    }
}
