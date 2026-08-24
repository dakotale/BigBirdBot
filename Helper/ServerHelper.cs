using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper
{
    /// <summary>Static helper for looking up a guild's server-configuration row (default channel, active flag, announcement settings).</summary>
    public static class ServerHelper
    {
        /// <summary>Fetches the configured server row for a guild, or null if the guild has no server record yet.</summary>
        public static async Task<ServerInfo?> GetServerInfoAsync(DiscordbotContext db, ulong serverId)
        {
            long serverUid = (long)serverId;
            var server = await db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.ServerUid == serverUid);
            return server is null ? null : new ServerInfo
            {
                ServerUID = (ulong)server.ServerUid,
                ServerName = server.ServerName,
                DefaultChannelID = server.DefaultChannelId?.ToString() ?? "",
                IsActive = server.IsActive,
                AnnouncementsEnabled = server.AnnouncementsEnabled
            };
        }

        /// <summary>Plain-data snapshot of a guild's row in the Servers table.</summary>
        public class ServerInfo
        {
            public ulong ServerUID { get; set; }
            public required string ServerName { get; set; }
            public required string DefaultChannelID { get; set; }
            public bool IsActive { get; set; }
            public bool AnnouncementsEnabled { get; set; }
        }
    }
}
