using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using RequireBotPermissionAttribute = Discord.Interactions.RequireBotPermissionAttribute;

namespace DiscordBot.SlashCommands
{
    public class NoParameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("info", "Shows information of the current server.")]
        [EnabledInDm(false)]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        public async Task HandleServerInformation()
        {
            await DeferAsync();
            double botPercentage = Math.Round(Context.Guild.Users.Count(x => x.IsBot) / Context.Guild.MemberCount * 100d, 2);

            string bannerUrl = Context.Guild.BannerUrl ?? "";

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithTitle($"Server Information for {Context.Guild.Name}")
                .WithDescription(
                    $"**Guild name:** {Context.Guild.Name}\n" +
                    $"**Guild ID:** {Context.Guild.Id}\n" +
                    $"**Created On:** {Context.Guild.CreatedAt:MM/dd/yyyy}\n" +
                    $"**Owner:** {Context.Guild.Owner}\n\n" +
                    $"**Users:** {Context.Guild.MemberCount - Context.Guild.Users.Count(x => x.IsBot)}\n" +
                    $"**Bots:** {Context.Guild.Users.Count(x => x.IsBot)} [ {botPercentage}% ]\n" +
                    $"**Text Channels:** {Context.Guild.TextChannels.Count}\n" +
                    $"**Voice Channels:** {Context.Guild.VoiceChannels.Count}\n" +
                    $"**Roles:** {Context.Guild.Roles.Count}\n" +
                    $"**Emotes:** {Context.Guild.Emotes.Count}\n" +
                    $"**Stickers:** {Context.Guild.Stickers.Count}\n\n" +
                    $"**Security level:** {Context.Guild.VerificationLevel}")
                 .WithImageUrl(bannerUrl)
                 .WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("fixembed", "Let the bot fix embeds for Twitter, Reddit, Tiktok, and Bsky links.")]
        [EnabledInDm(false)]
        public async Task HandleTwitterEmbeds()
        {
            await DeferAsync();
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
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Green, imageUrl).Build());
        }
    }
}
