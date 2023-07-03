using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Fox
    {
        private string fox = null;

        public string FoxText
        {
            get { return fox; }
            set { fox = value; }
        }

        public Fox() { }

        public Fox(string connStr, string foxDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetFoxPic", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", foxDetails));
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

        public Fox(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Fox> GetFox(string connStr, string foxDetails)
        {
            List<Fox> fox = new List<Fox>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetFoxPic", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", foxDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                fox.Add(new Fox(dr));
            }

            return fox;
        }

        protected void LoadData(DataRow dr)
        {
            fox = dr.Field<string>("FoxPic");
        }
    }
}
