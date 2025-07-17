using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Constants
{
    public class Audit
    {
        public Audit() { }

        public void InsertAudit(string command, string createdBy, string connStr, string serverId)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // 1.  create a command object identifying the stored procedure
                SqlCommand cmd = new SqlCommand("AddAudit", conn);

                // 2. set the command object so it knows to execute a stored procedure
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                cmd.Parameters.Add(new SqlParameter("@Command", command));
                cmd.Parameters.Add(new SqlParameter("@CreatedBy", createdBy));
                cmd.Parameters.Add(new SqlParameter("@ServerID", Int64.Parse(serverId)));

                // execute the command
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertAuditChannel(string connStr, string serverId, string serverName, string createdBy)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // 1.  create a command object identifying the stored procedure
                SqlCommand cmd = new SqlCommand("AddAuditChannel", conn);

                // 2. set the command object so it knows to execute a stored procedure
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                cmd.Parameters.Add(new SqlParameter("@CreatedBy", createdBy));
                cmd.Parameters.Add(new SqlParameter("@ServerID", Int64.Parse(serverId)));
                cmd.Parameters.Add(new SqlParameter("@ServerName", serverName));

                // execute the command
                cmd.ExecuteNonQuery();
            }
        }
    }
}
