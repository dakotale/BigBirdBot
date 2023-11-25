using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Flurl;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class WebScraperCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();
        [Command("housing")]
        [Summary("List of open houses for the current bid period based on the world and size")]
        public async Task HandleHousing([Remainder] string msg)
        {
            audit.InsertAudit("housing", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
            EmbedHelper embedHelper = new EmbedHelper();

            if (msg.Split(",").Count() == 5)
            {
                // Valid
                string datacenter = msg.Split(",")[0].Trim().ToLower();
                string world = msg.Split(",")[1].Trim().ToLower();
                string size = msg.Split(",")[2].Trim().ToLower();
                string period = msg.Split(",")[3].Trim().ToLower();
                string type = msg.Split(",")[4].Trim().ToLower();

                List<string> availableDataCenters = new List<string>() { "aether", "crystal", "primal"};
                List<string> availableSizes = new List<string>() { "small", "medium", "large", "small&medium", "medium&large", "all" };
                List<string> availablePeriods = new List<string> { "current", "next" };
                List<string> availableTypes = new List<string> { "personal", "fc", "all" };
                List<string> availableResidences = new List<string> { "Shirogane", "Mist", "Empyreum", "Lavender", "Goblet" };

                if (!availableDataCenters.Contains(datacenter, StringComparer.OrdinalIgnoreCase))
                {
                    // Error
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid data center.\nThe valid NA data centers are: **aether, crystal, or primal**", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                if (!availablePeriods.Contains(period, StringComparer.OrdinalIgnoreCase))
                {
                    // Error
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid period.\nThe valid periods are: **next or current**", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                if (!availableSizes.Contains(size, StringComparer.OrdinalIgnoreCase))
                {
                    // Error
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid size.\nThe valid sizes are: **small, medium, large, small&medium, medium&large, or all**", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                if (!availableTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    // Error
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid type.\nThe valid types are: **personal, fc, or all**", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                if (size.Equals("small&medium"))
                    size = "small%2Bmedium";
                if (size.Equals("medium&large"))
                    size = "medium%2Blarge";

                HtmlWeb web = new HtmlWeb();
                string housingUrl = $"https://www.xiv-housing.com/en/north-america/{datacenter}/{world}?sizeFilter={size}";

                if (period.Equals("next"))
                    housingUrl = $"https://www.xiv-housing.com/en/north-america/{datacenter}/{world}?sizeFilter={size}&cycle=next";

                if (!type.Equals("all"))
                    housingUrl += $"&targetBuyer={type}";

                HttpClient client = new HttpClient();
                var response = await client.GetStringAsync(housingUrl);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response);

                var ffHousing = new List<FinalFantasyHousing>();

                string ward = "";
                string plot = "";
                string entry = "";
                string residence = "";
                string output = $"__Available {type.ToUpper()} {size.ToUpper()} Houses for {world.ToUpper()}__:\n";

                var wardDetails = htmlDoc.DocumentNode.Descendants("p").Where(s => s.InnerHtml.Contains("Ward")).Select(s => s.InnerText.Replace("\n", "").Trim().Replace("                                                                            ", ",").Split(",").Select(s => s.Trim())).ToList();
                var imgDetails = htmlDoc.DocumentNode.Descendants("img").Where(s => s.GetAttributeValue("alt", "") != "" && !s.GetAttributeValue("src", "").Contains("housing-banner")).Select(s => s.GetAttributeValue("src", "")).ToList();
                string deadline = htmlDoc.DocumentNode.Descendants("p").Where(s => s.InnerHtml.Contains("in the")).Select(s => s.InnerText.Replace("\n", "").Trim()).FirstOrDefault() ?? "";

                try
                {
                    // These are the same length
                    // 0 - Ward
                    // 1 - Plot
                    // 2 - Entries
                    for (int i = 0; i < wardDetails.Count; i++)
                    {
                        string imgUrl = imgDetails[i];

                        foreach (string s in availableResidences)
                        {
                            if (imgUrl.Contains(s, StringComparison.CurrentCultureIgnoreCase))
                                residence = s;
                            if (residence.Equals("Lavender"))
                                residence = "Lavender Beds";
                        }

                        // If < 3 there are no entries, need to default to avoid an exception
                        if (wardDetails[i].Count() < 3)
                        {
                            ward = wardDetails[i].ElementAt(0);
                            plot = wardDetails[i].ElementAt(1);
                            entry = "Entry Not Available";
                        }
                        else
                        {
                            ward = wardDetails[i].ElementAt(0);
                            plot = wardDetails[i].ElementAt(1);
                            entry = wardDetails[i].ElementAt(2);
                        }

                        var ff = new FinalFantasyHousing() { Entry = entry, Plot = plot, Ward = ward, Residence = residence };
                        ffHousing.Add(ff);
                    }
                    if (ffHousing.Count == 0)
                    {
                        var embedNoHouse = embedHelper.BuildMessageEmbed("BigBirdBot - Housing", "No houses available based on your parameters", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                        await ReplyAsync(embed: embedNoHouse.Build());
                        return;
                    }

                    if (deadline.Contains("until"))
                    {
                        // Need to split to get the time EST
                        string date = deadline.Split(":")[1] + ":" + deadline.Split(":")[2];
                        if (date.Contains(","))
                        {
                            date = date.Replace(",", "");
                            if (DateTime.TryParse(date, out DateTime dt))
                            {
                                dt = dt.AddHours(-6);
                                deadline = $"in the entry phase until: {dt} ET";
                            }
                        }
                    }

                    output += "**" + deadline + "**\n";


                    output += "\n**Empyreum**\n";

                    // Empyreum
                    if (ffHousing.Where(s => s.Residence.Equals("Empyreum")).ToList().Count > 0)
                    {
                        foreach (var e in ffHousing.Where(s => s.Residence.Equals("Empyreum")).OrderBy(s => s.Entry).ToList())
                            output += $"{e.Ward} - {e.Plot} - **{e.Entry}**\n";
                    }
                    else
                        output += "None\n";

                    output += "\n**Goblet**\n";

                    // Goblet
                    if (ffHousing.Where(s => s.Residence.Equals("Goblet")).ToList().Count > 0)
                    {
                        foreach (var e in ffHousing.Where(s => s.Residence.Equals("Goblet")).OrderBy(s => s.Entry).ToList())
                            output += $"{e.Ward} - {e.Plot} - **{e.Entry}**\n";
                    }
                    else
                        output += "None\n";

                    output += "\n**Lavender Beds**\n";

                    // Lavender Beds
                    if (ffHousing.Where(s => s.Residence.Equals("Lavender Beds")).ToList().Count > 0)
                    {
                        foreach (var e in ffHousing.Where(s => s.Residence.Equals("Lavender Beds")).OrderBy(s => s.Entry).ToList())
                            output += $"{e.Ward} - {e.Plot} - **{e.Entry}**\n";
                    }
                    else
                        output += "None\n";

                    output += "\n**Mist**\n";

                    // Mist
                    if (ffHousing.Where(s => s.Residence.Equals("Mist")).ToList().Count > 0)
                    {
                        foreach (var e in ffHousing.Where(s => s.Residence.Equals("Mist")).OrderBy(s => s.Entry).ToList())
                            output += $"{e.Ward} - {e.Plot} - **{e.Entry}**\n";
                    }
                    else
                        output += "None\n";

                    output += "\n**Shirogane**\n";

                    // Shirogane
                    if (ffHousing.Where(s => s.Residence.Equals("Shirogane")).ToList().Count > 0)
                    {
                        foreach (var e in ffHousing.Where(s => s.Residence.Equals("Shirogane")).OrderBy(s => s.Entry).ToList())
                            output += $"{e.Ward} - {e.Plot} - **{e.Entry?? "N/A"}**\n";
                    }
                    else
                        output += "None\n";

                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Housing", output, "", "", Discord.Color.Blue, "");
                    await ReplyAsync(embed: embed.Build());
                }
                catch (Exception ex)
                {
                    // Error
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Housing", "No houses available based on your parameters", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                    return;
                }
            }
            else
            {
                // Error
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter five parameters for this command.\n**Example: -housing primal, excalibur, small, current, personal**", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await ReplyAsync(embed: embed.Build());
                return;
            }
        }
    }
}
