namespace DiscordBot.Services;

/// <summary>Abstraction over the AI chat backend used by the /ai commands, so the persona/history logic doesn't depend on a specific provider.</summary>
public interface IAIChatService
{
    /// <summary>Generates the assistant's reply for a persona, given prior turn history and the new user message.</summary>
    Task<string> GetResponseAsync(string persona, IEnumerable<(string Role, string Text)> history, string userMessage);
}
