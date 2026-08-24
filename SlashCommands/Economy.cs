using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Economy system — credit balance, earning, and transfer commands.
/// Credits are per-user per-server.
/// </summary>
public class Economy(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
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

    /// <summary>Shows a balance card (balance, total earned/spent, prestige, lifetime, daily streak) for yourself or another member.</summary>
    [SlashCommand("balance", "Check your credit balance.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBalanceAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();

        await EnsureAccountAsync(targetId);

        var credit = await db.Credits.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == targetId && c.ServerId == ServerId);
        if (credit is null) { await ErrorAsync("Could not load balance."); return; }

        decimal balance = credit.Balance;
        decimal totalEarned = credit.TotalEarned;
        decimal totalSpent = credit.TotalSpent;
        decimal lifetimeEarned = credit.LifetimeEarned;
        int dailyStreak = credit.DailyStreak;

        string prestigeRank = CreditHelper.PrestigeRank(lifetimeEarned);
        var (_, streakLabel) = CreditHelper.StreakMultiplier(dailyStreak);
        string streakDisplay = dailyStreak > 0
            ? $"🔥 {dailyStreak} day{(dailyStreak == 1 ? "" : "s")}" +
              (streakLabel != "" ? $" ({streakLabel})" : "")
            : "None";

        bool isSelf = target.Id == Context.User.Id;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{CreditHelper.CurrencyEmoji}  {target.Username}'s Balance", "", ColourGold,
            footer: isSelf ? "Use /daily and /work to earn more!" : Username, footerIconUrl: AvatarUrl,
            fields: [("Balance", CreditHelper.Format(balance), true),
                     ("Total Earned", CreditHelper.Format(totalEarned), true),
                     ("Total Spent", CreditHelper.Format(totalSpent), true),
                     ("🏅 Prestige", prestigeRank, true),
                     ("⭐ Lifetime", CreditHelper.Format(lifetimeEarned), true),
                     ("🔥 Daily Streak", streakDisplay, true)])
            .WithThumbnailUrl(target.GetAvatarUrl()).Build());
    }

    // ── /daily ────────────────────────────────────────────────────────────────

    /// <summary>Claims the once-per-24h daily credit reward, scaled up by the user's consecutive-day streak multiplier.</summary>
    [SlashCommand("daily", "Claim your daily credits!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleDailyAsync()
    {
        await DeferAsync();
        await EnsureAccountAsync(UserId);

        var credit = await db.Credits.FirstOrDefaultAsync(c => c.UserId == UserId && c.ServerId == ServerId);
        if (credit is null) { await ErrorAsync("Could not load account."); return; }

        // ── Cooldown check ─────────────────────────────────────────────────────
        if (credit.LastDaily is { } lastDaily)
        {
            var remaining = lastDaily.AddHours(CreditHelper.DailyCooldownHours) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                // Still show current streak so they know what they'd be protecting
                int currentStreak = credit.DailyStreak;
                var (_, streakLabel) = CreditHelper.StreakMultiplier(currentStreak);

                await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                    "⏳  Daily Already Claimed",
                    $"Come back in **{(int)remaining.TotalHours}h {remaining.Minutes}m**." +
                    (currentStreak > 0
                        ? $"\n\n🔥 Current streak: **{currentStreak} day{(currentStreak == 1 ? "" : "s")}**" +
                          (streakLabel != "" ? $" — {streakLabel}" : "")
                        : ""),
                    ColourRed, footer: Username, footerIconUrl: AvatarUrl).Build());
                return;
            }
        }

        // ── Update streak ──────────────────────────────────────────────────────
        var previousLastDaily = credit.LastDaily;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int newStreak = credit.LastStreakDate is null ? 1
            : credit.LastStreakDate.Value.DayNumber == today.DayNumber - 1 ? credit.DailyStreak + 1
            : 1;
        credit.DailyStreak = newStreak;
        credit.LastStreakDate = today;
        await db.SaveChangesAsync();

        bool streakReset = newStreak == 1 &&
            previousLastDaily is { } prev &&
            (DateTime.UtcNow - prev).TotalHours >= 48;

        // ── Compute payout ─────────────────────────────────────────────────────
        var (multiplier, streakBonusLabel) = CreditHelper.StreakMultiplier(newStreak);

        bool hasDailyBoost = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "daily_boost");
        decimal basePayout = CreditHelper.DailyAmount;
        if (hasDailyBoost)
        {
            basePayout *= 2m;
            await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "daily_boost");
        }

        // Golden Ticket: 2× | Golden Ticket II: 3× (checked after daily_boost)
        if (await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket_ii"))
            basePayout *= 3m;
        else if (await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket"))
            basePayout *= 2m;

        decimal finalPayout = Math.Floor(basePayout * multiplier);

        decimal newBalance = await CreditService.AddCreditsAsync(db, UserId, ServerId, finalPayout, "daily");

        // Challenge tracking — pay out immediately if this completes a slot. Only the "daily"
        // GameType's caller (this one) actually checks Progress==Target and pays a reward — the
        // other 17 challenge types elsewhere only ever increment progress. Confirmed pre-existing
        // and intentionally left as-is (not something to fix here).
        try
        {
            var challengeResult = await ChallengeService.IncrementProgressAsync(db, UserId, ServerId, "daily");

            if (challengeResult is not null)
            {
                foreach (var slot in new[] { challengeResult.Slot1, challengeResult.Slot2, challengeResult.Slot3 })
                {
                    if (slot.Progress == slot.Target && slot.Reward > 0m)
                        await CreditService.AddCreditsAsync(db, UserId, ServerId, slot.Reward, "challenge_daily");
                }

                await ChallengeService.ClaimBonusIfEligibleAsync(db, UserId, ServerId);
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

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🎁  Daily Claimed!", descLines.ToString(),
            multiplier >= 5m ? ColourGold : multiplier >= 2m ? new Color(255, 165, 0) : ColourGreen,
            footer: $"{Username} • Come back in 24 hours!", footerIconUrl: AvatarUrl,
            fields: [("Payout", CreditHelper.Format(finalPayout), true),
                     ("Multiplier", $"{multiplier}×", true),
                     ("Balance", CreditHelper.Format(newBalance), true)]).Build());
    }

    // ── /work ─────────────────────────────────────────────────────────────────

    /// <summary>Claims the once-per-hour /work reward: a random credit amount with a flavour-text message.</summary>
    [SlashCommand("work", "Do some work to earn credits!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleWorkAsync()
    {
        await DeferAsync();
        await EnsureAccountAsync(UserId);

        var credit = await db.Credits.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == UserId && c.ServerId == ServerId);
        if (credit is null) { await ErrorAsync("Could not load account."); return; }

        if (credit.LastWork is { } lastWork)
        {
            var remaining = lastWork.AddMinutes(CreditHelper.WorkCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                    "⏳  Still Working", $"You're still on shift! Clock back in **{remaining.Minutes}m {remaining.Seconds}s**.",
                    ColourRed, footer: Username, footerIconUrl: AvatarUrl).Build());
                return;
            }
        }

        decimal earned = Math.Floor(CreditHelper.WorkMin + (decimal)Random.Shared.NextDouble() * (CreditHelper.WorkMax - CreditHelper.WorkMin + 1m));
        // work_boost: 2× payout, decrements stack count (3 uses total)
        bool hasWorkBoost = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "work_boost");
        if (hasWorkBoost)
        {
            earned *= 2m;
            await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "work_boost");
        }

        // Golden Ticket: 2× | Golden Ticket II: 3×
        if (await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket_ii"))
            earned *= 3m;
        else if (await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket"))
            earned *= 2m;
        decimal newBalance = await CreditService.AddCreditsAsync(db, UserId, ServerId, earned, "work");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "💼  You Worked!",
            CreditHelper.WorkMessage(earned) + (hasWorkBoost ? " 💼 *(Work Boost!)*" : "") + "\n\n" +
            $"Balance: {CreditHelper.Format(newBalance)}",
            ColourGreen, footer: $"{Username} • Come back in 1 hour!", footerIconUrl: AvatarUrl).Build());
    }

    /// <summary>Transfers credits from the caller directly to another member, notifying the recipient in-channel.</summary>
    [SlashCommand("transfer", "Send credits to another user.")]
    [CommandContextType(InteractionContextType.Guild)]
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

        await EnsureAccountAsync(UserId);
        string recipientId = recipient.Id.ToString();
        await EnsureAccountAsync(recipientId, ServerId);

        decimal senderBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        if (transferAmount > senderBalance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer",
                $"You don't have enough credits. Your balance: {CreditHelper.Format(senderBalance)}",
                Username).Build(), ephemeral: true);
            return;
        }

        decimal newSenderBalance    = await CreditService.DeductCreditsAsync(db, UserId, ServerId, transferAmount, "transfer_out");
        decimal newRecipientBalance = await CreditService.AddCreditsAsync(db, recipientId, ServerId, transferAmount, "transfer_in");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{CreditHelper.CurrencyEmoji}  Transfer Complete",
            $"Sent {CreditHelper.Format(transferAmount)} to {recipient.Mention}.\n\n" +
            $"Your new balance: {CreditHelper.Format(newSenderBalance)}",
            ColourGreen, footer: Username, footerIconUrl: AvatarUrl).Build(), ephemeral: true);

        // Notify recipient via the channel
        await Context.Channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
            $"{CreditHelper.CurrencyEmoji}  Credits Received!",
            $"{Context.User.Mention} sent {CreditHelper.Format(transferAmount)} to {recipient.Mention}!\n" +
            $"{recipient.DisplayName}'s new balance: {CreditHelper.Format(newRecipientBalance)}",
            ColourGold).Build());
    }


    // ── /donate ───────────────────────────────────────────────────────────────

    /// <summary>Splits a donated amount equally across every server member who has a credit account.</summary>
    [SlashCommand("donate", "Spread your credits equally among all server members who have a balance.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleDonateAsync(
        [Summary("amount", "How many credits to donate in total.")]
        [MinValue(1)] long amount)
    {
        await DeferAsync();
        await EnsureAccountAsync(UserId);

        decimal donateAmount = (decimal)amount;
        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        if (donateAmount > balance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Donate",
                $"You don't have enough credits. Your balance: {CreditHelper.Format(balance)}",
                Username).Build());
            return;
        }

        // Fetch server members who have credits, excluding the donor — source's
        // GetCreditLeaderboard is TOP 20 by Balance DESC, so donations only ever reach the
        // richest 20 other members even if more exist. Preserved exactly.
        var recipients = await db.Credits.AsNoTracking()
            .Where(c => c.ServerId == ServerId && c.UserId != UserId)
            .OrderByDescending(c => c.Balance)
            .Take(20)
            .Select(c => c.UserId)
            .ToListAsync();

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
        decimal newBalance = await CreditService.DeductCreditsAsync(db, UserId, ServerId, totalDistributed, "donate_out");

        foreach (string recipientId in recipients)
        {
            await CreditService.AddCreditsAsync(db, recipientId, ServerId, share, "donate_in");
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{CreditHelper.CurrencyEmoji}  Donation Complete!",
            $"**{CreditHelper.Format(totalDistributed)}** spread equally across **{recipients.Count}** member{(recipients.Count == 1 ? "" : "s")}.\n\n" +
            $"Each member received: {CreditHelper.Format(share)}\n" +
            $"Your new balance: {CreditHelper.Format(newBalance)}",
            ColourGreen, footer: Username, footerIconUrl: AvatarUrl).Build());
    }

    // ── /creditleaderboard ────────────────────────────────────────────────────

    /// <summary>Shows the top credit balances in this server.</summary>
    [SlashCommand("creditleaderboard", "Show the richest users in this server.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleLeaderboardAsync()
    {
        await DeferAsync();

        // Source (GetCreditLeaderboard) LEFT JOINs Users on UserID + TRY_CAST(@ServerID AS
        // BIGINT), falling back to "User_{UserID}" when no Users row matches.
        long? serverIdLong = long.TryParse(ServerId, out long sid) ? sid : null;
        var rows = await (
            from c in db.Credits.AsNoTracking()
            where c.ServerId == ServerId
            orderby c.Balance descending
            select new
            {
                c.UserId,
                c.Balance,
                Username = db.Users.Where(u => u.UserId == c.UserId && u.ServerUid == serverIdLong)
                    .Select(u => u.Username).FirstOrDefault()
            }
        ).Take(20).ToListAsync();

        if (rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Leaderboard", "No credit data found for this server.", Username).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        var medals = new[] { "🥇", "🥈", "🥉" };

        for (int i = 0; i < rows.Count; i++)
        {
            string medal = i < 3 ? medals[i] : $"**{i + 1}.**";
            string userName = rows[i].Username ?? $"User_{rows[i].UserId}";
            decimal bal = rows[i].Balance;
            sb.AppendLine($"{medal} **{userName}** — {CreditHelper.Format(bal)}");
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"💰  Credit Leaderboard — {Context.Guild.Name}", sb.ToString(), ColourGold).Build());
    }

    // ── /prestige ─────────────────────────────────────────────────────────────

    /// <summary>Shows a member's prestige rank (based on lifetime earnings), progress to the next rank, and the full rank ladder.</summary>
    [SlashCommand("prestige", "View your prestige rank and progress.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePrestigeAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();
        await EnsureAccountAsync(targetId);

        var credit = await db.Credits.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == targetId && c.ServerId == ServerId);
        if (credit is null) { await ErrorAsync("Could not load account."); return; }

        decimal lifetimeEarned = credit.LifetimeEarned;
        int dailyStreak = credit.DailyStreak;

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

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🏅  {target.Username}'s Prestige", "",
            currentIdx >= 6 ? ColourGold : currentIdx >= 4 ? new Color(88, 101, 242) : ColourGreen,
            footer: $"{target.Username} • Earn credits to climb the ranks", footerIconUrl: AvatarUrl,
            fields: [("Current Rank", current.rank, true),
                     ("Lifetime Earned", CreditHelper.Format(lifetimeEarned), true),
                     ("Daily Multiplier", $"{streakMult}× (day {dailyStreak})", true),
                     ("Progress to Next Rank", progressSection, false),
                     ("Rank Ladder", ladder.ToString(), false)])
            .WithThumbnailUrl(target.GetAvatarUrl()).Build());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Ensures a credit account row exists for the user (slash command context, ServerId from
    /// Context). Actual credit read/write logic now lives in <see cref="CreditService"/> — other
    /// files that used to reach it via <c>new Economy()</c> (Forge, Gambling, Blackjack, Poker,
    /// Shop, Program.cs) call CreditService directly with their own DbContext instead.
    /// </summary>
    private Task EnsureAccountAsync(string userId) => EnsureAccountAsync(userId, ServerId);

    private Task EnsureAccountAsync(string userId, string serverId) => CreditService.EnsureAccountAsync(db, userId, serverId);

    /// <summary>Posts a standard Economy-branded error embed.</summary>
    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Economy", message, Username).Build());
}