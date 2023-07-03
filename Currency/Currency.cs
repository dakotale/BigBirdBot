using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Currency
{
    public class Currency
    {
        private long? userId = null;
        private string username = null;
        private int? currencyValue = null;
        private DateTime? createdOn = null;
        private DateTime? modifiedOn = null;

        public long? UserID
        {
            get { return userId; }
            set { userId = value; }
        }

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        public int? CurrencyValue
        {
            get { return currencyValue; }
            set { currencyValue = value; }
        }

        public DateTime? CreatedOn
        {
            get { return createdOn; }
            set { createdOn = value; }
        }

        public DateTime? ModifiedOn
        {
            get { return modifiedOn; }
            set { modifiedOn = value; }
        }

        public Currency() { }

        public Currency(string connStr, int? userId)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetCurrencyUser", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@UserID", userId));
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

        public Currency(DataRow dr)
        {
            LoadData(dr);
        }

        protected void LoadData(DataRow dr)
        {
            userId = dr.Field<long?>("UserID");
            username = dr.Field<string>("Username");
            currencyValue = dr.Field<int?>("CurrencyValue");
            createdOn = dr.Field<DateTime?>("CreatedOn");
            modifiedOn = dr.Field<DateTime?>("ModifiedOn");
        }

        public List<Currency> GetCurrencyUser(string connStr, long? userId)
        {
            List<Currency> currencies = new List<Currency>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetCurrencyUser", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@UserID", userId));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                currencies.Add(new Currency(dr));
            }

            return currencies;
        }

        public void AddCurrencyUser(string connStr, long? userId, string username)
        {
            using (SqlConnection conn = new SqlConnection(Constants.Constants.discordBotConnStr))
            {
                conn.Open();

                // 1.  create a command object identifying the stored procedure
                SqlCommand cmd = new SqlCommand("AddCurrencyUser", conn);

                // 2. set the command object so it knows to execute a stored procedure
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                cmd.Parameters.Add(new SqlParameter("@UserID", userId));
                cmd.Parameters.Add(new SqlParameter("@Username", username));
                // execute the command
                cmd.ExecuteNonQuery();

                conn.Close();
                cmd.Dispose();
            }
        }
        public void UpdateCurrencyUser(string connStr, long? userId, int? currencyValue)
        {
            using (SqlConnection conn = new SqlConnection(Constants.Constants.discordBotConnStr))
            {
                conn.Open();

                // 1.  create a command object identifying the stored procedure
                SqlCommand cmd = new SqlCommand("UpdateCurrencyUser", conn);

                // 2. set the command object so it knows to execute a stored procedure
                cmd.CommandType = CommandType.StoredProcedure;

                // 3. add parameter to command, which will be passed to the stored procedure
                cmd.Parameters.Add(new SqlParameter("@UserID", userId));
                cmd.Parameters.Add(new SqlParameter("@CurrencyValue", currencyValue));
                // execute the command
                cmd.ExecuteNonQuery();

                conn.Close();
                cmd.Dispose();
            }
        }
    }
}
