using DiscordBot.Constants;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Casino
{
    public class Card
    {
        private int? cardId = null;
        private string cardName = null;
        private string cardSymbol = null;
        private int? cardValue = null;
        private int? cardSecondaryValue = null;

        public int? CardID { get { return cardId; } set {  cardId = value; } }
        public string CardName { get {  return cardName; } set { cardName = value; } }
        public string CardSymbol { get {  return cardSymbol; } set { cardSymbol = value; } }
        public int? CardValue { get {  return cardValue; } set { cardValue = value; } }
        public int? CardSecondaryValue { get { return cardSecondaryValue; } set { cardSecondaryValue =value; } }

        public Card() { }

        public Card(string connStr)
        {
            DataTable dt = null;
            try
            {
                StoredProcedure stored = new StoredProcedure();
                dt = stored.Select(connStr, "GetCard", new List<System.Data.SqlClient.SqlParameter> { });

                if (dt.Rows.Count > 0)
                    LoadData(dt.Rows[0]);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
        }

        public Card(DataRow dr)
        {
            LoadData(dr);
        }

        public Card GetCard(string connStr)
        {
            Card card = new Card();
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = stored.Select(connStr, "GetCard", new List<System.Data.SqlClient.SqlParameter>());
            foreach (DataRow dr in dt.Rows)
                card = new Card(dr);

            return card;
        }

        protected void LoadData(DataRow dr)
        {
            cardId = dr.Field<int?>("CardID");
            cardName = dr.Field<string>("CardName");
            cardSymbol = dr.Field<string>("CardSymbol");
            cardValue = dr.Field<int?>("CardValue");
            cardSecondaryValue = dr.Field<int?>("CardSecondaryValue");
        }
    }
}
