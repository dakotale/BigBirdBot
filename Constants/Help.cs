using System.Data.SqlClient;
using System.Data;

namespace DiscordBot.Constants
{
    public class Help
    {
        private string commandName = "";

        public string CommandName
        {
            get { return commandName; }
            set { commandName = value; }
        }

        public Help() { }

        public Help(string connStr)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetCommandList", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    da.Fill(dt);
                }
                if (dt.Rows.Count > 0)
                {
                    LoadData(dt.Rows[0]);
                }
            }
            finally
            {
                if (dt != null)
                {
                    dt.Dispose();
                }
            }
        }

        public Help(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Help> GetHelp(string connStr)
        {
            List<Help> help = new List<Help>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetCommandList", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                help.Add(new Help(dr));
            }

            return help;
        }

        protected void LoadData(DataRow dr)
        {
            commandName = dr.Field<string>("CommandName");
        }
    }
}
