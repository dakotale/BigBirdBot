using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.Constants
{
    /// <summary>
    /// Bot auditing of commands and Discord events.
    /// </summary>
    public class Audit
    {
        public void InsertAudit(string command, string createdBy, string connStr, string serverId)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAudit", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@Command",   command));
            cmd.Parameters.Add(new SqlParameter("@CreatedBy", createdBy));
            cmd.Parameters.Add(new SqlParameter("@ServerID",  long.Parse(serverId)));
            cmd.ExecuteNonQuery();
        }

        public void InsertUserJoinedAudit(string userId, string guildId, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditUserJoined", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@UserUID",   long.Parse(userId)));
            cmd.Parameters.Add(new SqlParameter("@ServerUID", long.Parse(guildId)));
            cmd.ExecuteNonQuery();
        }

        public void InsertUserLeftAudit(string userId, string guildId, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditUserLeft", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@UserUID",   long.Parse(userId)));
            cmd.Parameters.Add(new SqlParameter("@ServerUID", long.Parse(guildId)));
            cmd.ExecuteNonQuery();
        }

        public void InsertButtonAudit(string buttonId, string userId, string guildId, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditButtonExecuted", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@ButtonID",  buttonId));
            cmd.Parameters.Add(new SqlParameter("@UserUID",   long.Parse(userId)));
            cmd.Parameters.Add(new SqlParameter("@ServerUID", long.Parse(guildId)));
            cmd.ExecuteNonQuery();
        }

        public void InsertGuildJoinedAudit(string guildId, string guildName, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditGuildJoined", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@ServerUID",  long.Parse(guildId)));
            cmd.Parameters.Add(new SqlParameter("@ServerName", guildName));
            cmd.ExecuteNonQuery();
        }

        public void InsertReactionAudit(string emoji, string messageId, string userId, string channelId, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditReactionAdded", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@Emoji",      emoji));
            cmd.Parameters.Add(new SqlParameter("@MessageUID", long.Parse(messageId)));
            cmd.Parameters.Add(new SqlParameter("@UserUID",    long.Parse(userId)));
            cmd.Parameters.Add(new SqlParameter("@ChannelUID", long.Parse(channelId)));
            cmd.ExecuteNonQuery();
        }

        public void InsertGameTriggerAudit(string game, string userId, string guildId, string connStr)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("AddAuditGameTrigger", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@Game",      game));
            cmd.Parameters.Add(new SqlParameter("@UserUID",   long.Parse(userId)));
            cmd.Parameters.Add(new SqlParameter("@ServerUID", long.Parse(guildId)));
            cmd.ExecuteNonQuery();
        }
    }
}
