using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Constants
{
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
        }

    }
}
