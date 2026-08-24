using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Stock market system — buy, sell, portfolio, and market overview.
/// Prices tick every 15 minutes via BotHost timer.
/// </summary>
[Group("stock", "The Big Bird Stock Exchange.")]
public class Stock(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourMarket = EmbedColors.Blue;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourRed = EmbedColors.Red;
    private static readonly Color ColourGold = EmbedColors.Gold;

    // ── /stock market ─────────────────────────────────────────────────────────

    /// <summary>Shows a monospace table of every stock's current price and change since the last tick.</summary>
    [SlashCommand("market", "View all stocks and current prices.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleMarketAsync()
    {
        await DeferAsync();

        var stocks = await db.Stocks.AsNoTracking().OrderBy(s => s.Ticker).ToListAsync();

        if (stocks.Count == 0)
        {
            await ErrorAsync("No stocks found. The market may not be initialised.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"{"TICKER",-7} {"PRICE",8} {"CHANGE",13}  {"TREND"}");
        sb.AppendLine(new string('─', 42));

        foreach (var row in stocks)
        {
            string ticker = row.Ticker;
            decimal price = row.Price;
            decimal prev = row.PrevPrice;
            decimal change = price - prev;
            decimal pct = prev == 0 ? 0 : change / prev * 100;
            string sign = change >= 0 ? "+" : "";
            string arrow = change > 0 ? "▲" : change < 0 ? "▼" : "─";

            sb.AppendLine(
                $"{ticker,-7} {StockHelper.FormatPrice(price),8} " +
                $"{sign}{pct,5:N2}%  {arrow}");
        }

        sb.AppendLine("```");
        sb.AppendLine($"-# Prices update every {StockHelper.TickIntervalMinutes} minutes.");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "📊  Big Bird Stock Exchange", sb.ToString(), ColourMarket).Build());
    }

    // ── /stock info ───────────────────────────────────────────────────────────

    /// <summary>Shows one ticker's company/sector, current price, 24h high/low, and a sparkline of recent price history.</summary>
    [SlashCommand("info", "Detailed info and price history for a stock.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleInfoAsync(
        [MinLength(1), MaxLength(8)] string ticker)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        // Source called GetStockDetail here but discarded its result entirely (comment: "returns
        // multiple result sets — use raw stock table check") and re-fetched via GetAllStocks
        // filtered client-side instead. Querying by ticker directly is behaviorally identical
        // and skips the dead call + full-table fetch.
        var row = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(s => s.Ticker == ticker);

        if (row == null)
        {
            await ErrorAsync($"Ticker **{ticker}** not found.");
            return;
        }

        string company = row.CompanyName;
        string sector = row.Sector;
        decimal price = row.Price;
        decimal prev = row.PrevPrice;
        decimal high = row.High24h;
        decimal low = row.Low24h;

        // Fetch price history separately
        var histPrices = await db.StockHistories.AsNoTracking()
            .Where(h => h.Ticker == ticker).OrderByDescending(h => h.RecordedAt)
            .Take(10).Select(h => h.Price).ToListAsync();
        histPrices.Reverse(); // oldest first for sparkline
        histPrices.Add(price);

        string spark = StockHelper.Sparkline(histPrices);
        string change = StockHelper.FormatChange(price, prev);
        string arrow = StockHelper.TrendArrow(price, prev);
        string sEmoji = StockHelper.SectorEmoji(sector);
        Color colour = price >= prev ? ColourGreen : ColourRed;

        var eb = _embed.BuildSimpleEmbed(
            $"{sEmoji}  {company} ({ticker})", "", colour,
            footer: $"Ticker: {ticker} • Updates every {StockHelper.TickIntervalMinutes}min",
            fields: [("Price", StockHelper.FormatPrice(price), true),
                     ("Change", $"{arrow} {change}", true),
                     ("Sector", $"{sEmoji} {sector}", true),
                     ("24h High", StockHelper.FormatPrice(high), true),
                     ("24h Low", StockHelper.FormatPrice(low), true)]);

        if (!string.IsNullOrEmpty(spark))
            eb.AddField("Price History", $"`{spark}`", inline: false);

        await FollowupAsync(embed: eb.Build());
    }

    // ── /stock buy ────────────────────────────────────────────────────────────

    /// <summary>Buys shares of a ticker at its current price (if the user can afford it), updating their average buy price.</summary>
    [SlashCommand("buy", "Buy shares in a company.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBuyAsync(
        [MinLength(1), MaxLength(8)] string ticker,
        [MinValue(1), MaxValue(10000)] int shares)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        // Look up current price (source's separate GetStockDetail call was dead — see HandleInfoAsync)
        var stockRow = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(s => s.Ticker == ticker);

        if (stockRow == null)
        {
            await ErrorAsync($"Ticker **{ticker}** not found. Use `/stock market` to see available stocks.");
            return;
        }

        decimal priceEach = stockRow.Price;
        decimal totalCost = Math.Ceiling(priceEach * shares);
        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        if (balance < totalCost)
        {
            await ErrorAsync(
                $"You need **{CreditHelper.Format(totalCost)}** but only have **{CreditHelper.Format(balance)}**.");
            return;
        }

        // Deduct credits
        await CreditService.DeductCreditsAsync(db, UserId, ServerId, totalCost, $"stock_buy_{ticker}");

        // Record purchase — source (BuyStock) upserts the holding (weighted-average buy price
        // if one already exists) and logs a BUY transaction, in one proc call.
        var holding = await db.StockHoldings.FirstOrDefaultAsync(h => h.UserId == UserId && h.ServerId == ServerId && h.Ticker == ticker);
        if (holding is not null)
        {
            holding.AvgBuyPrice = ((holding.AvgBuyPrice * holding.Shares) + (priceEach * shares)) / (holding.Shares + shares);
            holding.Shares += shares;
        }
        else
        {
            holding = new StockHolding { UserId = UserId, ServerId = ServerId, Ticker = ticker, Shares = shares, AvgBuyPrice = priceEach };
            db.StockHoldings.Add(holding);
        }
        db.StockTransactions.Add(new StockTransaction
        {
            UserId = UserId, ServerId = ServerId, Ticker = ticker, TxType = "BUY",
            Shares = shares, PriceEach = priceEach, TotalCost = totalCost
        });
        await db.SaveChangesAsync();

        int totalShares = holding.Shares;
        decimal avgBuy = holding.AvgBuyPrice;
        decimal newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        string company = stockRow.CompanyName;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"📈  Bought {ticker}", $"Purchased **{shares:N0} share{(shares == 1 ? "" : "s")}** of **{company}**.",
            ColourGreen, footer: Username, footerIconUrl: AvatarUrl,
            fields: [("Price Each", StockHelper.FormatPrice(priceEach), true),
                     ("Total Cost", CreditHelper.Format(totalCost), true),
                     ("Total Shares", $"{totalShares:N0}", true),
                     ("Avg Buy Price", StockHelper.FormatPrice(avgBuy), true),
                     ("Balance Left", CreditHelper.Format(newBalance), true)]).Build());
    }

    // ── /stock sell ───────────────────────────────────────────────────────────

    /// <summary>Sells a quantity of shares (or the whole position via <paramref name="sellAll"/>) at the current price and credits the proceeds, reporting the realized P&amp;L.</summary>
    [SlashCommand("sell", "Sell shares you own.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleSellAsync(
    [MinLength(1), MaxLength(8)] string ticker,
    [MinValue(1), MaxValue(10000000000)] int shares = 1,
    [Summary("sell_all", "Sell your entire position.")] bool sellAll = false)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        var holding = await db.StockHoldings.FirstOrDefaultAsync(h => h.UserId == UserId && h.ServerId == ServerId && h.Ticker == ticker);

        if (holding is null)
        {
            await ErrorAsync($"You don't own any shares of **{ticker}**.");
            return;
        }

        int owned = holding.Shares;
        decimal avgBuy = holding.AvgBuyPrice;

        // Resolve quantity — sell_all overrides the shares param
        int qty = sellAll ? owned : shares;

        if (qty > owned)
        {
            await ErrorAsync($"You only own **{owned:N0} share{(owned == 1 ? "" : "s")}** of **{ticker}**.");
            return;
        }

        var stockRow = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(s => s.Ticker == ticker);

        if (stockRow == null) { await ErrorAsync($"Ticker **{ticker}** not found."); return; }

        decimal priceEach = stockRow.Price;
        decimal totalGain = Math.Floor(priceEach * qty);
        decimal pnl = (priceEach - avgBuy) * qty;
        string company = stockRow.CompanyName;

        // Source (SellStock) deletes the holding outright if selling the whole position,
        // otherwise decrements it, and logs a SELL transaction.
        if (qty == owned)
            db.StockHoldings.Remove(holding);
        else
            holding.Shares -= qty;

        db.StockTransactions.Add(new StockTransaction
        {
            UserId = UserId, ServerId = ServerId, Ticker = ticker, TxType = "SELL",
            Shares = qty, PriceEach = priceEach, TotalCost = totalGain
        });
        await db.SaveChangesAsync();

        await CreditService.AddCreditsAsync(db, UserId, ServerId, totalGain, $"stock_sell_{ticker}");
        decimal newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        int remain = owned - qty;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            sellAll ? $"📉  Sold all {ticker}" : $"📉  Sold {ticker}",
            sellAll
                ? $"Sold your entire position of **{qty:N0} share{(qty == 1 ? "" : "s")}** in **{company}**."
                : $"Sold **{qty:N0} share{(qty == 1 ? "" : "s")}** of **{company}**.",
            pnl >= 0 ? ColourGreen : ColourRed,
            footer: Username, footerIconUrl: AvatarUrl,
            fields: [("Sale Price", StockHelper.FormatPrice(priceEach), true),
                     ("Total Gained", CreditHelper.Format(totalGain), true),
                     ("P&L", StockHelper.FormatPnL(pnl), true),
                     ("Shares Left", $"{remain:N0}", true),
                     ("New Balance", CreditHelper.Format(newBalance), true)])
            .Build());
    }

    // ── /stock portfolio ──────────────────────────────────────────────────────

    /// <summary>Shows a member's (or the caller's) full stock portfolio as a monospace table with per-holding and total unrealized P&amp;L.</summary>
    [SlashCommand("portfolio", "View your stock holdings and unrealized P&L.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePortfolioAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        string tid = target.Id.ToString();

        var rows = await (
            from h in db.StockHoldings.AsNoTracking()
            join s in db.Stocks.AsNoTracking() on h.Ticker equals s.Ticker
            where h.UserId == tid && h.ServerId == ServerId && h.Shares > 0
            orderby h.Ticker
            select new { h.Ticker, h.Shares, h.AvgBuyPrice, CurrentPrice = s.Price, s.CompanyName, UnrealizedPnL = (s.Price - h.AvgBuyPrice) * h.Shares }
        ).ToListAsync();

        if (rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "📂  Portfolio", $"**{target.Username}** owns no stocks. Use `/stock buy` to invest!",
                ColourMarket).Build());
            return;
        }

        decimal totalPnL = 0;
        decimal totalValue = 0;
        decimal totalCost = 0;
        var sb = new StringBuilder();

        // ── Table header ──────────────────────────────────────────────────────
        // Columns: TICKER(5) SHARES(6) PRICE(9) CHG%(7) P&L(9)  = 46 chars + spaces
        // "PRICE" is current; CHG% is (cur-avg)/avg; P&L is compact $K/$M/$B
        sb.AppendLine("```");
        sb.AppendLine($"{"TKR",-5} {"SHARES",6}  {"PRICE",8}  {"CHG%",6}  {"P&L",9}");
        sb.AppendLine(new string('─', 44));

        foreach (var row in rows)
        {
            string tkr = row.Ticker;
            long shrs = row.Shares;
            decimal avg = row.AvgBuyPrice;
            decimal cur = row.CurrentPrice;
            decimal pnl = row.UnrealizedPnL;

            decimal chgPct = avg == 0 ? 0 : (cur - avg) / avg * 100;
            string arrow = pnl > 0 ? "▲" : pnl < 0 ? "▼" : " ";
            string chgStr = $"{arrow}{Math.Abs(chgPct):0.0}%";
            string pnlStr = StockHelper.CompactPnL(pnl);

            totalPnL += pnl;
            totalValue += cur * shrs;
            totalCost += avg * shrs;

            sb.AppendLine(
                $"{tkr,-5} {StockHelper.CompactShares(shrs),6}  " +
                $"{StockHelper.FormatPrice(cur),8}  " +
                $"{chgStr,6}  " +
                $"{pnlStr,9}");
        }

        sb.AppendLine(new string('─', 44));

        // Total row — P&L% on the whole portfolio
        decimal totalChgPct = totalCost == 0 ? 0 : (totalValue - totalCost) / totalCost * 100;
        string totalArrow = totalPnL > 0 ? "▲" : totalPnL < 0 ? "▼" : " ";
        string totalChgStr = $"{totalArrow}{Math.Abs(totalChgPct):0.0}%";

        sb.AppendLine(
            $"{"TOT",-5} {"",6}  " +
            $"{StockHelper.CompactPnL(totalValue).TrimStart('+'),8}  " +
            $"{totalChgStr,6}  " +
            $"{StockHelper.CompactPnL(totalPnL),9}");
        sb.AppendLine("```");

        Color colour = StockHelper.PnLColour(totalPnL);
        bool isSelf = target.Id == Context.User.Id;

        // Win/loss counts for the summary line
        int winners = rows.Count(r => r.UnrealizedPnL > 0);
        int losers = rows.Count(r => r.UnrealizedPnL < 0);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"📂  {target.Username}'s Portfolio", sb.ToString(), colour,
            footer: isSelf ? "P&L = unrealized gain/loss vs avg buy price" : $"Requested by {Username}",
            footerIconUrl: AvatarUrl,
            fields: [("Value", StockHelper.FormatPrice(totalValue), true),
                     ("Unrealized P&L", StockHelper.FormatPnL(totalPnL), true),
                     ("Holdings", $"📈 {winners} up  ·  📉 {losers} down", true)])
            .WithThumbnailUrl(target.GetAvatarUrl()).Build());
    }

    // ── /stock history ────────────────────────────────────────────────────────

    /// <summary>Shows the user's most recent buy/sell stock transactions as a monospace table.</summary>
    [SlashCommand("history", "View your recent stock transactions.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleHistoryAsync()
    {
        await DeferAsync();

        var rows = await db.StockTransactions.AsNoTracking()
            .Where(t => t.UserId == UserId && t.ServerId == ServerId)
            .OrderByDescending(t => t.TxTime).Take(10).ToListAsync();

        if (rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "🧾  Transaction History", "No transactions yet.", ColourMarket).Build());
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"{"TYPE",-5} {"TICKER",-7} {"SHARES",6} {"EACH",9} {"TOTAL",12}  DATE");
        sb.AppendLine(new string('─', 56));

        foreach (var row in rows)
        {
            string type = row.TxType;
            string tkr = row.Ticker;
            int shrs = row.Shares;
            decimal each = row.PriceEach;
            decimal total = row.TotalCost;
            string date = row.TxTime.ToString("MM/dd HH:mm");
            string arrow = type == "BUY" ? "▲" : "▼";

            sb.AppendLine(
                $"{arrow}{type,-4} {tkr,-7} {shrs,6:N0} {StockHelper.FormatPrice(each),9} " +
                $"{CreditHelper.Format(total),12}  {date}");
        }

        sb.AppendLine("```");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🧾  Transaction History", sb.ToString(), ColourMarket,
            footer: $"Last 10 transactions • {Username}", footerIconUrl: AvatarUrl).Build());
    }

    // ── Error helper ──────────────────────────────────────────────────────────

    /// <summary>Posts a standard stock market error embed as the interaction followup.</summary>
    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Stock Market", message, Username).Build());
}