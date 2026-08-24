using DiscordBot.Data;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// Shared EF Core daily-challenge operations, used by Economy, Challenges, Gambling, Blackjack,
/// and Poker. Mirrors the source stored procs exactly, including the current live behavior where
/// only the "daily" challenge type's caller (Economy.HandleDailyAsync) actually reads a slot's
/// Progress/Target and pays its RewardAmount on completion — the other 17 challenge types
/// (blackjack, poker, fish variants, slots, etc.) only ever have their Progress incremented via
/// TrackChallenge-style fire-and-forget calls elsewhere; nothing pays their individual reward.
/// Confirmed with the user this is a known, pre-existing gap to replicate as-is, not fix here.
/// </summary>
public static class ChallengeService
{
    public sealed record ChallengeSlot(string Key, string Description, int Target, int Progress, decimal Reward, short Difficulty);
    public sealed record DailyChallengesResult(ChallengeSlot Slot1, ChallengeSlot Slot2, ChallengeSlot Slot3, bool BonusClaimed);

    /// <summary>Returns today's 3 assigned challenges, assigning one per difficulty tier (1/2/3) at random if none exist yet today.</summary>
    public static async Task<DailyChallengesResult?> GetOrAssignDailyChallengesAsync(DiscordbotContext db, string userId, string serverId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        bool exists = await db.UserDailyChallenges.AnyAsync(u => u.UserId == userId && u.ServerId == serverId && u.ChallengeDate == today);

        if (!exists)
        {
            // Source used ORDER BY NEWID() per difficulty tier — no clean server-side random-row
            // translation, so fetch each tier's ids and pick client-side (same pattern used
            // elsewhere in this migration for random-row picks).
            async Task<int> PickAsync(short difficulty)
            {
                var ids = await db.ChallengePools.AsNoTracking()
                    .Where(c => c.Difficulty == difficulty).Select(c => c.ChallengeId).ToListAsync();
                return ids[Random.Shared.Next(ids.Count)];
            }

            int c1Id = await PickAsync(1), c2Id = await PickAsync(2), c3Id = await PickAsync(3);
            db.UserDailyChallenges.Add(new UserDailyChallenge
            {
                UserId = userId, ServerId = serverId, ChallengeDate = today,
                Challenge1Id = c1Id, Challenge2Id = c2Id, Challenge3Id = c3Id
            });
            await db.SaveChangesAsync();
        }

        return await LoadTodayAsync(db, userId, serverId, today);
    }

    /// <summary>
    /// Increments progress on any of today's 3 slots whose ChallengePool.GameType matches
    /// gameType (clamped at each slot's target). Returns null if no challenges are assigned for
    /// today yet (matches source: its UPDATE/SELECT would simply match/return 0 rows).
    /// </summary>
    public static async Task<DailyChallengesResult?> IncrementProgressAsync(DiscordbotContext db, string userId, string serverId, string gameType)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var udc = await db.UserDailyChallenges.FirstOrDefaultAsync(u => u.UserId == userId && u.ServerId == serverId && u.ChallengeDate == today);
        if (udc is null) return null;

        var (c1, c2, c3) = await LoadPoolRowsAsync(db, udc);

        if (c1.GameType == gameType && udc.Progress1 < c1.TargetCount) udc.Progress1++;
        if (c2.GameType == gameType && udc.Progress2 < c2.TargetCount) udc.Progress2++;
        if (c3.GameType == gameType && udc.Progress3 < c3.TargetCount) udc.Progress3++;
        await db.SaveChangesAsync();

        return ToResult(udc, c1, c2, c3);
    }

    /// <summary>
    /// Marks the completion bonus as claimed if all 3 slots are done and it hasn't been claimed
    /// yet. Source computed a combined @Payout sum but neither caller ever reads or spends it —
    /// Challenges.cs's own comment confirms this is intentional ("no additional credits are
    /// issued here") — matched by not issuing anything here either, just the claimed flag.
    /// </summary>
    public static async Task<bool> ClaimBonusIfEligibleAsync(DiscordbotContext db, string userId, string serverId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var udc = await db.UserDailyChallenges.FirstOrDefaultAsync(u => u.UserId == userId && u.ServerId == serverId && u.ChallengeDate == today);
        if (udc is null || udc.BonusClaimed) return false;

        var (c1, c2, c3) = await LoadPoolRowsAsync(db, udc);
        bool eligible = udc.Progress1 >= c1.TargetCount && udc.Progress2 >= c2.TargetCount && udc.Progress3 >= c3.TargetCount;
        if (!eligible) return false;

        udc.BonusClaimed = true;
        await db.SaveChangesAsync();
        return true;
    }

    private static async Task<DailyChallengesResult?> LoadTodayAsync(DiscordbotContext db, string userId, string serverId, DateOnly today)
    {
        var udc = await db.UserDailyChallenges.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ServerId == serverId && u.ChallengeDate == today);
        if (udc is null) return null;
        var (c1, c2, c3) = await LoadPoolRowsAsync(db, udc);
        return ToResult(udc, c1, c2, c3);
    }

    private static async Task<(ChallengePool c1, ChallengePool c2, ChallengePool c3)> LoadPoolRowsAsync(DiscordbotContext db, UserDailyChallenge udc)
    {
        var c1 = await db.ChallengePools.AsNoTracking().FirstAsync(c => c.ChallengeId == udc.Challenge1Id);
        var c2 = await db.ChallengePools.AsNoTracking().FirstAsync(c => c.ChallengeId == udc.Challenge2Id);
        var c3 = await db.ChallengePools.AsNoTracking().FirstAsync(c => c.ChallengeId == udc.Challenge3Id);
        return (c1, c2, c3);
    }

    private static DailyChallengesResult ToResult(UserDailyChallenge udc, ChallengePool c1, ChallengePool c2, ChallengePool c3) => new(
        new ChallengeSlot(c1.Key, c1.Description, c1.TargetCount, udc.Progress1, c1.RewardAmount, c1.Difficulty),
        new ChallengeSlot(c2.Key, c2.Description, c2.TargetCount, udc.Progress2, c2.RewardAmount, c2.Difficulty),
        new ChallengeSlot(c3.Key, c3.Description, c3.TargetCount, udc.Progress3, c3.RewardAmount, c3.Difficulty),
        udc.BonusClaimed);
}
