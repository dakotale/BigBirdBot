namespace DiscordBot.Data;

/// <summary>
/// A guild's configuration row. Table <c>dbo.Servers</c> — the bot's root per-guild
/// settings table, read/written by almost every feature area.
/// </summary>
public sealed class Server
{
    public int ServerId { get; set; }
    public long ServerUid { get; set; }
    public string ServerName { get; set; } = "";
    public long? DefaultChannelId { get; set; }
    public int Volume { get; set; }
    public bool FixEmbed { get; set; }
    public bool IsPlayerConnected { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool AnnouncementsEnabled { get; set; }
}

/// <summary>
/// A selectable pronoun option offered by <c>/pronoun</c>. Table <c>dbo.Pronouns</c> — a
/// static reference list with no add/edit/delete command; read-only from the app's side.
/// </summary>
public sealed class Pronoun
{
    public int Id { get; set; }
    public string PronounText { get; set; } = "";
}
