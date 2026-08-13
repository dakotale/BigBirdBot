namespace DiscordBot.Constants
{
    /// <summary>
    /// Static cleanup of common /fixembed handles.
    /// </summary>
    public class URLCleanup
    {
        private static readonly Dictionary<string, string> UrlReplacements = new()
        {
            ["fxtwitter.com"] = "dl.fxtwitter.com",
            ["vxtwitter.com"] = "dl.fxtwitter.com",
            ["twitter.com"] = "dl.fxtwitter.com",
            ["x.com"] = "dl.fxtwitter.com",
            ["girlcockx.com"] = "dl.fxtwitter.com",
            ["tiktok.com"] = "vxtiktok.com",
            ["bsky.app"] = "bskx.app",
            ["reddit.com"] = "rxddit.com",
            ["www.reddit.com"] = "rxddit.com"
        };

        // Precompute the full "https://domain" form of every known key once, since
        // HasSocialMediaEmbed below is called on every guild message.
        private static readonly string[] SocialMediaDomains = UrlReplacements.Keys
            .Select(domain => "https://" + domain)
            .ToArray();

        /// <summary>Rewrites a known social-media domain in <paramref name="message"/> to its embed-friendly mirror (e.g. fxtwitter).</summary>
        public string CleanURLEmbed(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            foreach (var kvp in UrlReplacements)
            {
                var oldValue = "https://" + kvp.Key;
                if (message.Contains(oldValue))
                {
                    message = message.Replace(kvp.Key, kvp.Value);
                }
            }

            return message;
        }

        /// <summary>True if the message contains a link to any domain this bot knows how to fix the embed for.</summary>
        public bool HasSocialMediaEmbed(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            return SocialMediaDomains.Any(message.Contains);
        }
    }

}
