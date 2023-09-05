using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Json;
using System.Data.SqlClient;
using System.Data;
using System.Net;
using DiscordBot.Helper;

namespace DiscordBot.Modules
{
    public class JsonCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();

        [Command("wikip")]
        [Discord.Commands.Summary("Get a random Wikipedia article.")]
        public async Task HandleWikipediaCommand()
        {
            try
            {
                audit.InsertAudit("wikip", Context.User.Username, Constants.Constants.discordBotConnStr);
                // https://en.wikipedia.org/wiki/Special:Random
                // https://en.wikipedia.org/api/rest_v1/page/random/summary
                string apiUrl = "https://en.wikipedia.org/api/rest_v1/page/random/summary";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.AutomaticDecompression = DecompressionMethods.GZip;
                string results = string.Empty;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    results = reader.ReadToEnd();
                    string connStr = Constants.Constants.discordBotConnStr;
                    Wikipedia wikipedia = new Wikipedia();
                    List<Wikipedia> wikipedias = wikipedia.GetWikiURL(connStr, results);
                    foreach (var o in wikipedias)
                    {
                        string title = "BigBirdBot - Wikipedia";
                        string desc = $"{o.WikiURL}";
                        string thumbnailUrl = "";
                        string createdBy = "Command from: " + Context.User.Username;

                        EmbedHelper embed = new EmbedHelper();
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Magenta).Build());
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("weather")]
        [Discord.Commands.Summary("Look up the weather for a location in 'City, State/Province, Country' format.")]
        public async Task HandleWeatherCommand([Remainder] string weatherLocation)
        {
            audit.InsertAudit("weather", Context.User.Username, Constants.Constants.discordBotConnStr);

            try
            {
                string[] weatherBreakdown = weatherLocation.Split(',');
                if (weatherBreakdown.Length >= 3) 
                {
                    string city = weatherBreakdown[0].Trim();
                    string state = weatherBreakdown[1].Trim();
                    string country = weatherBreakdown[2].Trim();
                    string longitude, latitude = "";

                    // Call GeoCode API to pull Lat/Long
                    string apiUrl = Constants.Constants.weatherGeoCodeApiUrl + city + "," + state + "," + country + "&limit=1&appid=" + Constants.Constants.weatherApiUrlKey;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                    request.AutomaticDecompression = DecompressionMethods.GZip;
                    string results = string.Empty;

                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    Stream stream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(stream);
                    results = reader.ReadToEnd();

                    string connStr = Constants.Constants.discordBotConnStr;
                    StoredProcedure select = new StoredProcedure();
                    DataTable dtLonLat = select.Select(connStr, "GetLatLonFromJson", new List<SqlParameter> { new SqlParameter("@json", results) });
                    if (dtLonLat.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dtLonLat.Rows)
                        {
                            longitude = dr["Lon"].ToString();
                            latitude = dr["Lat"].ToString();
                            apiUrl = Constants.Constants.weatherApiUrl + "lat=" + latitude + "&lon=" + longitude + "&appid=" + Constants.Constants.weatherApiUrlKey + "&units=imperial";

                            request = (HttpWebRequest)WebRequest.Create(apiUrl);
                            request.AutomaticDecompression = DecompressionMethods.GZip;
                            response = (HttpWebResponse)request.GetResponse();
                            stream = response.GetResponseStream();
                            reader = new StreamReader(stream);
                            results = reader.ReadToEnd();

                            DataTable dtWeather = select.Select(connStr, "GetWeather", new List<SqlParameter> { new SqlParameter("@json", results) });
                            if (dtWeather.Rows.Count > 0)
                            {
                                string weatherResult = "";
                                foreach (DataRow drWeather in dtWeather.Rows)
                                {
                                    weatherResult += "Conditions: " + drWeather["Condition"].ToString() + "\n";
                                    weatherResult += "Current Temperature: " + drWeather["TempCurrent"].ToString() + "\n";
                                    weatherResult += "Feels Like: " + drWeather["TempFeelsLike"].ToString() + "\n";
                                    weatherResult += "Min/Max: " + drWeather["TempMin"].ToString() + " | " + drWeather["TempMax"].ToString() + "\n";
                                    weatherResult += "Wind Speed: " + drWeather["WindSpeed"].ToString() + "\n";
                                    weatherResult += "Humidity: " + drWeather["Humidity"].ToString() + "\n";

                                    var embed = new EmbedBuilder
                                    {
                                        Title = "BigBirdBot - Weather for " + city.ToUpper(),
                                        Color = Color.Teal,
                                        Description = weatherResult,
                                        ThumbnailUrl = "https://images.pexels.com/photos/186980/pexels-photo-186980.jpeg?auto=compress&cs=tinysrgb&w=1260&h=750&dpr=2"
                                    };

                                    await ReplyAsync(embed: embed.Build());
                                }
                            }
                            else
                            {
                                await ReplyAsync("No weather information found for City: " + city + ", State: " + state + ", Country: " + country);
                            }
                        }
                    }
                    else
                    {
                        await ReplyAsync("City: " + city + ", State: " + state + ", Country: " + country + " is not a valid geographic location.");
                    }
                }
                else
                {
                    await ReplyAsync("Please enter the weather with a City, State/Province, and Country.  Example: -weather Toronto, Ontario, Canada");
                }
                
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
