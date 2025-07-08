using System.Data;
using System.Data.SqlClient;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    public class NoParameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("fixembed", "Let the bot fix embeds for Twitter, Reddit, Tiktok, and Bsky links.")]
        [EnabledInDm(false)]
        public async Task HandleTwitterEmbeds()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure procedure = new StoredProcedure();
            string result = "";

            DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "UpdateTwitterBroken", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())) });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString();
            }

            string title = "BigBirdBot - Twitter Embeds";
            string desc = result;
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build(), ephemeral: true);
        }

        [SlashCommand("genpalette", "Let the bot generated 5 random hex color codes for your next inspiration")]
        [EnabledInDm(true)]
        public async Task HandlePalette()
        {
            await DeferAsync(ephemeral: true);
            string result = "Here are the 5 generated hex codes with reference photos\n";

            for (int i = 0; i < 5; i++)
            {
                Random random = new Random();
                string color = String.Format("{0:X6}", random.Next(0x1000000));
                result += (i + 1).ToString() + ". https://www.color-hex.com/color/" + color + "\n\n";
            }
            string title = "BigBirdBot - Generate Palette";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, result, "", embedCreatedBy, Discord.Color.Blue).Build(), ephemeral: true);
        }
    }
}
