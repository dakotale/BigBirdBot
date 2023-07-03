using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Helper
{
    public class MusicLog
    {
        private int? code = null;
        private string? reason = null;
        private string? serverName = null;
        private DateTime? createdOn = null;

        public int? Code
        {
            get { return code; }
            set { code = value; }
        }

        public string Reason
        {
            get { return reason; }
            set { reason = value; }
        }

        public string ServerName
        {
            get { return serverName; }
            set { serverName = value; }
        }

        public DateTime? CreatedOn
        {
            get { return createdOn; }
            set { createdOn = value; }
        }

        public MusicLog() { }

        public MusicLog(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetMusicLog", con))
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

        public MusicLog(DataRow dr)
        {
            LoadData(dr);
        }

        public List<MusicLog> GetMusicLog(string connStr)
        {
            List<MusicLog> musicLog = new List<MusicLog>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetMusicLog", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                musicLog.Add(new MusicLog(dr));
            }

            return musicLog;
        }

        protected void LoadData(DataRow dr)
        {
            code = dr.Field<int?>("Code");
            reason = dr.Field<string?>("Reason");
            serverName = dr.Field<string?>("ServerName");
            createdOn = dr.Field<DateTime?>("CreatedOn");
        }
    }
}
