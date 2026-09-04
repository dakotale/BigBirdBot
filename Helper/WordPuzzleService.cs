using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for the hourly bonus word puzzle. Replaces <c>GetRandomWord</c>,
/// <c>AddPetWordPuzzle</c>, <c>ClaimPetPuzzle</c>, <c>GetPuzzleClaimedStatus</c>, and the two
/// procedures <c>GetActivePetPuzzle</c>/<c>GetPetWordPuzzle</c> — which had identical bodies
/// (same filter, same columns) despite the different names, so both call sites share one
/// <see cref="GetActivePuzzleAsync"/> method here rather than being ported as two copies of
/// the same query.
/// </summary>
public sealed class WordPuzzleService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Picks one random word to use as the next puzzle. Replaces <c>GetRandomWord</c> (<c>ORDER BY NEWID()</c>).</summary>
    public async Task<string?> GetRandomWordAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Words
            .OrderBy(w => Guid.NewGuid()) // translates to ORDER BY NEWID() on SQL Server
            .Select(w => w.Text)
            .FirstOrDefaultAsync();
    }

    /// <summary>Posts a new puzzle for a channel. Replaces <c>AddPetWordPuzzle</c>.</summary>
    public async Task AddPuzzleAsync(string channelId, string word, DateTime expiresAt)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        db.PetWordPuzzles.Add(new PetWordPuzzle
        {
            ChannelId = channelId,
            Word = word,
            ExpiresAt = expiresAt,
            Claimed = false
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The unclaimed, unexpired puzzle currently active in a channel, if any. Replaces both
    /// <c>GetActivePetPuzzle</c> and <c>GetPetWordPuzzle</c> (identical procedures).
    /// </summary>
    public async Task<ActivePuzzle?> GetActivePuzzleAsync(string channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;

        return await db.PetWordPuzzles
            .Where(p => p.ChannelId == channelId && !p.Claimed && p.ExpiresAt > now)
            .Select(p => new ActivePuzzle(p.PuzzleId, p.Word, p.ExpiresAt))
            .FirstOrDefaultAsync();
    }

    /// <summary>Marks a puzzle solved. Replaces <c>ClaimPetPuzzle</c>.</summary>
    public async Task ClaimPuzzleAsync(int puzzleId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.PetWordPuzzles
            .Where(p => p.PuzzleId == puzzleId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Claimed, true));
    }

    /// <summary>
    /// Whether the most recently posted puzzle in a channel (regardless of expiry) was
    /// claimed. Replaces <c>GetPuzzleClaimedStatus</c>.
    /// </summary>
    public async Task<bool?> GetClaimedStatusAsync(string channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.PetWordPuzzles
            .Where(p => p.ChannelId == channelId)
            .OrderByDescending(p => p.PuzzleId)
            .Select(p => (bool?)p.Claimed)
            .FirstOrDefaultAsync();
    }
}
