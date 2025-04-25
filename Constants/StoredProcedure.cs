using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Constants
{
    public class StoredProcedure
    {
        public DataTable Select(string connStr, string spName, List<SqlParameter> parameters)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // Check if existing event from this user happens at the same
                // If yes then select existing EventID and insert into AddEventReminder
                conn.Open();

                // 1.  create a command object identifying the stored procedure
                SqlCommand cmd = new SqlCommand(spName, conn);

                // 2. set the command object so it knows to execute a stored procedure
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                foreach (SqlParameter param in parameters)
                {
                    cmd.Parameters.Add(param);
                }

                SqlDataAdapter da = new SqlDataAdapter(cmd);

                da.Fill(dt);

                da.Dispose();
                conn.Close();
                cmd.Dispose();
            }

            return dt;
        }

        public void UpdateCreate(string connStr, string spName, List<SqlParameter> parameters)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            using (SqlCommand cmd = new SqlCommand(spName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                foreach (SqlParameter param in parameters)
                {
                    cmd.Parameters.Add(param);
                }

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
