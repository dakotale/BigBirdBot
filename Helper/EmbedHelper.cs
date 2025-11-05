using Discord;

namespace DiscordBot.Helper
{
    /// <summary>
    /// Forms the Message Embed to call instead of creating 
    /// a bunch of copy-paste EmbedBuilders...
    /// </summary>
    public class EmbedHelper
    {
        public EmbedHelper() { }
        public EmbedBuilder BuildMessageEmbed(string title, string description, string thumbnailUrl, string commandFrom, Color color, string imageUrl = null, string url = null)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"{title}",
                Color = color,
                Description = $"{description}",
                ThumbnailUrl = $"{thumbnailUrl}",
                ImageUrl = imageUrl,
                Url = url
            };

            embed.WithFooter(footer => footer.Text = commandFrom)
                                    .WithCurrentTimestamp();

            return embed;
        }

        public EmbedBuilder BuildImageEmbed(string title, string description, string thumbnailUrl, string commandFrom, Color color, Attachment attachment, string imageUrl = null, string url = null)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"{title}",
                Color = color,
                Description = $"{description}",
                ThumbnailUrl = $"{thumbnailUrl}",
                ImageUrl = imageUrl,
                Url = url,
            };

            embed.WithFooter(footer => footer.Text = commandFrom)
                                    .WithCurrentTimestamp();

            return embed;
        }

        public EmbedBuilder BuildErrorEmbed(string module, string description, string commandFrom)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"BigBirdBot - Error Module: {module}",
                Color = Color.Red,
                Description = $"{description}",
                ThumbnailUrl = Constants.Constants.errorImageUrl,
            };

            embed.WithFooter(footer => footer.Text = commandFrom)
                                    .WithCurrentTimestamp();

            return embed;
        }
    }
}
