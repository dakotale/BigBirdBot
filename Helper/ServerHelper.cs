using Discord;
using DiscordBot.Constants;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.Helper
{
    public static class ServerHelper
    {
        public static ServerInfo GetServerInfo(ulong serverId)
        {
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServerByID", [new SqlParameter("@ServerUID", (long)serverId)]);
            var serverInfo = ServerInfo.PopulateByDataTable(dt);

            if (serverInfo != null) return serverInfo;
            else return null;
        }

        public class ServerInfo
        {
            public ulong ServerUID { get; set; }
            public required string ServerName { get; set; }
            public required string DefaultChannelID { get; set; }
            public bool IsActive { get; set; }

            public static ServerInfo PopulateByDataTable(DataTable dt)
            {
                if (dt.Rows.Count == 0)
                {
                    return null;
                }
                DataRow row = dt.Rows[0];
                return new ServerInfo
                {
                    ServerUID = Convert.ToUInt64(row["ServerUID"]),
                    ServerName = row["ServerName"].ToString(),
                    DefaultChannelID = row["DefaultChannelID"].ToString(),
                    IsActive = Convert.ToBoolean(row["IsActive"])
                };
            }
        }
    }
}
