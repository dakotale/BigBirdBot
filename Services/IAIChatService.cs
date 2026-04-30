namespace DiscordBot.Services;

public interface IAIChatService
{
    Task<string> GetResponseAsync(string persona, IEnumerable<(string Role, string Text)> history, string userMessage);
}
