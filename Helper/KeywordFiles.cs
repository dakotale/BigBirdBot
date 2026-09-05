namespace DiscordBot.Helper;

/// <summary>
/// Classifies and resolves the value stored in <c>ChatKeyword.FilePath</c>, which is one of:
/// <list type="bullet">
///   <item>a URL — <c>http://…</c> / <c>https://…</c></item>
///   <item>a local file — stored as <c>file:&lt;keyword&gt;/&lt;name&gt;</c>, relative to
///         <c>Constants.keywordDirectory</c> so the same database works on any host</item>
///   <item>plain text — anything else</item>
/// </list>
/// Local files were absolute Windows paths (<c>C:\Temp\DiscordBot\cat\x.jpg</c>) before the
/// move to PostgreSQL; those still resolve here so a pre-migration row never crashes, but
/// nothing writes them any more (see <c>SQL/Database/postgres/002_KeywordPathsRelative.sql</c>).
/// </summary>
public static class KeywordFiles
{
    private const string Scheme = "file:";

    public static bool IsUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a stored local-file value (the <c>file:</c> form or a legacy absolute path).</summary>
    public static bool IsLocalFile(string value) =>
        value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(value);

    /// <summary>The value to store for a file saved as <paramref name="fileName"/> under <paramref name="keyword"/>'s folder.</summary>
    public static string ToStored(string keyword, string fileName) => $"{Scheme}{keyword}/{fileName}";

    /// <summary>
    /// Absolute on-disk path for a stored local-file value. Pass-through for a legacy
    /// absolute path. Throws if a <c>file:</c> value would resolve outside the keyword
    /// directory (path traversal).
    /// </summary>
    public static string Resolve(string value)
    {
        if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            return value; // already an absolute path (legacy row)

        string[] segments = value[Scheme.Length..]
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        string root = Path.GetFullPath(Constants.Constants.keywordDirectory);
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        string full = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Keyword path '{value}' resolves outside the keyword directory.");

        return full;
    }
}
