using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Anime
{
    public class MarriageCharacter
    {
        private int? animeId = null;
        private string characterName = null;
        private string characterUrl = null;
        private int? currencyVal = null;

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

        public string CharacterURL
        {
            get { return characterUrl; }
            set { characterUrl = value; }
        }

        public int? CurrencyValue
        {
            get { return currencyVal; }
            set { currencyVal = value; }
        }

        public MarriageCharacter() { }

        public MarriageCharacter(string connStr)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetMarriageCharacters", con))
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

        public MarriageCharacter(DataRow dr)
        {
            LoadData(dr);
        }

        public List<MarriageCharacter> GetMarriageCharacters(string connStr)
        {
            List<MarriageCharacter> marriages = new List<MarriageCharacter>();
            DataTable dt = new DataTable();

            // dt = sp output
            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetMarriageCharacters", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                marriages.Add(new MarriageCharacter(dr));
            }

            return marriages;
        }

        public List<MarriageCharacter> GetOneMarriageCharacters(string connStr)
        {
            List<MarriageCharacter> marriages = new List<MarriageCharacter>();
            DataTable dt = new DataTable();

            // dt = sp output
            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetMarriageCharactersByID", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                marriages.Add(new MarriageCharacter(dr));
            }

            return marriages;
        }

        protected void LoadData(DataRow dr)
        {
            animeId = dr.Field<int?>("AnimeID");
            characterName = dr.Field<string>("CharacterName");
            characterUrl = dr.Field<string>("CharacterImageURL");
            currencyVal = dr.Field<int?>("CurrencyValue");
        }
    }
}
