using DiscordBot.Data;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// Shared EF Core operations for the passive jackpot pool (fed automatically by 1% of every
/// gambling bet, as opposed to the direct-entry /jackpot pool in JackpotEntries). Used by
/// Program.cs's hourly scheduler draw, Gambling.cs, and Shop.cs.
///
/// NOTE: the source schema has two passive-jackpot tables — PassiveJackpot (ServerID BIGINT,
/// 2 columns) and ServerPassiveJackpot (ServerID VARCHAR, has a LastUpdated column). Confirmed
/// via SQL Server's sys.sql_expression_dependencies that only PassiveJackpot is referenced by
/// any stored proc (FeedPassiveJackpot/ClaimPassiveJackpot/GetPassiveJackpot/DrawPassiveJackpot)
/// — ServerPassiveJackpot has zero references anywhere and appears to be an unused leftover.
/// This service intentionally uses PassiveJackpot/PassiveJackpotContributors only, matching
/// what the live application actually reads and writes.
///
/// SECOND FINDING: PassiveJackpotContributors is itself permanently empty in production (0 rows
/// in both the SQL Server source and the migrated Postgres data). Verified that FeedPassiveJackpot
/// — called from both Gambling.cs's ApplyGamble (1% of every bet) and Shop.cs's owner-only
/// jackpot-seed command, the only two real callers — only ever does an UPDATE/INSERT against
/// PassiveJackpot.Pool; it never writes PassiveJackpotContributors. No other proc in the schema
/// does either. So DrawPassiveJackpot's "pick a random contributor" step always finds zero rows
/// and returns without paying anyone — the scheduled/hourly passive-jackpot draw (Program.cs) is
/// permanently dormant by construction; the ONLY way this pool actually gets paid out today is
/// the instant per-spin 0.5% ClaimPassiveJackpot roll in Gambling.cs. FeedAsync below originally
/// (mistakenly) also inserted a contributor row on every feed, which doesn't match any real
/// source proc — fixed here, before this service had any real caller, to stay pool-only and match
/// FeedPassiveJackpot exactly. DrawAsync/PassiveJackpotContributors are kept as a faithful,
/// currently-inert mirror of the real (equally inert) source mechanism — not deleted.
/// </summary>
public static class JackpotService
{
    /// <summary>Adds to the pool, creating the row if needed. Matches FeedPassiveJackpot exactly — does not touch contributors (see class remarks).</summary>
    public static async Task FeedAsync(DiscordbotContext db, long serverId, decimal amount)
    {
        var pool = await db.PassiveJackpots.FirstOrDefaultAsync(p => p.ServerId == serverId);
        if (pool is not null)
            pool.Pool += amount;
        else
            db.PassiveJackpots.Add(new PassiveJackpot { ServerId = serverId, Pool = amount });

        await db.SaveChangesAsync();
    }

    /// <summary>Returns the current pool for display, or 0 if no row exists yet.</summary>
    public static async Task<decimal> GetPoolAsync(DiscordbotContext db, long serverId)
    {
        var pool = await db.PassiveJackpots.AsNoTracking().FirstOrDefaultAsync(p => p.ServerId == serverId);
        return pool?.Pool ?? 0m;
    }

    /// <summary>Claims the full pool and resets it to 0. Returns the claimed amount (0 if no row exists).</summary>
    public static async Task<decimal> ClaimAsync(DiscordbotContext db, long serverId)
    {
        var pool = await db.PassiveJackpots.FirstOrDefaultAsync(p => p.ServerId == serverId);
        if (pool is null) return 0m;

        decimal claimed = pool.Pool;
        pool.Pool = 0m;
        await db.SaveChangesAsync();
        return claimed;
    }

    /// <summary>
    /// Picks a random eligible contributor and claims the pool for them, then clears the
    /// contributor list (matching source's ORDER BY NEWID() random pick + atomic reset).
    /// Returns null if the pool is empty or has no contributors (source's early-RETURN cases).
    /// </summary>
    public static async Task<(string userId, decimal pool)?> DrawAsync(DiscordbotContext db, long serverId)
    {
        var poolRow = await db.PassiveJackpots.FirstOrDefaultAsync(p => p.ServerId == serverId);
        if (poolRow is null || poolRow.Pool <= 0m) return null;

        var contributorIds = await db.PassiveJackpotContributors.AsNoTracking()
            .Where(c => c.ServerId == serverId).Select(c => c.UserId).ToListAsync();
        if (contributorIds.Count == 0) return null;

        string winnerId = contributorIds[Random.Shared.Next(contributorIds.Count)];
        decimal claimedPool = poolRow.Pool;

        poolRow.Pool = 0m;
        db.PassiveJackpotContributors.RemoveRange(
            db.PassiveJackpotContributors.Where(c => c.ServerId == serverId));
        await db.SaveChangesAsync();

        return (winnerId, claimedPool);
    }
}
