using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Misc
{
    public class DerpFlop
    {
        public int? id = null;
        public string derpUrl = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string DerpURL
        {
            get { return derpUrl; }
            set { derpUrl = value; }
        }

        public DerpFlop() { }

        public DerpFlop(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetDerp", con))
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

        public List<DerpFlop> GetDerp(string connStr)
        {
            List<DerpFlop> Derp = new List<DerpFlop>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetDerp", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                Derp.Add(new DerpFlop(dr));
            }

            return Derp;
        }

        public DerpFlop(DataRow dr)
        {
            LoadData(dr);
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("ID");
            derpUrl = dr.Field<string>("DerpURL");
        }
    }
}
