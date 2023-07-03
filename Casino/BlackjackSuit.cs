using DiscordBot.Constants;
using System.Data;

namespace DiscordBot.Casino
{
    public class BlackjackSuit
    {
        private int? id = null;
        private string suitName = null;
        private string suitSymbol = null;
        private string suitColor = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string SuitName
        {
            get { return suitName; }
            set { suitName = value; }
        }
        public string SuitSymbol
        {
            get { return suitSymbol; }
            set { suitSymbol = value; }
        }
        public string SuitColor
        {
            get { return suitColor; }
            set { suitColor = value; }
        }
        public BlackjackSuit() { }

        public BlackjackSuit(string connStr)
        {
            DataTable dt = null;
            try
            {
                StoredProcedure stored = new StoredProcedure();
                dt = stored.Select(connStr, "GetAllBlackjackSuits", new List<System.Data.SqlClient.SqlParameter> { });

                if (dt.Rows.Count > 0)
                    LoadData(dt.Rows[0]);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
        }

        public BlackjackSuit(DataRow dr)
        {
            LoadData(dr);
        }

        public List<BlackjackSuit> GetBlackjackSuits(string connStr)
        {
            List<BlackjackSuit> blackjackSuits = new List<BlackjackSuit>();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(connStr, "GetAllBlackjackSuits", new List<System.Data.SqlClient.SqlParameter> { });

            foreach (DataRow dr in dt.Rows)
                blackjackSuits.Add(new BlackjackSuit(dr));

            return blackjackSuits;
        }

        public BlackjackSuit GetRandomSuit(string connStr)
        {
            BlackjackSuit suit = new BlackjackSuit();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(connStr, "GetBlackjackSuit", new List<System.Data.SqlClient.SqlParameter> { });

            foreach (DataRow dr in dt.Rows)
                suit = new BlackjackSuit(dr);

            return suit;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("BlackjackSuitID");
            suitName = dr.Field<string>("SuitName");
            suitSymbol = dr.Field<string>("SuitSymbol");
            suitColor = dr.Field<string>("SuitColor");
        }
    }
}
