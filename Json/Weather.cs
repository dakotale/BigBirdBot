using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Weather
    {
        private string city = null;
        private string region = null;
        private string country = null;
        private int? temperatureCelsius = null;
        private int? temperatureFarenheit = null;
        private string weatherIcon = null;
        private string weatherDescription = null;
        private int? windKph = null;
        private decimal? windMph = null;
        private int? feelsLikeCelsius = null;
        private int? feelsLikeFarenheit = null;
        private decimal? precipPercentage = null;
        private int? humidity = null;

        public string City
        {
            get { return city; }
            set { city = value; }
        }

        public string Region
        {
            get { return region; }
            set { region = value; }
        }

        public string Country
        {
            get { return country; }
            set { country = value; }
        }

        public int? TemperatureCelsius
        {
            get { return temperatureCelsius; }
            set { temperatureCelsius = value; }
        }

        public int? TemperatureFarenheit
        {
            get { return temperatureFarenheit; }
            set { temperatureFarenheit = value; }
        }

        public string WeatherIcon
        {
            get { return weatherIcon; }
            set { weatherIcon = value; }
        }

        public string WeatherDescription
        {
            get { return weatherDescription; }
            set { weatherDescription = value; }
        }

        public int? WindKPH
        {
            get { return windKph; }
            set { windKph = value; }
        }

        public decimal? WindMPH
        {
            get { return windMph; }
            set { windMph = value; }
        }

        public int? FeelsLikeCelsius
        {
            get { return feelsLikeCelsius; }
            set { feelsLikeCelsius = value; }
        }

        public int? FeelsLikeFarenheit
        {
            get { return feelsLikeFarenheit; }
            set { feelsLikeFarenheit = value; }
        }

        public decimal? PrecipPercentage
        {
            get { return precipPercentage; }
            set { precipPercentage = value; }
        }

        public int? Humidity
        {
            get { return humidity; }
            set { humidity = value; }
        }

        public Weather() { }

        public Weather(string connStr, string weatherDetails)
        {
            DataTable dt = null;
            try
            {
                // dt = sp output
                using (var con = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("GetWeather", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@json", weatherDetails));
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

        public Weather(DataRow dr)
        {
            LoadData(dr);
        }

        public List<Weather> GetWeather(string connStr, string weatherDetails)
        {
            List<Weather> weathers = new List<Weather>();
            DataTable dt = new DataTable();

            // dt = sp output
            using (var con = new SqlConnection(connStr))
            using (var cmd = new SqlCommand("GetWeather", con))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@json", weatherDetails));
                da.Fill(dt);
            }

            foreach (DataRow dr in dt.Rows)
            {
                weathers.Add(new Weather(dr));
            }

            return weathers;
        }

        protected void LoadData(DataRow dr)
        {
            city = dr.Field<string>("City");
            region = dr.Field<string>("Region");
            country = dr.Field<string>("Country");
            temperatureCelsius = dr.Field<int?>("TemperatureCelsius");
            temperatureFarenheit = dr.Field<int?>("TemperatureFarenheit");
            weatherIcon = dr.Field<string>("WeatherIcon");
            weatherDescription = dr.Field<string>("WeatherDescription");
            windKph = dr.Field<int?>("WindSpeedKPH");
            windMph = dr.Field<decimal?>("WindSpeedMPH");
            feelsLikeCelsius = dr.Field<int>("FeelsLikeCelsius");
            feelsLikeFarenheit = dr.Field<int?>("FeelsLikeFarenheit");
            precipPercentage = dr.Field<decimal?>("PrecipPercentage");
            humidity = dr.Field<int>("Humidity");
        }
    }
}
