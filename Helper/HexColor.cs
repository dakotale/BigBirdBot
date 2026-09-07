using System.Globalization;
using Discord;

namespace DiscordBot.Helper;

/// <summary>
/// Parses a hex colour string — <c>#RGB</c> or <c>#RRGGBB</c>, with or without the leading
/// <c>#</c> — into a Discord <see cref="Color"/>. Replaces
/// <c>System.Drawing.ColorTranslator.FromHtml</c>, which is Windows-only at runtime.
/// </summary>
public static class HexColor
{
    public static bool TryParse(string input, out Color color)
    {
        color = default;

        string h = input.Trim().TrimStart('#');

        if (h.Length == 3) // #abc -> #aabbcc
            h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);

        if (h.Length != 6 ||
            !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return false;

        color = new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
