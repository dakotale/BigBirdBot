namespace DiscordBot.Data;

/// <summary>One track ever played (history log), written by <c>AddMusic</c> alongside a matching <see cref="MusicQueueEntry"/>. Table <c>dbo.Music</c>.</summary>
public sealed class MusicHistoryEntry
{
    public int MusicId { get; set; }
    public long ServerUid { get; set; }
    public string VideoId { get; set; } = "";
    public string Author { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}

/// <summary>One track in a guild's persisted playback queue, used to restore playback after a bot restart. Table <c>dbo.MusicQueue</c>.</summary>
public sealed class MusicQueueEntry
{
    public int MusicQueueId { get; set; }
    public int MusicId { get; set; }
    public long ServerUid { get; set; }
    public long VoiceChannelId { get; set; }
    public long TextChannelId { get; set; }
    public string Url { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}

/// <summary>Records that the bot's music player is currently connected to a voice/text channel pair in a guild. Table <c>dbo.PlayerConnected</c>.</summary>
public sealed class PlayerConnected
{
    public int PlayerId { get; set; }
    public long ServerUid { get; set; }
    public long VoiceChannelId { get; set; }
    public long TextChannelId { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}
