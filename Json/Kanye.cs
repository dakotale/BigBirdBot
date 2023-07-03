using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Kanye
    {
        private string kanye = null;

        public string KanyeText
        {
            get { return kanye; }
            set { kanye = value; }
        }

        public Kanye() { }

        public Kanye(string connStr, string kanyeDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetKanye", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", kanyeDetails));
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

        public Kanye(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Kanye> GetKanye(string connStr, string kanyeDetails)
        {
            List<Kanye> kanye = new List<Kanye>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetKanye", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", kanyeDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                kanye.Add(new Kanye(dr));
            }

            return kanye;
        }

        protected void LoadData(DataRow dr)
        {
            kanye = dr.Field<string>("Kanye");
        }
    }
}
