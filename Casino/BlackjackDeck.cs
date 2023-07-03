using DiscordBot.Constants;
using System.Data;

namespace DiscordBot.Casino
{
    public class BlackjackDeck
    {
        private int? id = null;
        private string cardName = null;
        private int? cardValue = null;
        private int? secondaryValue = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string CardName
        {
            get { return cardName; }
            set { cardName = value; }
        }
        public int? CardValue
        {
            get { return cardValue; }
            set { cardValue = value; }
        }
        public int? SecondaryValue
        {
            get { return secondaryValue; }
            set { secondaryValue = value; }
        }
        public BlackjackDeck() { }

        public BlackjackDeck(string connStr)
        {
            DataTable dt = null;
            try
            {
                StoredProcedure stored = new StoredProcedure();
                dt = stored.Select(connStr, "GetAllBlackjackDecks", new List<System.Data.SqlClient.SqlParameter> { });

                if (dt.Rows.Count > 0)
                    LoadData(dt.Rows[0]);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
            
        }

        public BlackjackDeck(DataRow dr)
        {
            LoadData(dr);
        }

        public List<BlackjackDeck> GetBlackjackDecks(string connStr)
        {
            List<BlackjackDeck> blackjackDecks = new List<BlackjackDeck>();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(connStr, "GetAllBlackjackDecks", new List<System.Data.SqlClient.SqlParameter> { });

            foreach (DataRow dr in dt.Rows)
                blackjackDecks.Add(new BlackjackDeck(dr));

            return blackjackDecks;
        }

        public BlackjackDeck GetRandomDeck(string connStr)
        {
            BlackjackDeck deck = new BlackjackDeck();
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(connStr, "GetBlackjackDeck", new List<System.Data.SqlClient.SqlParameter> { });

            foreach (DataRow dr in dt.Rows)
                deck = new BlackjackDeck(dr);

            return deck;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("BlackjackDeckID");
            cardName = dr.Field<string>("CardName");
            cardValue = dr.Field<int?>("CardValue");
            secondaryValue = dr.Field<int?>("CardSecondaryValue");
        }
    }
}
