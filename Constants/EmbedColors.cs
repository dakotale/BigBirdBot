using Discord;

namespace DiscordBot.Constants
{
    /// <summary>
    /// Shared embed color palette so slash commands don't each redefine the same RGB values.
    /// </summary>
    public static class EmbedColors
    {
        public static readonly Color Green   = new(87,  242, 135);
        public static readonly Color Red     = new(237, 66,  69);
        public static readonly Color Blue    = new(88,  101, 242);
        public static readonly Color Gold    = new(255, 215, 0);
        public static readonly Color Grey    = new(128, 128, 128);
        public static readonly Color Purple  = new(155, 89,  182);
        public static readonly Color Yellow  = new(254, 231, 92);
        public static readonly Color Orange  = new(255, 140, 0);
        public static readonly Color Peach   = new(255, 179, 71);
        public static readonly Color Amber   = new(255, 165, 0);
        public static readonly Color Spotify = new(30,  215, 96);
    }
}
