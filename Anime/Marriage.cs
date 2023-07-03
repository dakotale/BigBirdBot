using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Anime
{
    public class Marriage
    {
        private int? animeId = null;
        private string characterName = null;
        private string createdBy = null;
        private DateTime? createdOn = null;
        private string imageUrl = null;

        public int? AnimeID
        {
            get { return animeId; }
            set { animeId = value; }
        }

        public string CharacterName
        {
            get { return characterName; }
            set { characterName = value; }
        }

        public string ImageURL
        {
            get { return imageUrl; }
            set { imageUrl = value; }
        }
        public string CreatedBy
        {
            get { return createdBy; }
            set { createdBy = value; }
        }

        public DateTime? CreatedOn
        {
            get { return createdOn; }
            set { createdOn = value; }
        }

        public Marriage() { }

        public Marriage(string connStr)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetAnimeMarriage", con))
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

        public Marriage(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Marriage> GetMarriages(string connStr)
        {
            List<Marriage> marriages = new List<Marriage>();
            DataTable dt = new DataTable();

            // dt = sp output
            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetAnimeMarriage", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                marriages.Add(new Marriage(dr));
            }

            return marriages;
        }

        protected void LoadData(DataRow dr)
        {
            animeId = dr.Field<int?>("AMID");
            characterName = dr.Field<string>("CharacterName");
            imageUrl = dr.Field<string>("MarriageURL");
            createdBy = dr.Field<string>("MarriageUser");
            createdOn = dr.Field<DateTime?>("CreatedOn");
        }
    }
}
