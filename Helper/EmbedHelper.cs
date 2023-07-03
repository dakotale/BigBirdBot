using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Helper
{
    public class EmbedHelper
    {
        public EmbedHelper() { }
        public EmbedBuilder BuildMessageEmbed(string title, string description, string thumbnailUrl, string commandFrom, Color color, string imageUrl = null, string url = null)
        {
            var embed = new EmbedBuilder
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
    }
}
