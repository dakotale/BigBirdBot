using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Stock market system — buy, sell, portfolio, and market overview.
/// Prices tick every 15 minutes via BotHost timer.
/// </summary>
[Group("stock", "The Big Bird Stock Exchange.")]
public class Stock : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();
    private readonly Economy _eco = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourMarket = new(88, 101, 242);
    private static readonly Color ColourGreen = new(87, 242, 135);
    private static readonly Color ColourRed = new(237, 66, 69);
    private static readonly Color ColourGold = new(255, 215, 0);

    // ── /stock market ─────────────────────────────────────────────────────────

    [SlashCommand("market", "View all stocks and current prices.")]
    [EnabledInDm(false)]
    public async Task HandleMarketAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllStocks", []);

        if (dt.Rows.Count == 0)
        {
            await ErrorAsync("No stocks found. The market may not be initialised.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"{"TICKER",-7} {"PRICE",8} {"CHANGE",13}  {"TREND"}");
        sb.AppendLine(new string('─', 42));

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string ticker = row["Ticker"].ToString()!;
            decimal price = decimal.Parse(row["Price"].ToString()!);
            decimal prev = decimal.Parse(row["PrevPrice"].ToString()!);
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

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("📊  Big Bird Stock Exchange")
            .WithColor(ColourMarket)
            .WithDescription(sb.ToString())
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /stock info ───────────────────────────────────────────────────────────

    [SlashCommand("info", "Detailed info and price history for a stock.")]
    [EnabledInDm(false)]
    public async Task HandleInfoAsync(
        [MinLength(1), MaxLength(8)] string ticker)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        var stockDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllStocks", []);
        System.Data.DataRow? row = null;
        foreach (System.Data.DataRow r in stockDt.Rows)
            if (r["Ticker"].ToString() == ticker) { row = r; break; }

        if (row == null)
        {
            await ErrorAsync($"Ticker **{ticker}** not found.");
            return;
        }

        string company = row["CompanyName"].ToString()!;
        string sector = row["Sector"].ToString()!;
        decimal price = decimal.Parse(row["Price"].ToString()!);
        decimal prev = decimal.Parse(row["PrevPrice"].ToString()!);
        decimal high = decimal.Parse(row["High24h"].ToString()!);
        decimal low = decimal.Parse(row["Low24h"].ToString()!);

        // Fetch price history separately
        var histDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetStockHistory",
        [
            new SqlParameter("@Ticker", ticker)
        ]);

        var histPrices = new List<decimal>();
        foreach (System.Data.DataRow h in histDt.Rows)
            histPrices.Add(decimal.Parse(h["Price"].ToString()!));
        histPrices.Reverse(); // oldest first for sparkline
        histPrices.Add(price);

        string spark = StockHelper.Sparkline(histPrices);
        string change = StockHelper.FormatChange(price, prev);
        string arrow = StockHelper.TrendArrow(price, prev);
        string sEmoji = StockHelper.SectorEmoji(sector);
        Color colour = price >= prev ? ColourGreen : ColourRed;

        var eb = new EmbedBuilder()
            .WithTitle($"{sEmoji}  {company} ({ticker})")
            .WithColor(colour)
            .AddField("Price", StockHelper.FormatPrice(price), inline: true)
            .AddField("Change", $"{arrow} {change}", inline: true)
            .AddField("Sector", $"{sEmoji} {sector}", inline: true)
            .AddField("24h High", StockHelper.FormatPrice(high), inline: true)
            .AddField("24h Low", StockHelper.FormatPrice(low), inline: true);

        if (!string.IsNullOrEmpty(spark))
            eb.AddField("Price History", $"`{spark}`", inline: false);

        eb.WithFooter($"Ticker: {ticker} • Updates every {StockHelper.TickIntervalMinutes}min")
          .WithCurrentTimestamp();

        await FollowupAsync(embed: eb.Build());
    }

    // ── /stock buy ────────────────────────────────────────────────────────────

    [SlashCommand("buy", "Buy shares in a company.")]
    [EnabledInDm(false)]
    public async Task HandleBuyAsync(
        [MinLength(1), MaxLength(8)] string ticker,
        [MinValue(1), MaxValue(10000)] int shares)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        // Look up current price
        var stockDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetStockDetail",
        [
            new SqlParameter("@Ticker", ticker)
        ]);

        // GetStockDetail returns multiple result sets — use raw stock table check
        var allDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllStocks", []);
        System.Data.DataRow? stockRow = null;
        foreach (System.Data.DataRow r in allDt.Rows)
            if (r["Ticker"].ToString() == ticker) { stockRow = r; break; }

        if (stockRow == null)
        {
            await ErrorAsync($"Ticker **{ticker}** not found. Use `/stock market` to see available stocks.");
            return;
        }

        decimal priceEach = decimal.Parse(stockRow["Price"].ToString()!);
        decimal totalCost = Math.Ceiling(priceEach * shares);
        decimal balance = _eco.GetBalance(UserId, ServerId);

        if (balance < totalCost)
        {
            await ErrorAsync(
                $"You need **{CreditHelper.Format(totalCost)}** but only have **{CreditHelper.Format(balance)}**.");
            return;
        }

        // Deduct credits
        _eco.DeductCredits(UserId, ServerId, totalCost, $"stock_buy_{ticker}");

        // Record purchase
        var result = _sp.Select(Constants.Constants.discordBotConnStr, "BuyStock",
        [
            new SqlParameter("@UserID",    UserId),
            new SqlParameter("@ServerID",  ServerId),
            new SqlParameter("@Ticker",    ticker),
            new SqlParameter("@Shares",    shares),
            new SqlParameter("@PriceEach", priceEach),
            new SqlParameter("@TotalCost", totalCost)
        ]);

        int totalShares = result.Rows.Count > 0 ? int.Parse(result.Rows[0]["Shares"].ToString()!) : shares;
        decimal avgBuy = result.Rows.Count > 0 ? decimal.Parse(result.Rows[0]["AvgBuyPrice"].ToString()!) : priceEach;
        decimal newBalance = _eco.GetBalance(UserId, ServerId);

        string company = stockRow["CompanyName"].ToString()!;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📈  Bought {ticker}")
            .WithColor(ColourGreen)
            .WithDescription($"Purchased **{shares:N0} share{(shares == 1 ? "" : "s")}** of **{company}**.")
            .AddField("Price Each", StockHelper.FormatPrice(priceEach), inline: true)
            .AddField("Total Cost", CreditHelper.Format(totalCost), inline: true)
            .AddField("Total Shares", $"{totalShares:N0}", inline: true)
            .AddField("Avg Buy Price", StockHelper.FormatPrice(avgBuy), inline: true)
            .AddField("Balance Left", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /stock sell ───────────────────────────────────────────────────────────

    [SlashCommand("sell", "Sell shares you own.")]
    [EnabledInDm(false)]
    public async Task HandleSellAsync(
    [MinLength(1), MaxLength(8)] string ticker,
    [MinValue(1), MaxValue(10000000000)] int shares = 1,
    [Summary("sell_all", "Sell your entire position.")] bool sellAll = false)
    {
        await DeferAsync();

        ticker = ticker.ToUpperInvariant();

        var holdingDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetHolding",
        [
            new SqlParameter("@UserID",   UserId),
        new SqlParameter("@ServerID", ServerId),
        new SqlParameter("@Ticker",   ticker)
        ]);

        if (holdingDt.Rows.Count == 0)
        {
            await ErrorAsync($"You don't own any shares of **{ticker}**.");
            return;
        }

        int owned = int.Parse(holdingDt.Rows[0]["Shares"].ToString()!);
        decimal avgBuy = decimal.Parse(holdingDt.Rows[0]["AvgBuyPrice"].ToString()!);

        // Resolve quantity — sell_all overrides the shares param
        int qty = sellAll ? owned : shares;

        if (qty > owned)
        {
            await ErrorAsync($"You only own **{owned:N0} share{(owned == 1 ? "" : "s")}** of **{ticker}**.");
            return;
        }

        var allDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllStocks", []);
        System.Data.DataRow? stockRow = null;
        foreach (System.Data.DataRow r in allDt.Rows)
            if (r["Ticker"].ToString() == ticker) { stockRow = r; break; }

        if (stockRow == null) { await ErrorAsync($"Ticker **{ticker}** not found."); return; }

        decimal priceEach = decimal.Parse(stockRow["Price"].ToString()!);
        decimal totalGain = Math.Floor(priceEach * qty);
        decimal pnl = (priceEach - avgBuy) * qty;
        string company = stockRow["CompanyName"].ToString()!;

        _sp.Select(Constants.Constants.discordBotConnStr, "SellStock",
        [
            new SqlParameter("@UserID",    UserId),
        new SqlParameter("@ServerID",  ServerId),
        new SqlParameter("@Ticker",    ticker),
        new SqlParameter("@Shares",    qty),
        new SqlParameter("@PriceEach", priceEach),
        new SqlParameter("@TotalGain", totalGain)
        ]);

        _eco.AddCredits(UserId, ServerId, totalGain, $"stock_sell_{ticker}");
        decimal newBalance = _eco.GetBalance(UserId, ServerId);

        int remain = owned - qty;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle(sellAll ? $"📉  Sold all {ticker}" : $"📉  Sold {ticker}")
            .WithColor(pnl >= 0 ? ColourGreen : ColourRed)
            .WithDescription(sellAll
                ? $"Sold your entire position of **{qty:N0} share{(qty == 1 ? "" : "s")}** in **{company}**."
                : $"Sold **{qty:N0} share{(qty == 1 ? "" : "s")}** of **{company}**.")
            .AddField("Sale Price", StockHelper.FormatPrice(priceEach), inline: true)
            .AddField("Total Gained", CreditHelper.Format(totalGain), inline: true)
            .AddField("P&L", StockHelper.FormatPnL(pnl), inline: true)
            .AddField("Shares Left", $"{remain:N0}", inline: true)
            .AddField("New Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /stock portfolio ──────────────────────────────────────────────────────

    [SlashCommand("portfolio", "View your stock holdings and unrealized P&L.")]
    [EnabledInDm(false)]
    public async Task HandlePortfolioAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        string tid = target.Id.ToString();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPortfolio",
        [
            new SqlParameter("@UserID",   tid),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("📂  Portfolio")
                .WithColor(ColourMarket)
                .WithDescription($"**{target.Username}** owns no stocks. Use `/stock buy` to invest!")
                .WithCurrentTimestamp()
                .Build());
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

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string tkr = row["Ticker"].ToString()!;
            long shrs = long.Parse(row["Shares"].ToString()!);
            decimal avg = decimal.Parse(row["AvgBuyPrice"].ToString()!);
            decimal cur = decimal.Parse(row["CurrentPrice"].ToString()!);
            decimal pnl = decimal.Parse(row["UnrealizedPnL"].ToString()!);

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
        int winners = dt.Rows.Cast<System.Data.DataRow>()
            .Count(r => decimal.Parse(r["UnrealizedPnL"].ToString()!) > 0);
        int losers = dt.Rows.Cast<System.Data.DataRow>()
            .Count(r => decimal.Parse(r["UnrealizedPnL"].ToString()!) < 0);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📂  {target.Username}'s Portfolio")
            .WithColor(colour)
            .WithDescription(sb.ToString())
            .AddField("Value", StockHelper.FormatPrice(totalValue), inline: true)
            .AddField("Unrealized P&L", StockHelper.FormatPnL(totalPnL), inline: true)
            .AddField("Holdings", $"📈 {winners} up  ·  📉 {losers} down", inline: true)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .WithFooter(isSelf ? "P&L = unrealized gain/loss vs avg buy price" : $"Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /stock history ────────────────────────────────────────────────────────

    [SlashCommand("history", "View your recent stock transactions.")]
    [EnabledInDm(false)]
    public async Task HandleHistoryAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetStockTransactions",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🧾  Transaction History")
                .WithColor(ColourMarket)
                .WithDescription("No transactions yet.")
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"{"TYPE",-5} {"TICKER",-7} {"SHARES",6} {"EACH",9} {"TOTAL",12}  DATE");
        sb.AppendLine(new string('─', 56));

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string type = row["TxType"].ToString()!;
            string tkr = row["Ticker"].ToString()!;
            int shrs = int.Parse(row["Shares"].ToString()!);
            decimal each = decimal.Parse(row["PriceEach"].ToString()!);
            decimal total = decimal.Parse(row["TotalCost"].ToString()!);
            string date = DateTime.Parse(row["TxTime"].ToString()!).ToString("MM/dd HH:mm");
            string arrow = type == "BUY" ? "▲" : "▼";

            sb.AppendLine(
                $"{arrow}{type,-4} {tkr,-7} {shrs,6:N0} {StockHelper.FormatPrice(each),9} " +
                $"{CreditHelper.Format(total),12}  {date}");
        }

        sb.AppendLine("```");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🧾  Transaction History")
            .WithColor(ColourMarket)
            .WithDescription(sb.ToString())
            .WithFooter($"Last 10 transactions • {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Error helper ──────────────────────────────────────────────────────────

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Stock Market", message, Username).Build());
}