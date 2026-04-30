using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Microsoft.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Daily challenges (/challenges) and personal stats (/stats).
/// Challenge progress is incremented by hooks in Gambling.cs, Economy.cs,
/// and Blackjack.cs via ChallengeHelper.Increment().
/// </summary>
public class Challenges : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly Economy _eco = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourGold = EmbedColors.Gold;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourBlue = EmbedColors.Blue;
    private static readonly Color ColourPurple = EmbedColors.Purple;

    // ── /challenges ───────────────────────────────────────────────────────────

    [SlashCommand("challenges", "View your daily challenges and claim your bonus.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleChallengesAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetOrAssignDailyChallenges",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: BuildError("Could not load challenges.").Build());
            return;
        }

        var row = dt.Rows[0];

        // Parse the three challenges
        var challenges = new[]
        {
            (
                key:    row["C1Key"].ToString()!,
                desc:   row["C1Desc"].ToString()!,
                target: int.Parse(row["C1Target"].ToString()!),
                prog:   int.Parse(row["Progress1"].ToString()!),
                reward: decimal.Parse(row["C1Reward"].ToString()!),
                diff:   int.Parse(row["C1Diff"].ToString()!)
            ),
            (
                key:    row["C2Key"].ToString()!,
                desc:   row["C2Desc"].ToString()!,
                target: int.Parse(row["C2Target"].ToString()!),
                prog:   int.Parse(row["Progress2"].ToString()!),
                reward: decimal.Parse(row["C2Reward"].ToString()!),
                diff:   int.Parse(row["C2Diff"].ToString()!)
            ),
            (
                key:    row["C3Key"].ToString()!,
                desc:   row["C3Desc"].ToString()!,
                target: int.Parse(row["C3Target"].ToString()!),
                prog:   int.Parse(row["Progress3"].ToString()!),
                reward: decimal.Parse(row["C3Reward"].ToString()!),
                diff:   int.Parse(row["C3Diff"].ToString()!)
            ),
        };

        bool bonusClaimed = row["BonusClaimed"].ToString() == "1" || row["BonusClaimed"].ToString() == "True";
        bool allDone = challenges.All(c => c.prog >= c.target);

        // Individual challenge rewards are paid automatically via TrackChallenge when each
        // challenge completes. ClaimChallengeBonus is used only to mark the bonus as claimed
        // and prevent double-payment — no additional credits are issued here.
        string? claimNote = null;
        if (allDone && !bonusClaimed)
        {
            _sp.Select(Constants.Constants.discordBotConnStr, "ClaimChallengeBonus",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId)
            ]);
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

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("📋  Daily Challenges")
            .WithColor(colour)
            .WithDescription(desc.ToString())
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── /stats ────────────────────────────────────────────────────────────────

    [SlashCommand("stats", "View your gambling and fishing stats.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleStatsAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();
        bool isSelf = target.Id == Context.User.Id;

        // GetUserStats returns 3 result sets — call each SP individually
        var gambleTable = _sp.Select(Constants.Constants.discordBotConnStr, "GetGambleStats",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        var fishTable = _sp.Select(Constants.Constants.discordBotConnStr, "GetFishStats",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        var profileTable = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        // ── Profile row ────────────────────────────────────────────────────────
        decimal lifetimeEarned = 0m;
        int dailyStreak = 0;
        decimal balance = 0m;

        if (profileTable.Rows.Count > 0)
        {
            lifetimeEarned = decimal.Parse(profileTable.Rows[0]["LifetimeEarned"].ToString()!);
            dailyStreak = int.Parse(profileTable.Rows[0]["DailyStreak"].ToString()!);
            balance = decimal.Parse(profileTable.Rows[0]["Balance"].ToString()!);
        }

        var (streakMult, streakLabel) = CreditHelper.StreakMultiplier(dailyStreak);

        // ── Gambling summary ───────────────────────────────────────────────────
        var gambDesc = new StringBuilder();
        if (gambleTable.Rows.Count == 0)
        {
            gambDesc.AppendLine("*No gambling history yet.*");
        }
        else
        {
            // Aggregate totals across all games
            decimal totalWagered = 0m, totalNet = 0m;
            int totalWins = 0, totalLosses = 0, totalDraws = 0;
            decimal biggestWin = 0m, biggestLoss = 0m;

            foreach (System.Data.DataRow r in gambleTable.Rows)
            {
                totalWagered += decimal.Parse(r["TotalWagered"].ToString()!);
                totalNet += decimal.Parse(r["NetTotal"].ToString()!);
                totalWins += int.Parse(r["Wins"].ToString()!);
                totalLosses += int.Parse(r["Losses"].ToString()!);
                totalDraws += int.Parse(r["Draws"].ToString()!);
                biggestWin = Math.Max(biggestWin, decimal.Parse(r["BiggestWin"].ToString()!));
                biggestLoss = Math.Min(biggestLoss, decimal.Parse(r["BiggestLoss"].ToString()!));
            }

            int totalGames = totalWins + totalLosses + totalDraws;
            double winRate = totalGames > 0 ? (double)totalWins / totalGames * 100 : 0;

            gambDesc.AppendLine($"**Total Wagered:** {CreditHelper.Format(totalWagered)}");
            gambDesc.AppendLine($"**Net P&L:** {CreditHelper.FormatDelta(totalNet)}");
            gambDesc.AppendLine($"**Win Rate:** {winRate:F1}% ({totalWins}W / {totalLosses}L / {totalDraws}D)");
            gambDesc.AppendLine($"**Biggest Win:** {CreditHelper.Format(biggestWin)}");
            gambDesc.AppendLine($"**Biggest Loss:** {CreditHelper.Format(biggestLoss)}");
            gambDesc.AppendLine();

            // Per-game breakdown (top 5 by wagered)
            gambDesc.AppendLine("**By Game:**");
            foreach (System.Data.DataRow r in gambleTable.Rows)
            {
                string game = r["Game"].ToString()!;
                int played = int.Parse(r["GamesPlayed"].ToString()!);
                int wins = int.Parse(r["Wins"].ToString()!);
                decimal net = decimal.Parse(r["NetTotal"].ToString()!);
                string netStr = net >= 0
                    ? $"+{CreditHelper.Format(net)}"
                    : $"-{CreditHelper.Format(Math.Abs(net))}";
                gambDesc.AppendLine($"　`{game,-12}` {played,4} plays  {wins,4}W  {netStr}");
            }
        }

        // ── Fish summary ───────────────────────────────────────────────────────
        var fishDesc = new StringBuilder();
        if (fishTable.Rows.Count == 0 || fishTable.Rows[0]["TotalCaught"].ToString() == "0")
        {
            fishDesc.AppendLine("*No fish caught yet.*");
        }
        else
        {
            var fr = fishTable.Rows[0];
            int total = int.Parse(fr["TotalCaught"].ToString()!);
            decimal earn = decimal.Parse(fr["TotalFishEarned"].ToString()!);
            decimal best = decimal.Parse(fr["BiggestCatch"].ToString()!);
            int leg = int.Parse(fr["Legendaries"].ToString()!);
            int rare = int.Parse(fr["Rares"].ToString()!);
            int unc = int.Parse(fr["Uncommons"].ToString()!);
            int com = int.Parse(fr["Commons"].ToString()!);
            int junk = int.Parse(fr["Junks"].ToString()!);

            fishDesc.AppendLine($"**Total Caught:** {total:N0}");
            fishDesc.AppendLine($"**Total Earned:** {CreditHelper.Format(earn)}");
            fishDesc.AppendLine($"**Best Catch:** {CreditHelper.Format(best)}");
            fishDesc.AppendLine();
            fishDesc.AppendLine($"🌟 Legendary: **{leg}**   🟨 Rare: **{rare}**");
            fishDesc.AppendLine($"🟦 Uncommon: **{unc}**   🟩 Common: **{com}**   ⬜ Junk: **{junk}**");
        }

        // ── Prestige / profile ─────────────────────────────────────────────────
        string prestigeRank = CreditHelper.PrestigeRank(lifetimeEarned);
        string streakDisplay = dailyStreak > 0
            ? $"🔥 {dailyStreak} day{(dailyStreak == 1 ? "" : "s")}" +
              (streakLabel != "" ? $" — {streakLabel}" : "")
            : "None";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📊  {target.Username}'s Stats")
            .WithColor(ColourPurple)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .AddField("💳 Balance", CreditHelper.Format(balance), inline: true)
            .AddField("⭐ Lifetime Earned", CreditHelper.Format(lifetimeEarned), inline: true)
            .AddField("🏅 Prestige", prestigeRank, inline: true)
            .AddField("🔥 Daily Streak", streakDisplay, inline: true)
            .AddField("🎲 Gambling", gambDesc.ToString(), inline: false)
            .AddField("🎣 Fishing", fishDesc.ToString(), inline: false)
            .WithFooter(isSelf ? Username : $"Viewing {target.Username}'s stats", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EmbedBuilder BuildError(string msg) =>
        new EmbedBuilder()
            .WithTitle("❌  Error")
            .WithColor(Color.Red)
            .WithDescription(msg)
            .WithFooter(Username, AvatarUrl);
}