using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for the static pronoun-role reference list used by <c>/pronoun</c> and
/// the pronoun-button handler. Replaces <c>GetPronouns</c>. There is no add/edit/delete
/// procedure — the list is read-only from the app's side.
/// </summary>
public sealed class PronounService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Every selectable pronoun option. Replaces <c>GetPronouns</c>.</summary>
    public async Task<IReadOnlyList<(int Id, string Pronoun)>> GetAllAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Pronouns
            .Select(p => new ValueTuple<int, string>(p.Id, p.PronounText))
            .ToListAsync();
    }
}
