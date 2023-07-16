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

        [Command("maptrivia")]
        public async Task HandleMapTriviaCommand()
        {
            audit.InsertAudit("maptrivia", Context.User.Username, Constants.Constants.discordBotConnStr);

            // Concept: Pull random street view images and provide 4 options on where this is located
            // API: https://api.3geonames.org/randomland.json
            // Pulls Latitude and Longitude, use lookup from weather
            // TODO: Generate a table of City, Country to pull from with a randomizer for the choices
            string apiUrl = "https://api.3geonames.org/randomland.json";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            string results = string.Empty;
            Random r = new Random();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            results = reader.ReadToEnd();

            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure select = new StoredProcedure();
            DataTable dtCityState = select.Select(connStr, "GetCityStateCountryFromLatLonJson", new List<SqlParameter> { new SqlParameter("@json", results) });
            if (dtCityState.Rows.Count > 0)
            {
                foreach (DataRow drCityState in dtCityState.Rows)
                {
                    // Lookup the Lat/Lon with the Weather GeoCode to get City, State, Country
                    // https://openweathermap.org/api/geocoding-api
                    apiUrl = Constants.Constants.weatherGeoCodeReverseApiUrl + drCityState["lat"].ToString() + "&lon=" + drCityState["lon"].ToString() + "&limit=1&appid=" + Constants.Constants.weatherApiUrlKey;
                    request = (HttpWebRequest)WebRequest.Create(apiUrl);
                    request.AutomaticDecompression = DecompressionMethods.GZip;
                    results = string.Empty;

                    response = (HttpWebResponse)request.GetResponse();
                    stream = response.GetResponseStream();
                    reader = new StreamReader(stream);
                    results = reader.ReadToEnd();

                    DataTable dtLonLat = select.Select(connStr, "GetLatLonFromJson", new List<SqlParameter> { new SqlParameter("@json", results) });
                    if (dtLonLat.Rows.Count > 0)
                    {
                        foreach (DataRow drLonLat in dtLonLat.Rows)
                        {
                            List<string> map = new List<string>();
                            string city = drLonLat["City"].ToString() ?? "";
                            //string state = drLonLat["State"].ToString() ?? "";
                            string country = drLonLat["Country"].ToString() ?? "";
                            string latitude = drLonLat["Lat"].ToString() ?? "";
                            string longitude = drLonLat["Lon"].ToString() ?? "";
                            int size = map.Count();

                            DataTable dtCountry = select.Select(connStr, "GetCountry", new List<SqlParameter> { new SqlParameter("@Country", country) });
                            foreach (DataRow drCountry in dtCountry.Rows)
                            {
                                string rightAnswer = city + ", " + drCountry["Country"].ToString();

                                map.Add(rightAnswer);

                                string test = "https://www.mapquestapi.com/staticmap/v5/map?key=tFeGCad6XIa7OKQGXv7xnJr5GGsu1ZQJ&center=" + latitude + "," + longitude + "&zoom=10&type=sat&size=600,400@2x";
                                DataTable wrongAnswers = select.Select(connStr, "GetMapTriviaCityCountry", new List<SqlParameter> { });
                                foreach (DataRow drWrong in wrongAnswers.Rows)
                                {
                                    map.Add(drWrong["City"].ToString() + ", " + drWrong["CountryName"].ToString());
                                }

                                // Form multiple choice
                                string customId = "menu";

                                var menu = new SelectMenuBuilder()
                                {
                                    CustomId = customId
                                };

                                map = map.OrderBy(x => r.Next()).ToList();

                                foreach (var val in map)
                                {
                                    if (val.Equals(rightAnswer))
                                    {
                                        menu.AddOption(val, "Congratulations " + Context.User.Username + ", you're right! " + val + " :smile:");
                                    }
                                    else
                                    {
                                        menu.AddOption(val, ":x: " + val + " :x: Right Answer - " + rightAnswer);
                                    }
                                }

                                var component = new ComponentBuilder();
                                component.WithSelectMenu(menu);

                                //string imageUrl = drMapQuest["ImageURL"].ToString();
                                var embed = new EmbedBuilder();
                                embed.WithTitle("Location");
                                embed.WithImageUrl(test);//(imageUrl);

                                await ReplyAsync("Where do you think this marker is located?", components: component.Build(), embed: embed.Build());

                                var messages = await Context.Channel.GetMessagesAsync(1).FlattenAsync();
                                var msgRef = new MessageReference(messages.First().Id);

                                //AddMessage((ulong)msgRef.MessageId, menu.CustomId);
                            }
                        }
                    }
                    else
                    {
                        await ReplyAsync("Unable to find a valid geographic location.");
                    }
                }
            }
            else
            {
                await ReplyAsync("Unable to find a valid geographic location.");
            }
        }
    }
}
