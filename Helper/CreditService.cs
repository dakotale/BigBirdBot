using DiscordBot.Data;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// Shared EF Core credit-account operations, extracted from the old <c>Economy</c> stored-proc
/// wrapper so both slash commands and singleton code (BotHost, other feature modules) can call
/// them with whatever <see cref="DiscordbotContext"/> they already have — mirrors how <c>Economy</c>
/// used to be constructed bare (<c>new Economy()</c>) purely to reach these methods, without a
/// real DI-injected DbContext of its own.
/// </summary>
public static class CreditService
{
    /// <summary>Ensures a credit account row exists for the user in the given server (idempotent).</summary>
    public static async Task EnsureAccountAsync(DiscordbotContext db, string userId, string serverId)
    {
        bool exists = await db.Credits.AnyAsync(c => c.UserId == userId && c.ServerId == serverId);
        if (!exists)
        {
            db.Credits.Add(new Credit { UserId = userId, ServerId = serverId });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Adds credits, creating the account first if needed. Returns the new balance.
    /// Source did this as two independent proc calls (AddCredits, then AddLifetimeEarned wrapped
    /// in a try/catch that silently swallowed failures) — bundled into one atomic update here,
    /// an atomicity improvement over source, not a silent behavior change: LifetimeEarned can no
    /// longer drift out of sync with Balance/TotalEarned if a mid-flight failure occurs.
    /// </summary>
    public static async Task<decimal> AddCreditsAsync(DiscordbotContext db, string userId, string serverId, decimal amount, string source)
    {
        await EnsureAccountAsync(db, userId, serverId);
        var credit = await db.Credits.FirstAsync(c => c.UserId == userId && c.ServerId == serverId);

        credit.Balance += amount;
        credit.TotalEarned += amount;
        credit.LifetimeEarned += amount;
        if (source == "daily") credit.LastDaily = DateTime.UtcNow;
        if (source == "work") credit.LastWork = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return credit.Balance;
    }

    /// <summary>
    /// Deducts credits. Returns the new balance, or -1 if the account doesn't exist or has
    /// insufficient funds (matches the source DeductCredits proc's sentinel exactly — it does
    /// NOT auto-create the account first, unlike AddCredits).
    /// </summary>
    public static async Task<decimal> DeductCreditsAsync(DiscordbotContext db, string userId, string serverId, decimal amount, string source)
    {
        var credit = await db.Credits.FirstOrDefaultAsync(c => c.UserId == userId && c.ServerId == serverId);
        if (credit is null || credit.Balance < amount) return -1m;

        credit.Balance -= amount;
        credit.TotalSpent += amount;

        await db.SaveChangesAsync();
        return credit.Balance;
    }

    /// <summary>Gets the current balance, or 0 if the account doesn't exist.</summary>
    public static async Task<decimal> GetBalanceAsync(DiscordbotContext db, string userId, string serverId)
    {
        var credit = await db.Credits.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId && c.ServerId == serverId);
        return credit?.Balance ?? 0m;
    }
}
