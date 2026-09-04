using System.Data;
using Microsoft.Data.SqlClient;
using DiscordBot.Constants;

namespace DiscordBot.Helper
{
    /// <summary>Static helper for looking up a guild's server-configuration row (default channel, active flag, announcement settings).</summary>
    public static class ServerHelper
    {
        /// <summary>Fetches the configured server row for a guild, or null if the guild has no server record yet.</summary>
        public static ServerInfo? GetServerInfo(ulong serverId)
        {
            var stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServerByID",
                [new SqlParameter("@ServerUID", (long)serverId)]);

            return ServerInfo.PopulateByDataTable(dt);
        }

        /// <summary>Plain-data snapshot of a guild's row in the Servers table.</summary>
        public class ServerInfo
        {
            public ulong ServerUID { get; set; }
            public string ServerName { get; set; } = "";
            public string DefaultChannelID { get; set; } = "";
            public bool IsActive { get; set; }
            public bool AnnouncementsEnabled { get; set; }

            /// <summary>Builds a ServerInfo from the first row of a GetServerByID result, or null if the query returned no rows.</summary>
            public static ServerInfo? PopulateByDataTable(DataTable dt)
            {
                if (dt.Rows.Count == 0)
                    return null;

                DataRow row = dt.Rows[0];
                return new ServerInfo
                {
                    ServerUID = Convert.ToUInt64(row["ServerUID"]),
                    ServerName = row["ServerName"].ToString() ?? "",
                    DefaultChannelID = row["DefaultChannelID"].ToString() ?? "",
                    IsActive = Convert.ToBoolean(row["IsActive"]),
                    AnnouncementsEnabled = row.Table.Columns.Contains("AnnouncementsEnabled")
                                          && Convert.ToBoolean(row["AnnouncementsEnabled"])
                };
            }
        }
    }
}
