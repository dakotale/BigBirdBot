using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Dog
    {
        private string dog = null;

        public string DogText
        {
            get { return dog; }
            set { dog = value; }
        }

        public Dog() { }

        public Dog(string connStr, string dogDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetDogPic", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", dogDetails));
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

        public Dog(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Dog> GetDog(string connStr, string dogDetails)
        {
            List<Dog> dog = new List<Dog>();
            DataTable dt = new DataTable();

            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetDogPic", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", dogDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                dog.Add(new Dog(dr));
            }

            return dog;
        }

        protected void LoadData(DataRow dr)
        {
            dog = dr.Field<string>("DogPic");
        }
    }
}
