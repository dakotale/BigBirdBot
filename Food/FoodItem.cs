using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Food
{
    public class FoodItem
    {
        private int? id = null;
        private string foodItem = null;

        public int? ID
        {
            get { return id; }
            set { id = value; }
        }

        public string FoodName
        {
            get { return foodItem; }
            set { foodItem = value; }
        }

        public FoodItem() { }

        public FoodItem(string connStr)
        {
            DataTable dt = null;
            Random rnd = new Random();
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetFoodItem", con))
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

        public FoodItem(DataRow dr)
        {
            LoadData(dr);
        }

        public List<FoodItem> GetFood(string connStr)
        {
            List<FoodItem> food = new List<FoodItem>();
            DataTable dt = new DataTable();
            Random rnd = new Random();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetFoodItem", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                food.Add(new FoodItem(dr));
            }

            return food;
        }

        protected void LoadData(DataRow dr)
        {
            id = dr.Field<int?>("ID");
            foodItem = dr.Field<string>("FoodItem");
        }
    }
}
