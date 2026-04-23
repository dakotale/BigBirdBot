using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Economy system — credit balance, earning, and transfer commands.
/// Credits are per-user per-server.
/// </summary>
public class Economy : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourGold = EmbedColors.Gold;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourRed = EmbedColors.Red;
    private static readonly Color ColourBlue = EmbedColors.Blue;

    // ── /balance ──────────────────────────────────────────────────────────────

    [SlashCommand("balance", "Check your credit balance.")]
    [EnabledInDm(false)]
    public async Task HandleBalanceAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();

        EnsureAccount(targetId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load balance."); return; }

        decimal balance = decimal.Parse(dt.Rows[0]["Balance"].ToString()!);
        decimal totalEarned = decimal.Parse(dt.Rows[0]["TotalEarned"].ToString()!);
        decimal totalSpent = decimal.Parse(dt.Rows[0]["TotalSpent"].ToString()!);
        decimal lifetimeEarned = decimal.Parse(dt.Rows[0]["LifetimeEarned"].ToString()!);
        int dailyStreak = int.Parse(dt.Rows[0]["DailyStreak"].ToString()!);

        string prestigeRank = CreditHelper.PrestigeRank(lifetimeEarned);
        var (_, streakLabel) = CreditHelper.StreakMultiplier(dailyStreak);
        string streakDisplay = dailyStreak > 0
            ? $"🔥 {dailyStreak} day{(dailyStreak == 1 ? "" : "s")}" +
              (streakLabel != "" ? $" ({streakLabel})" : "")
            : "None";

        bool isSelf = target.Id == Context.User.Id;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  {target.Username}'s Balance")
            .WithColor(ColourGold)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .AddField("Balance", CreditHelper.Format(balance), inline: true)
            .AddField("Total Earned", CreditHelper.Format(totalEarned), inline: true)
            .AddField("Total Spent", CreditHelper.Format(totalSpent), inline: true)
            .AddField("🏅 Prestige", prestigeRank, inline: true)
            .AddField("⭐ Lifetime", CreditHelper.Format(lifetimeEarned), inline: true)
            .AddField("🔥 Daily Streak", streakDisplay, inline: true)
            .WithFooter(isSelf ? "Use /daily and /work to earn more!" : Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /daily ────────────────────────────────────────────────────────────────

    [SlashCommand("daily", "Claim your daily credits!")]
    [EnabledInDm(false)]
    public async Task HandleDailyAsync()
    {
        await DeferAsync();
        EnsureAccount(UserId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load account."); return; }

        // ── Cooldown check ─────────────────────────────────────────────────────
        if (DateTime.TryParse(dt.Rows[0]["LastDaily"]?.ToString(), out var lastDaily))
        {
            var remaining = lastDaily.AddHours(CreditHelper.DailyCooldownHours) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                // Still show current streak so they know what they'd be protecting
                var streakDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetStreakInfo",
                [
                    new SqlParameter("@UserID",   UserId),
                    new SqlParameter("@ServerID", ServerId)
                ]);
                int currentStreak = streakDt.Rows.Count > 0
                    ? int.Parse(streakDt.Rows[0]["DailyStreak"].ToString()!)
                    : 0;
                var (_, streakLabel) = CreditHelper.StreakMultiplier(currentStreak);

                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle("⏳  Daily Already Claimed")
                    .WithColor(ColourRed)
                    .WithDescription(
                        $"Come back in **{(int)remaining.TotalHours}h {remaining.Minutes}m**." +
                        (currentStreak > 0
                            ? $"\n\n🔥 Current streak: **{currentStreak} day{(currentStreak == 1 ? "" : "s")}**" +
                              (streakLabel != "" ? $" — {streakLabel}" : "")
                            : ""))
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
                return;
            }
        }

        // ── Update streak ──────────────────────────────────────────────────────
        var streakResult = _sp.Select(Constants.Constants.discordBotConnStr, "UpdateDailyStreak",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);
        int newStreak = streakResult.Rows.Count > 0
            ? int.Parse(streakResult.Rows[0]["DailyStreak"].ToString()!)
            : 1;
        bool streakReset = newStreak == 1 &&
            DateTime.TryParse(dt.Rows[0]["LastDaily"]?.ToString(), out var prev) &&
            (DateTime.UtcNow - prev).TotalHours >= 48;

        // ── Compute payout ─────────────────────────────────────────────────────
        var (multiplier, streakBonusLabel) = CreditHelper.StreakMultiplier(newStreak);

        bool hasDailyBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "daily_boost");
        decimal basePayout = CreditHelper.DailyAmount;
        if (hasDailyBoost)
        {
            basePayout *= 2m;
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "daily_boost");
        }

        // Golden Ticket: 2× | Golden Ticket II: 3× (checked after daily_boost)
        if (ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket_ii"))
            basePayout *= 3m;
        else if (ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket"))
            basePayout *= 2m;

        decimal finalPayout = Math.Floor(basePayout * multiplier);

        decimal newBalance = AddCredits(UserId, finalPayout, "daily");

        // Challenge tracking — pay out immediately if this completes a slot
        try
        {
            var challengeDt = _sp.Select(Constants.Constants.discordBotConnStr, "IncrementChallengeProgress",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@GameType", "daily")
            ]);

            if (challengeDt.Rows.Count > 0)
            {
                var cr = challengeDt.Rows[0];
                (string p, string t, string r)[] slots = [("Progress1", "Target1", "Reward1"), ("Progress2", "Target2", "Reward2"), ("Progress3", "Target3", "Reward3")];
                bool bonusClaimed = cr["BonusClaimed"].ToString() is "1" or "True";
                foreach (var (p, t, r) in slots)
                {
                    if (int.Parse(cr[p].ToString()!) == int.Parse(cr[t].ToString()!) && decimal.Parse(cr[r].ToString()!) > 0m)
                        AddCredits(UserId, decimal.Parse(cr[r].ToString()!), "challenge_daily");
                }
                if (!bonusClaimed)
                {
                    int p1 = int.Parse(cr["Progress1"].ToString()!), t1 = int.Parse(cr["Target1"].ToString()!);
                    int p2 = int.Parse(cr["Progress2"].ToString()!), t2 = int.Parse(cr["Target2"].ToString()!);
                    int p3 = int.Parse(cr["Progress3"].ToString()!), t3 = int.Parse(cr["Target3"].ToString()!);
                    if (p1 >= t1 && p2 >= t2 && p3 >= t3)
                        _sp.Select(Constants.Constants.discordBotConnStr, "ClaimChallengeBonus",
                            [new SqlParameter("@UserID", UserId), new SqlParameter("@ServerID", ServerId)]);
                }
            }
        }
        catch { }

        // ── Build embed ────────────────────────────────────────────────────────
        // Streak progress bar (up to day 30)
        int[] milestones = [3, 5, 7, 14, 30];
        int nextMilestone = milestones.FirstOrDefault(m => m > newStreak, 30);
        int prevMilestone = milestones.LastOrDefault(m => m <= newStreak, 0);
        int segTotal = nextMilestone - prevMilestone;
        int segDone = newStreak - prevMilestone;
        int barLen = 10;
        int filled = segTotal > 0 ? (int)Math.Round((double)segDone / segTotal * barLen) : barLen;
        string bar = new string('█', filled) + new string('░', barLen - filled);

        var descLines = new System.Text.StringBuilder();
        descLines.AppendLine($"You claimed **{CreditHelper.Format(finalPayout)}**!");
        if (hasDailyBoost) descLines.AppendLine("🎁 *Daily Boost applied (2×)!*");
        if (multiplier > 1m) descLines.AppendLine($"{streakBonusLabel} (**{multiplier}×** bonus applied)");
        if (streakReset) descLines.AppendLine("💔 *Your streak was reset — you missed a day.*");
        descLines.AppendLine();
        descLines.AppendLine($"🔥 **Streak:** {newStreak} day{(newStreak == 1 ? "" : "s")}");
        descLines.AppendLine($"`[{bar}]` → Day {nextMilestone}");

        // Next tier hint
        var (nextMult, nextLabel) = CreditHelper.StreakMultiplier(nextMilestone);
        if (nextMilestone > newStreak)
            descLines.AppendLine($"-# Reach day {nextMilestone} for **{nextMult}×** daily rewards.");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎁  Daily Claimed!")
            .WithColor(multiplier >= 5m ? ColourGold :
                       multiplier >= 2m ? new Color(255, 165, 0) : ColourGreen)
            .WithDescription(descLines.ToString())
            .AddField("Payout", CreditHelper.Format(finalPayout), inline: true)
            .AddField("Multiplier", $"{multiplier}×", inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter($"{Username} • Come back in 24 hours!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /work ─────────────────────────────────────────────────────────────────

    [SlashCommand("work", "Do some work to earn credits!")]
    [EnabledInDm(false)]
    public async Task HandleWorkAsync()
    {
        await DeferAsync();
        EnsureAccount(UserId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load account."); return; }

        if (DateTime.TryParse(dt.Rows[0]["LastWork"]?.ToString(), out var lastWork))
        {
            var remaining = lastWork.AddMinutes(CreditHelper.WorkCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle("⏳  Still Working")
                    .WithColor(ColourRed)
                    .WithDescription($"You're still on shift! Clock back in **{remaining.Minutes}m {remaining.Seconds}s**.")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
                return;
            }
        }

        decimal earned = Math.Floor(CreditHelper.WorkMin + (decimal)Random.Shared.NextDouble() * (CreditHelper.WorkMax - CreditHelper.WorkMin + 1m));
        // work_boost: 2× payout, decrements stack count (3 uses total)
        bool hasWorkBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "work_boost");
        if (hasWorkBoost)
        {
            earned *= 2m;
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "work_boost");
        }

        // Golden Ticket: 2× | Golden Ticket II: 3×
        if (ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket_ii"))
            earned *= 3m;
        else if (ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket"))
            earned *= 2m;
        decimal newBalance = AddCredits(UserId, earned, "work");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("💼  You Worked!")
            .WithColor(ColourGreen)
            .WithDescription(
                CreditHelper.WorkMessage(earned) + (hasWorkBoost ? " 💼 *(Work Boost!)*" : "") + "\n\n" +
                $"Balance: {CreditHelper.Format(newBalance)}")
            .WithFooter($"{Username} • Come back in 1 hour!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    [SlashCommand("transfer", "Send credits to another user.")]
    [EnabledInDm(false)]
    public async Task HandleTransferAsync(
        SocketGuildUser recipient,
        [MinValue(1)] long amount)
    {
        await DeferAsync(ephemeral: true);

        decimal transferAmount = (decimal)amount;

        if (recipient.Id == Context.User.Id)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer", "You can't transfer credits to yourself.", Username).Build(), ephemeral: true);
            return;
        }

        if (recipient.IsBot)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer", "You can't transfer credits to a bot.", Username).Build(), ephemeral: true);
            return;
        }

        EnsureAccount(UserId);
        string recipientId = recipient.Id.ToString();
        EnsureAccount(recipientId, ServerId);

        decimal senderBalance = GetBalance(UserId);

        if (transferAmount > senderBalance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer",
                $"You don't have enough credits. Your balance: {CreditHelper.Format(senderBalance)}",
                Username).Build(), ephemeral: true);
            return;
        }

        decimal newSenderBalance    = DeductCredits(UserId, transferAmount, "transfer_out");
        decimal newRecipientBalance = AddCredits(recipientId, ServerId, transferAmount, "transfer_in");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  Transfer Complete")
            .WithColor(ColourGreen)
            .WithDescription(
                $"Sent {CreditHelper.Format(transferAmount)} to {recipient.Mention}.\n\n" +
                $"Your new balance: {CreditHelper.Format(newSenderBalance)}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);

        // Notify recipient via the channel
        await Context.Channel.SendMessageAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  Credits Received!")
            .WithColor(ColourGold)
            .WithDescription(
                $"{Context.User.Mention} sent {CreditHelper.Format(transferAmount)} to {recipient.Mention}!\n" +
                $"{recipient.DisplayName}'s new balance: {CreditHelper.Format(newRecipientBalance)}")
            .WithCurrentTimestamp()
            .Build());
    }


    // ── /donate ───────────────────────────────────────────────────────────────

    [SlashCommand("donate", "Spread your credits equally among all server members who have a balance.")]
    [EnabledInDm(false)]
    public async Task HandleDonateAsync(
        [Summary("amount", "How many credits to donate in total.")]
        [MinValue(1)] long amount)
    {
        await DeferAsync();
        EnsureAccount(UserId);

        decimal donateAmount = (decimal)amount;
        decimal balance = GetBalance(UserId);

        if (donateAmount > balance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Donate",
                $"You don't have enough credits. Your balance: {CreditHelper.Format(balance)}",
                Username).Build());
            return;
        }

        // Fetch all server members who have credits, excluding the donor
        var lbDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCreditLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        var recipients = lbDt.Rows.Cast<System.Data.DataRow>()
            .Where(r => r["UserID"]?.ToString() != UserId)
            .ToList();

        if (recipients.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Donate",
                "There are no other members with credits in this server to donate to.",
                Username).Build());
            return;
        }

        decimal share = Math.Floor(donateAmount / recipients.Count);

        if (share < 1m)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Donate",
                $"Your donation of {CreditHelper.Format(donateAmount)} is too small to split among " +
                $"**{recipients.Count}** members (less than {CreditHelper.CurrencyEmoji} **1** each). " +
                $"Donate at least {CreditHelper.Format(recipients.Count)}.",
                Username).Build());
            return;
        }

        decimal totalDistributed = share * recipients.Count;
        decimal newBalance = DeductCredits(UserId, totalDistributed, "donate_out");

        foreach (System.Data.DataRow row in recipients)
        {
            string recipientId = row["UserID"].ToString()!;
            AddCredits(recipientId, ServerId, share, "donate_in");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  Donation Complete!")
            .WithColor(ColourGreen)
            .WithDescription(
                $"**{CreditHelper.Format(totalDistributed)}** spread equally across **{recipients.Count}** member{(recipients.Count == 1 ? "" : "s")}.\n\n" +
                $"Each member received: {CreditHelper.Format(share)}\n" +
                $"Your new balance: {CreditHelper.Format(newBalance)}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /creditleaderboard ────────────────────────────────────────────────────

    [SlashCommand("creditleaderboard", "Show the richest users in this server.")]
    [EnabledInDm(false)]
    public async Task HandleLeaderboardAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCreditLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Leaderboard", "No credit data found for this server.", Username).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        var medals = new[] { "🥇", "🥈", "🥉" };

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            string medal = i < 3 ? medals[i] : $"**{i + 1}.**";
            string userName = dt.Rows[i]["Username"].ToString()!;
            decimal bal = decimal.Parse(dt.Rows[i]["Balance"].ToString()!);
            sb.AppendLine($"{medal} **{userName}** — {CreditHelper.Format(bal)}");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"💰  Credit Leaderboard — {Context.Guild.Name}")
            .WithColor(ColourGold)
            .WithDescription(sb.ToString())
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /prestige ─────────────────────────────────────────────────────────────

    [SlashCommand("prestige", "View your prestige rank and progress.")]
    [EnabledInDm(false)]
    public async Task HandlePrestigeAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();
        EnsureAccount(targetId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load account."); return; }

        decimal lifetimeEarned = decimal.Parse(dt.Rows[0]["LifetimeEarned"].ToString()!);
        int dailyStreak = int.Parse(dt.Rows[0]["DailyStreak"].ToString()!);

        // ── Build rank ladder ──────────────────────────────────────────────────
        var tiers = new (decimal threshold, string rank, string title)[]
        {
            (0m,                 "🪨 Broke",    ""),
            (1_000_000m,         "🥉 Bronze",   "Bronze Roller"),
            (10_000_000m,        "🥈 Silver",   "Silver Shark"),
            (100_000_000m,       "🥇 Gold",     "Gold Gambler"),
            (1_000_000_000m,     "💎 Diamond",  "Diamond Dealer"),
            (10_000_000_000m,    "👑 Elite",    "Elite Earner"),
            (100_000_000_000m,   "🌟 Legend",   "Living Legend"),
            (1_000_000_000_000m, "🚀 Mythic",   "Mythic Overlord"),
        };

        int currentIdx = 0;
        for (int i = 0; i < tiers.Length; i++)
            if (lifetimeEarned >= tiers[i].threshold) currentIdx = i;

        var current = tiers[currentIdx];
        bool isMax = currentIdx == tiers.Length - 1;
        var next = isMax ? current : tiers[currentIdx + 1];

        // Progress bar to next tier
        string progressSection;
        if (isMax)
        {
            progressSection = $"```\n[██████████] MAX RANK\n```";
        }
        else
        {
            decimal fromPrev = lifetimeEarned - current.threshold;
            decimal toNext = next.threshold - current.threshold;
            int barLen = 12;
            int filled = (int)Math.Clamp(Math.Round((double)fromPrev / (double)toNext * barLen), 0, barLen);
            string bar = new string('█', filled) + new string('░', barLen - filled);
            string pct = ((double)fromPrev / (double)toNext * 100).ToString("F1");
            progressSection =
                $"```\n[{bar}] {pct}%\n```" +
                $"**{CreditHelper.Format(lifetimeEarned)}** / **{CreditHelper.Format(next.threshold)}**\n" +
                $"-# {CreditHelper.Format(next.threshold - lifetimeEarned)} more to reach {next.rank}";
        }

        // Full rank ladder
        var ladder = new System.Text.StringBuilder();
        for (int i = 0; i < tiers.Length; i++)
        {
            bool isCurrent = i == currentIdx;
            bool unlocked = lifetimeEarned >= tiers[i].threshold;
            string prefix = isCurrent ? "▶ " : (unlocked ? "✅ " : "   ");
            string threshStr = tiers[i].threshold == 0m ? "Start" : CreditHelper.Format(tiers[i].threshold);
            string titleStr = tiers[i].title != "" ? $" — *\"{tiers[i].title}\"*" : "";
            ladder.AppendLine($"{prefix}`{tiers[i].rank}`{titleStr} ({threshStr})");
        }

        var (streakMult, _) = CreditHelper.StreakMultiplier(dailyStreak);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🏅  {target.Username}'s Prestige")
            .WithColor(currentIdx >= 6 ? ColourGold :
                       currentIdx >= 4 ? new Color(88, 101, 242) : ColourGreen)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .AddField("Current Rank", current.rank, inline: true)
            .AddField("Lifetime Earned", CreditHelper.Format(lifetimeEarned), inline: true)
            .AddField("Daily Multiplier", $"{streakMult}× (day {dailyStreak})", inline: true)
            .AddField("Progress to Next Rank", progressSection, inline: false)
            .AddField("Rank Ladder", ladder.ToString(), inline: false)
            .WithFooter($"{target.Username} • Earn credits to climb the ranks", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnsureAccount(string userId) =>
        EnsureAccount(userId, ServerId);

    private void EnsureAccount(string userId, string serverId) =>
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "EnsureCreditAccount",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId)
        ]);

    /// <summary>Adds credits (slash command context — uses ServerId from Context).</summary>
    public decimal AddCredits(string userId, decimal amount, string source) =>
        AddCredits(userId, ServerId, amount, source);

    /// <summary>Adds credits with explicit serverId — safe to call from BotHost.</summary>
    public decimal AddCredits(string userId, string serverId, decimal amount, string source)
    {
        EnsureAccount(userId, serverId);
        var result = _sp.Select(Constants.Constants.discordBotConnStr, "AddCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId),
            new SqlParameter("@Amount",   amount),
            new SqlParameter("@Source",   source)
        ]);

        // Keep LifetimeEarned in sync for every positive credit flow
        try
        {
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddLifetimeEarned",
            [
                new SqlParameter("@UserID",   userId),
                new SqlParameter("@ServerID", serverId),
                new SqlParameter("@Amount",   amount)
            ]);
        }
        catch { }

        return result.Rows.Count > 0 ? decimal.Parse(result.Rows[0]["Balance"].ToString()!) : 0m;
    }

    /// <summary>Deducts credits (slash command context).</summary>
    public decimal DeductCredits(string userId, decimal amount, string source) =>
        DeductCredits(userId, ServerId, amount, source);

    /// <summary>Deducts credits with explicit serverId — safe to call from BotHost.</summary>
    public decimal DeductCredits(string userId, string serverId, decimal amount, string source)
    {
        var result = _sp.Select(Constants.Constants.discordBotConnStr, "DeductCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId),
            new SqlParameter("@Amount",   amount),
            new SqlParameter("@Source",   source)
        ]);
        return result.Rows.Count > 0 ? decimal.Parse(result.Rows[0]["Balance"].ToString()!) : -1m;
    }

    /// <summary>Gets balance (slash command context).</summary>
    public decimal GetBalance(string userId) =>
        GetBalance(userId, ServerId);

    /// <summary>Gets balance with explicit serverId — safe to call from BotHost.</summary>
    public decimal GetBalance(string userId, string serverId)
    {
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId)
        ]);
        return dt.Rows.Count > 0 ? decimal.Parse(dt.Rows[0]["Balance"].ToString()!) : 0m;
    }

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Economy", message, Username).Build());
}