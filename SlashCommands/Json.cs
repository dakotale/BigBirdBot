using Discord;
using Discord.Commands;
using Discord.Interactions;
using DiscordBot.Helper;
using DiscordBot.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands
{
    public class Json : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("wikip", "Random Wikipedia article.")]
        [EnabledInDm(true)]
        public async Task HandleWikipediaCommand()
        {
            await DeferAsync();
            try
            {
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
                        await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Blue).Build());
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("hltb", "Using HowLongToBeat API, pull the information on a game for how it will take to beat.")]
        [EnabledInDm(true)]
        public async Task HandleHowLongToBeat([MinLength(1)] string game)
        {
            await DeferAsync();

            EmbedHelper embedHelper = new EmbedHelper();
            try
            {
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
                                    await FollowupAsync(embed: embed.Build());
                                    break;
                                }
                            }
                            else
                            {
                                foreach (var item in howToBeatDetails)
                                {
                                    string desc = $"Details for **{item.name}**\n**- Main Story: {item.gameplayMain} hours \n- Main + Extra: {item.gameplayMainExtra} hours \n- Completionist: {item.gameplayCompletionist} hours**";

                                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - How Long to Beat", desc, "", "", Color.Blue, item.imageUrl);
                                    await FollowupAsync(embed: embed.Build());
                                }
                            }
                        }
                        else
                        {
                            var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The game entered was not a valid title.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                            await FollowupAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The game entered was not a valid title or data was not found.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                        await FollowupAsync(embed: embed.Build());
                    }
                }
                else
                {
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The game entered was not a valid title or data was not found.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await FollowupAsync(embed: embed.Build());
                }
            }
            catch (Exception e)
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }
    }
}
