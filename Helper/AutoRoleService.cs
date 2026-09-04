using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for the <c>/autorole</c> feature — one configured role per guild, assigned
/// to every new member on join. Replaces <c>GetGuildAutoRole</c>/<c>UpsertGuildAutoRole</c>/
/// <c>DeleteGuildAutoRole</c>. Faithful port; see <see cref="KeywordService"/> for the
/// established pattern this follows.
/// </summary>
public sealed class AutoRoleService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Returns the configured auto-role id for a guild, or <c>null</c> if none is set. Replaces <c>GetGuildAutoRole</c>.</summary>
    public async Task<ulong?> GetRoleIdAsync(ulong guildId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long? roleId = await db.GuildAutoRoles
            .Where(g => g.GuildId == (long)guildId)
            .Select(g => (long?)g.RoleId)
            .FirstOrDefaultAsync();

        return roleId is null ? null : (ulong)roleId.Value;
    }

    /// <summary>Sets (or replaces) a guild's auto-role. Replaces <c>UpsertGuildAutoRole</c>.</summary>
    public async Task SetAsync(ulong guildId, ulong roleId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        long gid = (long)guildId;
        var existing = await db.GuildAutoRoles.FirstOrDefaultAsync(g => g.GuildId == gid);

        if (existing is null)
        {
            db.GuildAutoRoles.Add(new GuildAutoRole
            {
                GuildId = gid,
                RoleId = (long)roleId,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.RoleId = (long)roleId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Removes a guild's auto-role configuration entirely. Replaces <c>DeleteGuildAutoRole</c>.</summary>
    public async Task ClearAsync(ulong guildId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.GuildAutoRoles.Where(g => g.GuildId == (long)guildId).ExecuteDeleteAsync();
    }
}
