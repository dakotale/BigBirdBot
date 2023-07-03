using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Cat
    {
        private string cat = null;

        public string CatText
        {
            get { return cat; }
            set { cat = value; }
        }

        public Cat() { }

        public Cat(string connStr, string catDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetCatPic", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", catDetails));
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

        public Cat(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Cat> GetCat(string connStr, string catDetails)
        {
            List<Cat> cat = new List<Cat>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetCatPic", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", catDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                cat.Add(new Cat(dr));
            }

            return cat;
        }

        protected void LoadData(DataRow dr)
        {
            cat = dr.Field<string>("CatURL");
        }
    }
}
