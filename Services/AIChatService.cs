using Anthropic;
using Anthropic.Models.Messages;
using System.Text;

namespace DiscordBot.Services;

/// <summary>Anthropic-backed implementation of <see cref="IAIChatService"/>, used by the /ai chat commands.</summary>
public sealed class AIChatService : IAIChatService
{
    private readonly AnthropicClient _client = new() { ApiKey = Constants.Constants.anthropicApiKey };

    /// <summary>Streams a completion from Claude using the given persona as the system prompt, then returns the assembled text.</summary>
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
            Model = Model.ClaudeOpus4_7,
            MaxTokens = 2048,
            System = persona,
            Messages = messages
        };

        var sb = new StringBuilder();
        await foreach (var streamEvent in _client.Messages.CreateStreaming(parameters))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                delta.Delta.TryPickText(out var text))
                sb.Append(text.Text);
        }

        return sb.ToString();
    }
}
