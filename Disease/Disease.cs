using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Disease
{
    public class Disease
    {
        private int? id = null;
        private string disease = null;
        private int? expendencyDays = null;
        private decimal? expendencyYears = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string DiseaseName
        {
            get { return disease; }
            set { disease = value; }
        }

        public int? ExpendencyDays
        {
            get { return expendencyDays; }
            set { expendencyDays = value; }
        }

        public decimal? ExpendencyYears
        {
            get { return expendencyYears; }
            set { expendencyYears = value; }
        }

        public Disease() { }

        public Disease(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetDisease", con))
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

        public Disease(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Disease> GetDisease(string connStr)
        {
            List<Disease> disease = new List<Disease>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetDisease", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                disease.Add(new Disease(dr));
            }

            return disease;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("ID");
            disease = dr.Field<string>("Disease");
            expendencyDays = dr.Field<int?>("ExpendencyDays");
            expendencyYears = dr.Field<decimal?>("ExpendencyYears");
        }
    }
}
