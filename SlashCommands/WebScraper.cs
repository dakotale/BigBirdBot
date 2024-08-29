using Discord.Interactions;
using DiscordBot.Helper;
using DiscordBot.Misc;
using HtmlAgilityPack;

namespace DiscordBot.SlashCommands
{
    public class WebScraper : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("housing", "List of open houses for the current bid period based on the world and size.")]
        [EnabledInDm(true)]
        public async Task HandleHousing(
                [Choice("Aether", "aether"), Choice("Crystal", "crystal"), Choice("Primal", "primal")] string dataCenter,
                [Choice("Aether - Adamantoise", "adamantoise"), Choice("Aether - Cactuar", "cactuar"), Choice("Aether - Faerie", "faerie"), Choice("Aether - Gilgamesh", "gilgamesh"), Choice("Aether - Jenova", "jenova"), Choice("Aether - Midgardsormr", "midgardsormr"), Choice("Aether - Sargatanas", "sargatanas"), Choice("Aether - Siren", "siren"), Choice("Crystal - Balmung", "balmung"), Choice("Crystal - Brynhildr", "brynhildr"), Choice("Crystal - Coeurl", "coeurl"), Choice("Crystal - Diabolos", "diabolos"), Choice("Crystal - Goblin", "goblin"), Choice("Crystal - Malboro", "malboro"), Choice("Crystal - Mateus", "mateus"), Choice("Crystal - Zalera", "zalera"), Choice("Primal - Behemoth", "behemoth"), Choice("Primal - Excalibur", "excalibur"), Choice("Primal - Exodus", "exodus"), Choice("Primal - Famfrit", "famfrit"), Choice("Primal - Hyperion", "hyperion"), Choice("Primal - Lamia", "lamia"), Choice("Primal - Leviathan", "leviathan"), Choice("Primal - Ultros", "ultros")] string residence,
                [Choice("Small", "small"), Choice("Medium", "medium"), Choice("Large", "large"), Choice("Small and Medium", "small&medium"), Choice("Medium and Large", "medium&large"), Choice("All", "all")] string size, 
                [Choice("Current", "current"), Choice("Next", "next")] string period, 
                [Choice("Personal", "personal"), Choice("Free Company", "fc"), Choice("All", "all")] string houseType)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();

            // Valid
            dataCenter = dataCenter.Trim().ToLower();
            residence = residence.Trim().ToLower();
            size = size.Trim().ToLower();
            period = period.Trim().ToLower();
            houseType = houseType.Trim().ToLower();

            List<string> availableResidences = new List<string> { "Shirogane", "Mist", "Empyreum", "Lavender", "Goblet" };

            HtmlWeb web = new HtmlWeb();
            string housingUrl = $"https://www.xiv-housing.com/en/north-america/{dataCenter}/{residence}?sizeFilter={size}";

            if (period.Equals("next"))
                housingUrl = $"https://www.xiv-housing.com/en/north-america/{dataCenter}/{residence}?sizeFilter={size}&cycle=next";

            if (!houseType.Equals("all"))
                housingUrl += $"&targetBuyer={houseType}";

            HttpClient client = new HttpClient();
            var response = await client.GetStringAsync(housingUrl);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            var ffHousing = new List<FinalFantasyHousing>();

            string ward = "";
            string plot = "";
            string entry = "";
            string output = $"__Available {houseType.ToUpper()} {size.ToUpper()} Houses for {residence.ToUpper()}__:\n";

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
                    await FollowupAsync(embed: embedNoHouse.Build());
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
                            string dtOutput = dt.ToString("F");
                            deadline = $"Entry Phase Deadline: {dtOutput} ET";
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
                        output += $"{e.Ward} - {e.Plot} - **{e.Entry ?? "N/A"}**\n";
                }
                else
                    output += "None\n";

                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Housing", output, "", "", Discord.Color.Blue, "");
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                // Error
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Housing", "No houses available based on your parameters", Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await FollowupAsync(embed: embed.Build());
                return;
            }
        }
    }
}
