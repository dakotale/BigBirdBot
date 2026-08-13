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

        /// <summary>
        /// Builds the common "title + colored description[, stat fields], timestamped" embed
        /// shape used for game results, confirmations, and status messages throughout the bot —
        /// no thumbnail or image. Pass <paramref name="fields"/> for inline stat fields (e.g.
        /// Bet/Payout/Balance), <paramref name="footer"/>/<paramref name="footerIconUrl"/> for a
        /// footer, and set <paramref name="timestamp"/> to false for transient frames (e.g.
        /// mid-animation embeds) that intentionally don't show one. Use
        /// <see cref="BuildMessageEmbed"/> instead when a thumbnail/image is needed.
        /// </summary>
        public EmbedBuilder BuildSimpleEmbed(
            string title, string description, Color color,
            string? footer = null, string? footerIconUrl = null, bool timestamp = true,
            params (string Name, string Value, bool Inline)[] fields)
        {
            var embed = new EmbedBuilder
            {
                Title = title,
                Color = color,
                Description = description
            };

            foreach (var field in fields)
                embed.AddField(field.Name, field.Value, field.Inline);

            if (footer is not null)
                embed.WithFooter(footer, footerIconUrl);

            if (timestamp)
                embed.WithCurrentTimestamp();

            return embed;
        }

        public EmbedBuilder BuildErrorEmbed(string module, string description, string commandFrom)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"Error Module: {module}",
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

