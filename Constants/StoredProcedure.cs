using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.Constants
{
    /// <summary>
    /// Core that runs the Stored Procedures for the bot.
    /// If the bot returns something -> Use the Select
    /// If the bot doesn't return something -> Use UpdateCreate
    /// </summary>
    public class StoredProcedure
    {
        public DataTable Select(string connStr, string spName, List<SqlParameter> parameters)
        {
            var dt = new DataTable();

            using SqlConnection conn = new SqlConnection(connStr);
            using SqlCommand cmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (parameters != null)
            {
                foreach (var param in parameters)
                    cmd.Parameters.Add(param);
            }

            using SqlDataAdapter da = new SqlDataAdapter(cmd);
            da.Fill(dt);

            return dt;
        }


        public void UpdateCreate(string connStr, string spName, List<SqlParameter> parameters)
        {
            using SqlConnection conn = new SqlConnection(connStr);
            using SqlCommand cmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (parameters != null)
            {
                foreach (var param in parameters)
                    cmd.Parameters.Add(param);
            }

            conn.Open();
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        }

    }
}
