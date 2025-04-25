using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Misc
{
    public class EightBall
    {
        public int? ID { get; set; } = null;

        public string Saying { get; set; } = null;

        public EightBall() { }

        public EightBall(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (SqlConnection con = new SqlConnection(connStr))
                using (SqlCommand cmd = new SqlCommand("GetEightBall", con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
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

        public EightBall(DataRow dr)
        {
            LoadData(dr);
        }

        public List<EightBall> GetEightBall(string connStr)
        {
            List<EightBall> eightBall = new List<EightBall>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (SqlConnection con = new SqlConnection(connStr))
            using (SqlCommand cmd = new SqlCommand("GetEightBall", con))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                eightBall.Add(new EightBall(dr));
            }

            return eightBall;
        }

        protected void LoadData(DataRow dr)
        {
            ID = dr.Field<int?>("ID");
            Saying = dr.Field<string>("Sayings");
        }
    }
}
