using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Autocomplete handler for the food parameter on /feed.
/// Filters available food by the pet's current level and the user's partial input.
/// </summary>
public class FoodAutocompleteHandler : AutocompleteHandler
{
    private readonly StoredProcedure _sp = new();

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
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
            var dt = _sp.Select(DiscordBot.Constants.Constants.discordBotConnStr, "GetActivePet",
                [new SqlParameter("@UserID", userId)]);

            if (dt.Rows.Count > 0)
                petLevel = PetHelper.LevelFromXp(int.Parse(dt.Rows[0]["XP"].ToString()!));
        }
        catch { /* fallback to level 1 */ }

        var suggestions = PetHelper.Foods
            .Where(f => f.minLevel <= petLevel &&
                        f.name.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(f => new AutocompleteResult(
                $"{f.emoji} {f.name}  (+{f.hungerRestore} hunger, +{f.happyBonus} happiness)",
                f.name));

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}
