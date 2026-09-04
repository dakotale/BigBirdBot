namespace DiscordBot.Data;

/// <summary>
/// A guild's configured auto-assign-on-join role. Table <c>dbo.GuildAutoRole</c> —
/// one row per guild (<see cref="GuildId"/> is the primary key), upserted by <c>/autorole set</c>.
/// </summary>
public sealed class GuildAutoRole
{
    public long GuildId { get; set; }
    public long RoleId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
