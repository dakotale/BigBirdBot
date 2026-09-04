using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for a member's per-server row in <c>Users</c>. Replaces <c>AddUser</c>,
/// <c>DeleteUser</c>, and <c>UpdateUserLastSeen</c>.
/// </summary>
public sealed class UserService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>
    /// Registers a member for a server if they aren't already known there. Insert-only: if the
    /// row already exists, nothing is updated — a returning member's stored Username/Nickname
    /// stay whatever they were on first join, matching the original procedure exactly. Replaces
    /// <c>AddUser</c>.
    /// </summary>
    public async Task AddUserIfMissingAsync(string userId, string username, DateTime joinDate, ulong serverUid, string? nickname)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverUid;
        if (await db.Users.AnyAsync(u => u.UserId == userId && u.ServerUid == sid)) return;

        db.Users.Add(new User
        {
            UserId = userId,
            Username = username,
            JoinDate = joinDate,
            ServerUid = sid,
            Nickname = nickname,
            PronounId = null,
            CreatedOn = DateTime.Now,
            DeletedOn = null
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Removes a member's row for one server. If that was their only row across every server,
    /// also purges their scheduled-keyword and AI-chat-history rows (matches the original's
    /// pre-delete count check, evaluated before anything is removed). Replaces <c>DeleteUser</c>.
    /// </summary>
    public async Task DeleteUserAsync(string userId, ulong serverUid)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long sid = (long)serverUid;
        int totalCount = await db.Users.CountAsync(u => u.UserId == userId);

        await db.Users.Where(u => u.UserId == userId && u.ServerUid == sid).ExecuteDeleteAsync();

        if (totalCount == 1)
        {
            await db.UsersScheduledKeywords.Where(k => k.UserId == userId).ExecuteDeleteAsync();
            await db.BotAiMessages.Where(m => m.UserId == userId).ExecuteDeleteAsync();
        }
    }

    /// <summary>Stamps a member's last-activity time. Replaces <c>UpdateUserLastSeen</c>.</summary>
    public async Task UpdateLastSeenAsync(string userId, ulong serverUid)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.Users
            .Where(u => u.UserId == userId && u.ServerUid == (long)serverUid)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeen, DateTime.UtcNow));
    }
}
