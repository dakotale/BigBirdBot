using Anthropic;
using Anthropic.Models.Messages;

namespace DiscordBot.Services;

/// <summary>Anthropic-backed implementation of <see cref="IAIChatService"/>, used by the /chat and /support commands.</summary>
public sealed class AIChatService : IAIChatService
{
    private readonly AnthropicClient _client = new() { ApiKey = Constants.Constants.anthropicApiKey };

    /// <summary>
    /// Sends a completion request to Claude using the given persona as the system prompt and
    /// returns the assembled text. Throws <see cref="InvalidOperationException"/> (surfaced to
    /// the user by the command's catch block) when the model declines the request or returns
    /// no usable text — so a bad turn is never persisted as conversation history.
    /// </summary>
    public async Task<string> GetResponseAsync(string persona, IEnumerable<(string Role, string Text)> history, string userMessage)
    {
        var messages = history
            .Select(h => new MessageParam
            {
                Role = h.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? Role.Assistant
                    : Role.User,
                Content = h.Text
            })
            .ToList();

        messages.Add(new MessageParam { Role = Role.User, Content = userMessage });

        var parameters = new MessageCreateParams
        {
            Model = Model.ClaudeOpus5,
            MaxTokens = 4000,               // ~2 Discord messages of prose; the embed limit caps useful length anyway
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = Effort.Low },   // chat workload — keep it responsive
            System = persona,
            Messages = messages
        };

        var response = await _client.Messages.Create(parameters);

        if (response.StopReason == "refusal")
            throw new InvalidOperationException(
                "Claude declined to respond to that message. Try rephrasing, or start a new conversation.");

        string text = string.Concat(response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "No usable reply came back. Try again, or start a new conversation with `new-conversation: Yes`.");

        return text.Trim();
    }
}
