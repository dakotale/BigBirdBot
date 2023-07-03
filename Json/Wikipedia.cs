using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Wikipedia
    {
        private string wikiurl = null;

        public string WikiURL
        {
            get { return wikiurl; }
            set { wikiurl = value; }
        }

        public Wikipedia() { }

        public Wikipedia(string connStr, string wikiDetails)
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
                    cmd.Parameters.Add(new SqlParameter("@json", wikiDetails));
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

        public Wikipedia(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Wikipedia> GetWikiURL(string connStr, string wikiDetails)
        {
            List<Wikipedia> wikipedia = new List<Wikipedia>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetWikipediaURL", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", wikiDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                wikipedia.Add(new Wikipedia(dr));
            }

            return wikipedia;
        }

        protected void LoadData(DataRow dr)
        {
            wikiurl = dr.Field<string>("WikiURL");
        }
    }
}
