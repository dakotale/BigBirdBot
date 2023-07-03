using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Misc
{
    public class Bird
    {
        private int? id = null;
        private string birdUrl = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string BirdUrl
        {
            get { return birdUrl; }
            set { birdUrl = value; }
        }

        public Bird() { }

        public Bird(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetBird", con))
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

        public Bird(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Bird> GetBird(string connStr)
        {
            List<Bird> Bird = new List<Bird>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetBird", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                Bird.Add(new Bird(dr));
            }

            return Bird;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("ID");
            birdUrl = dr.Field<string>("BirdUrl");
        }
    }
}
