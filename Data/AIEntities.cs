namespace DiscordBot.Data;

/// <summary>
/// One turn (user or assistant) of a <c>/chat</c> conversation. <c>ServerUid</c> is stored as
/// a string (unlike most <c>ServerUID</c> columns elsewhere, which are <c>bigint</c>) because
/// <c>/chat</c> is also usable in DMs, where there is no guild id to store.
/// Table <c>dbo.BotAIMessage</c>.
/// </summary>
public sealed class BotAiMessage
{
    public int BotAiMessageId { get; set; }
    public string UserId { get; set; } = "";
    public string ServerUid { get; set; } = "";
    public string ChatRole { get; set; } = "";
    public string ChatMessage { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string? ChannelId { get; set; }
}
