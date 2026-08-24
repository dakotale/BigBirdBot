using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Daily challenges (/challenges) and personal stats (/stats).
/// Challenge progress is incremented by hooks in Gambling.cs, Economy.cs, and Blackjack.cs
/// via ChallengeService.IncrementProgressAsync().
/// </summary>
public class Challenges(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourGold = EmbedColors.Gold;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourBlue = EmbedColors.Blue;
    private static readonly Color ColourPurple = EmbedColors.Purple;

    // ── /challenges ───────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the user's 3 daily challenges and progress toward each. Individual challenges
    /// pay out automatically as they're completed elsewhere (via TrackChallenge hooks); this
    /// command only marks the completion bonus as claimed once so it can't be paid twice.
    /// </summary>
    [SlashCommand("challenges", "View your daily challenges and claim your bonus.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleChallengesAsync()
    {
        await DeferAsync();

        var result = await ChallengeService.GetOrAssignDailyChallengesAsync(db, UserId, ServerId);

        if (result is null)
        {
            await FollowupAsync(embed: BuildError("Could not load challenges.").Build());
            return;
        }

        // Parse the three challenges
        var challenges = new[]
        {
            (key: result.Slot1.Key, desc: result.Slot1.Description, target: result.Slot1.Target, prog: result.Slot1.Progress, reward: result.Slot1.Reward, diff: (int)result.Slot1.Difficulty),
            (key: result.Slot2.Key, desc: result.Slot2.Description, target: result.Slot2.Target, prog: result.Slot2.Progress, reward: result.Slot2.Reward, diff: (int)result.Slot2.Difficulty),
            (key: result.Slot3.Key, desc: result.Slot3.Description, target: result.Slot3.Target, prog: result.Slot3.Progress, reward: result.Slot3.Reward, diff: (int)result.Slot3.Difficulty),
        };

        bool bonusClaimed = result.BonusClaimed;
        bool allDone = challenges.All(c => c.prog >= c.target);

        // NOTE: only the "daily" challenge type's tracker (Economy.HandleDailyAsync) actually
        // pays individual challenge rewards on completion — this doc comment's claim that
        // TrackChallenge hooks pay out for every game type is not accurate for the other 17
        // challenge types (confirmed with the user, kept as-is rather than fixed here).
        // ClaimChallengeBonus only marks the bonus as claimed and prevents double-payment — no
        // additional credits are issued here, matching source exactly.
        string? claimNote = null;
        if (allDone && !bonusClaimed)
        {
            await ChallengeService.ClaimBonusIfEligibleAsync(db, UserId, ServerId);
            bonusClaimed = true;
        }

        // ── Build embed ────────────────────────────────────────────────────────
        static string DiffEmoji(int d) => d switch { 1 => "🟢", 2 => "🟡", _ => "🔴" };
        static string ProgressBar(int done, int total)
        {
            int len = 8;
            int filled = total > 0 ? Math.Clamp((int)Math.Round((double)done / total * len), 0, len) : len;
            return $"`[{"█".PadRight(filled, '█').PadRight(len, '░')}]` {done}/{total}";
        }

        var desc = new StringBuilder();
        if (claimNote is not null) desc.AppendLine(claimNote).AppendLine();

        foreach (var (key, cdesc, target, prog, reward, diff) in challenges)
        {
            bool done = prog >= target;
            string tick = done ? "✅" : DiffEmoji(diff);
            desc.AppendLine($"{tick} **{cdesc}**");
            desc.AppendLine($"　{ProgressBar(prog, target)} — {CreditHelper.Format(reward)}{(done ? " *(paid)*" : "")}");
        }

        desc.AppendLine();

        if (bonusClaimed)
            desc.AppendLine("✨ *All challenges complete for today! Come back tomorrow.*");
        else if (allDone)
            desc.AppendLine("✅ *All done! Credits have been awarded for each challenge.*");
        else
            desc.AppendLine($"-# Each challenge pays out automatically when completed.");

        // Time until reset
        var now = DateTime.UtcNow;
        var midnight = now.Date.AddDays(1);
        var timeLeft = midnight - now;
        desc.AppendLine($"-# ⏱ Resets in {(int)timeLeft.TotalHours}h {timeLeft.Minutes}m.");

        var colour = bonusClaimed ? ColourGold : allDone ? ColourGreen : ColourBlue;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "📋  Daily Challenges", desc.ToString(), colour, footer: Username, footerIconUrl: AvatarUrl).Build());
    }

    // ── /stats ────────────────────────────────────────────────────────────────

    /// <summary>Shows a profile summary (balance, prestige, streak) plus aggregated gambling and fishing statistics, for yourself or another member.</summary>
    [SlashCommand("stats", "View your gambling and fishing stats.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleStatsAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();
        bool isSelf = target.Id == Context.User.Id;

        // GetUserStats returns 3 result sets — call each SP individually
        var gambleRows = await db.GambleLogs.AsNoTracking()
            .Where(g => g.UserId == targetId && g.ServerId == ServerId)
            .GroupBy(g => g.Game)
            .Select(grp => new
            {
                Game = grp.Key,
                GamesPlayed = grp.Count(),
                TotalWagered = grp.Sum(g => g.Bet),
                Wins = grp.Count(g => g.Net > 0),
                Losses = grp.Count(g => g.Net < 0),
                Draws = grp.Count(g => g.Net == 0),
                BiggestWin = grp.Max(g => g.Net),
                BiggestLoss = grp.Min(g => g.Net),
                NetTotal = grp.Sum(g => g.Net)
            })
            .OrderByDescending(g => g.TotalWagered)
            .ToListAsync();

        // Source (GetFishStats) names this column "TotalEarned" — the original C# read it as
        // "TotalFishEarned", a column that doesn't exist; would throw for any user with fish
        // history. Fixed (not a design choice, just a mismatched name).
        var fishStats = await db.FishLogs.AsNoTracking()
            .Where(f => f.UserId == targetId && f.ServerId == ServerId)
            .GroupBy(f => 1)
            .Select(g => new
            {
                TotalCaught = g.Count(),
                TotalEarned = g.Sum(f => f.Credits),
                BiggestCatch = g.Max(f => f.Credits),
                Legendaries = g.Count(f => f.Rarity == "Legendary"),
                Rares = g.Count(f => f.Rarity == "Rare"),
                Uncommons = g.Count(f => f.Rarity == "Uncommon"),
                Commons = g.Count(f => f.Rarity == "Common"),
                Junks = g.Count(f => f.Rarity == "Junk")
            })
            .FirstOrDefaultAsync();

        var credit = await db.Credits.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == targetId && c.ServerId == ServerId);

        // ── Profile row ────────────────────────────────────────────────────────
        decimal lifetimeEarned = credit?.LifetimeEarned ?? 0m;
        int dailyStreak = credit?.DailyStreak ?? 0;
        decimal balance = credit?.Balance ?? 0m;

        var (streakMult, streakLabel) = CreditHelper.StreakMultiplier(dailyStreak);

        // ── Gambling summary ───────────────────────────────────────────────────
        var gambDesc = new StringBuilder();
        if (gambleRows.Count == 0)
        {
            gambDesc.AppendLine("*No gambling history yet.*");
        }
        else
        {
            // Aggregate totals across all games
            decimal totalWagered = 0m, totalNet = 0m;
            int totalWins = 0, totalLosses = 0, totalDraws = 0;
            decimal biggestWin = 0m, biggestLoss = 0m;

            foreach (var r in gambleRows)
            {
                totalWagered += r.TotalWagered;
                totalNet += r.NetTotal;
                totalWins += r.Wins;
                totalLosses += r.Losses;
                totalDraws += r.Draws;
                biggestWin = Math.Max(biggestWin, r.BiggestWin);
                biggestLoss = Math.Min(biggestLoss, r.BiggestLoss);
            }

            int totalGames = totalWins + totalLosses + totalDraws;
            double winRate = totalGames > 0 ? (double)totalWins / totalGames * 100 : 0;

            gambDesc.AppendLine($"**Total Wagered:** {CreditHelper.Format(totalWagered)}");
            gambDesc.AppendLine($"**Net P&L:** {CreditHelper.FormatDelta(totalNet)}");
            gambDesc.AppendLine($"**Win Rate:** {winRate:F1}% ({totalWins}W / {totalLosses}L / {totalDraws}D)");
            gambDesc.AppendLine($"**Biggest Win:** {CreditHelper.Format(biggestWin)}");
            gambDesc.AppendLine($"**Biggest Loss:** {CreditHelper.Format(biggestLoss)}");
            gambDesc.AppendLine();

            // Per-game breakdown, every game played, ordered by wagered
            gambDesc.AppendLine("**By Game:**");
            foreach (var r in gambleRows)
            {
                string netStr = r.NetTotal >= 0
                    ? $"+{CreditHelper.Format(r.NetTotal)}"
                    : $"-{CreditHelper.Format(Math.Abs(r.NetTotal))}";
                gambDesc.AppendLine($"　`{r.Game,-12}` {r.GamesPlayed,4} plays  {r.Wins,4}W  {netStr}");
            }
        }

        // ── Fish summary ───────────────────────────────────────────────────────
        var fishDesc = new StringBuilder();
        if (fishStats is null || fishStats.TotalCaught == 0)
        {
            fishDesc.AppendLine("*No fish caught yet.*");
        }
        else
        {
            fishDesc.AppendLine($"**Total Caught:** {fishStats.TotalCaught:N0}");
            fishDesc.AppendLine($"**Total Earned:** {CreditHelper.Format(fishStats.TotalEarned)}");
            fishDesc.AppendLine($"**Best Catch:** {CreditHelper.Format(fishStats.BiggestCatch)}");
            fishDesc.AppendLine();
            fishDesc.AppendLine($"🌟 Legendary: **{fishStats.Legendaries}**   🟨 Rare: **{fishStats.Rares}**");
            fishDesc.AppendLine($"🟦 Uncommon: **{fishStats.Uncommons}**   🟩 Common: **{fishStats.Commons}**   ⬜ Junk: **{fishStats.Junks}**");
        }

        // ── Prestige / profile ─────────────────────────────────────────────────
        string prestigeRank = CreditHelper.PrestigeRank(lifetimeEarned);
        string streakDisplay = dailyStreak > 0
            ? $"🔥 {dailyStreak} day{(dailyStreak == 1 ? "" : "s")}" +
              (streakLabel != "" ? $" — {streakLabel}" : "")
            : "None";

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"📊  {target.Username}'s Stats", "", ColourPurple,
            footer: isSelf ? Username : $"Viewing {target.Username}'s stats", footerIconUrl: AvatarUrl,
            fields: [("💳 Balance", CreditHelper.Format(balance), true),
                     ("⭐ Lifetime Earned", CreditHelper.Format(lifetimeEarned), true),
                     ("🏅 Prestige", prestigeRank, true),
                     ("🔥 Daily Streak", streakDisplay, true),
                     ("🎲 Gambling", gambDesc.ToString(), false),
                     ("🎣 Fishing", fishDesc.ToString(), false)])
            .WithThumbnailUrl(target.GetAvatarUrl()).Build());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a standard Challenges-branded error embed.</summary>
    private EmbedBuilder BuildError(string msg) =>
        _embed.BuildSimpleEmbed("❌  Error", msg, Color.Red, footer: Username, footerIconUrl: AvatarUrl, timestamp: false);
}