using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Insult
    {
        private string insult = null;

        public string InsultText
        {
            get { return insult; }
            set { insult = value; }
        }

        public Insult() { }

        public Insult(string connStr, string insultDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetWikipediaURL", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", insultDetails));
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

        public Insult(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Insult> GetInsult(string connStr, string insultDetails)
        {
            List<Insult> insult = new List<Insult>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetInsult", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", insultDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                insult.Add(new Insult(dr));
            }

            return insult;
        }

        protected void LoadData(DataRow dr)
        {
            insult = dr.Field<string>("Insult");
        }
    }
}
