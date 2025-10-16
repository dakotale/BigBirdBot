using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;

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

            DataTable dt = procedure.Select(Constants.Constants.DISCORD_BOT_CONN_STR, "UpdateTwitterBroken", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(Context.Guild.Id.ToString())) });

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                    result = dr["Result"].ToString() ?? "";
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

            var random = new Random();
            var sb = new StringBuilder("Here are the 5 generated hex codes with reference photos\n\n");

            for (int i = 1; i <= 5; i++)
            {
                string color = random.Next(0x1000000).ToString("X6");
                sb.AppendLine($"{i}. https://www.color-hex.com/color/{color}\n");
            }

            string title = "BigBirdBot - Generate Palette";
            string embedCreatedBy = $"Command from: {Context.User.Username}";

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, sb.ToString(), "", embedCreatedBy, Discord.Color.Blue).Build(), ephemeral: true);
        }
    }
}
