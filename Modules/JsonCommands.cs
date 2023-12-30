using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Json;
using System.Data.SqlClient;
using System.Data;
using System.Net;
using DiscordBot.Helper;
using SpotifyAPI.Web.Http;
using System.Text.Json;

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
                audit.InsertAudit("wikip", Context.User.Id.ToString(), Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
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
                        await ReplyAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Blue).Build());
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
            audit.InsertAudit("weather", Context.User.Id.ToString(), Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());

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

        [Command("hltb")]
        [Summary("Using HowLongToBeat API, pull the information on a game for how it will take to beat.")]
        public async Task HandleHowLongToBeat([Remainder] string game)
        {
            EmbedHelper embedHelper = new EmbedHelper();
            try
            {
                audit.InsertAudit("hltb", Context.User.Id.ToString(), Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());

                string results = string.Empty;
                string url = Constants.Constants.hltbApiUrl + game.Trim();

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                var response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream stream = response.GetResponseStream();
                    StreamReader streamReader = new StreamReader(stream);

                    results = streamReader.ReadToEnd();

                    if (results != string.Empty)
                    {
                        var howToBeatDetails = JsonSerializer.Deserialize<HowLongToBeat[]>(results);

                        if (howToBeatDetails.Length > 0)
                        {
                            if (howToBeatDetails.Length > 1)
                            {
                                // Pulling only the first 1
                                foreach (var item in howToBeatDetails)
                                {
                                    string desc = $"Top Search Details for **{item.name}**\n**- Main Story: {item.gameplayMain} hours \n- Main + Extra: {item.gameplayMainExtra} hours \n- Completionist: {item.gameplayCompletionist} hours**";

                                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - How Long to Beat", desc, "", "", Color.Blue, item.imageUrl);
                                    await ReplyAsync(embed: embed.Build());
                                    break;
                                }
                            }
                            else
                            {
                                foreach (var item in howToBeatDetails)
                                {
                                    string desc = $"Details for **{item.name}**\n**- Main Story: {item.gameplayMain} hours \n- Main + Extra: {item.gameplayMainExtra} hours \n- Completionist: {item.gameplayCompletionist} hours**";

                                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - How Long to Beat", desc, "", "", Color.Blue, item.imageUrl);
                                    await ReplyAsync(embed: embed.Build());
                                }
                            }
                        }
                        else
                        {
                            var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The game entered was not a valid title.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                            await ReplyAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The game entered was not a valid title or data was not found.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                        await ReplyAsync(embed: embed.Build());
                    }
                }
                else
                {
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error","The game entered was not a valid title or data was not found.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }
            catch (Exception e)
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
