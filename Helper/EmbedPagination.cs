using System.Text;

namespace DiscordBot.Helper;

/// <summary>Splits pre-formatted text into embed-description-sized pages.</summary>
public static class EmbedPagination
{
    /// <summary>
    /// Chunks <paramref name="lines"/> into pages whose concatenated length never exceeds
    /// <paramref name="maxLength"/> (Discord's embed <c>Description</c> caps at 4096 characters),
    /// without ever splitting a single line across two pages. Always returns at least one page,
    /// which is empty when <paramref name="lines"/> is empty.
    /// </summary>
    public static List<string> BuildPages(IEnumerable<string> lines, int maxLength)
    {
        var pages = new List<string>();
        var current = new StringBuilder();

        foreach (string line in lines)
        {
            // Only flush a non-empty accumulator — a single line longer than maxLength on its
            // own still gets its own page rather than producing a spurious empty page before it.
            if (current.Length > 0 && current.Length + line.Length > maxLength)
            {
                pages.Add(current.ToString());
                current.Clear();
            }
            current.Append(line);
        }

        pages.Add(current.ToString());
        return pages;
    }
}
