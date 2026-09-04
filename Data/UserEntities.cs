namespace DiscordBot.Data;

/// <summary>
/// A member's row for one server they belong to (composite key: a user in N servers has N
/// rows). Table <c>dbo.Users</c>. Originally a minimal read-only projection added for the
/// keyword feature area's <c>/owner schedulelist</c>; expanded to every column when the
/// user/server/audit/etc. areas moved to EF Core.
/// </summary>
public sealed class User
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime JoinDate { get; set; }
    public long ServerUid { get; set; }
    public string? Nickname { get; set; }

    /// <summary>Never set anywhere in the app — <c>AddUser</c> always inserts it as <c>NULL</c>, and nothing reads or updates it afterward. Kept for schema fidelity.</summary>
    public int? PronounId { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime? DeletedOn { get; set; }
    public DateTime? LastSeen { get; set; }
}
