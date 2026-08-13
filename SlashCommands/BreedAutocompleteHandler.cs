using Discord;
using Discord.Interactions;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Autocomplete handler for the breed parameter on /adopt.
/// Filters the breed list for the species the user has already typed,
/// matching against the current partial input.
/// </summary>
public class BreedAutocompleteHandler : AutocompleteHandler
{
    /// <summary>Returns up to 25 breed suggestions for the already-selected species, filtered by the user's partial input.</summary>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        // Read the species option the user has already filled in
        string species = autocompleteInteraction.Data.Options
            .FirstOrDefault(o => o.Name == "species")?.Value?.ToString()?.ToLower() ?? "";

        // Read what the user has typed so far for breed
        string current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        if (!PetHelper.Breeds.TryGetValue(species, out var breeds))
        {
            // Species not selected yet — show nothing
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }

        var suggestions = breeds
            .Where(b => b.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)   // Discord hard limit
            .Select(b => new AutocompleteResult(b, b));

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}
