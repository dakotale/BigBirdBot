using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Advice
    {
        private string advice = null;

        public string AdviceText
        {
            get { return advice; }
            set { advice = value; }
        }

        public Advice() { }

        public Advice(string connStr, string adviceDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetAdvicePic", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", adviceDetails));
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

        public Advice(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Advice> GetAdvice(string connStr, string adviceDetails)
        {
            List<Advice> advice = new List<Advice>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetAdvicePic", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", adviceDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                advice.Add(new Advice(dr));
            }

            return advice;
        }

        protected void LoadData(DataRow dr)
        {
            advice = dr.Field<string>("AdvicePic");
        }
    }
}
