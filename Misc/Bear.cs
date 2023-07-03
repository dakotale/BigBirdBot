using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Misc
{
    public class Bear
    {
        private int? id = null;
        private string bearUrl = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string BearUrl
        {
            get { return bearUrl; }
            set { bearUrl = value; }
        }

        public Bear() { }

        public Bear(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetBear", con))
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

        public Bear(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Bear> GetBear(string connStr)
        {
            List<Bear> bear = new List<Bear>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetBear", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                bear.Add(new Bear(dr));
            }

            return bear;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("ID");
            bearUrl = dr.Field<string>("BearURL");
        }
    }
}
