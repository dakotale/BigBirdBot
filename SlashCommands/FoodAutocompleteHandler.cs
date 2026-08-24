using Discord;
using Discord.Interactions;
using DiscordBot.Data;
using DiscordBot.Helper;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Autocomplete handler for the food parameter on /feed.
/// Filters available food by the pet's current level and the user's partial input.
/// </summary>
public class FoodAutocompleteHandler(DiscordbotContext db) : AutocompleteHandler
{
    /// <summary>Returns up to 25 food suggestions unlocked at the user's active pet's level, filtered by their partial input.</summary>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        string userId = context.User.Id.ToString();
        string current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        // Fetch active pet level to gate food suggestions
        int petLevel = 1;
        try
        {
            var pet = await db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
            if (pet is not null)
                petLevel = PetHelper.LevelFromXp(pet.Xp);
        }
        catch { /* fallback to level 1 */ }

        var suggestions = PetHelper.Foods
            .Where(f => f.minLevel <= petLevel &&
                        f.name.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(f => new AutocompleteResult(
                $"{f.emoji} {f.name}  (+{f.hungerRestore} hunger, +{f.happyBonus} happiness)",
                f.name));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
