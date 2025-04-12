using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Misc
{
    public class EightBall
    {
        private int? id = null;
        private string saying = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string Saying
        {
            get { return saying; }
            set { saying = value; }
        }

        public EightBall() { }

        public EightBall(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetEightBall", con))
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

        public EightBall(DataRow dr)
        {
            LoadData(dr);
        }

        public List<EightBall> GetEightBall(string connStr)
        {
            List<EightBall> eightBall = new List<EightBall>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetEightBall", con))
            using (var da = new SqlDataAdapter(cmd))
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
            id = dr.Field<int?>("ID");
            saying = dr.Field<string>("Sayings");
        }
    }
}
