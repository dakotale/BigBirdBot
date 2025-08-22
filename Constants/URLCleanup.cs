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
            ["tiktok.com"] = "vxtiktok.com",
            ["bsky.app"] = "bskx.app"
        };

        private static readonly string[] SocialMediaDomains = UrlReplacements.Keys
            .Select(domain => "https://" + domain)
            .ToArray();

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

        public bool HasSocialMediaEmbed(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            return SocialMediaDomains.Any(message.Contains);
        }
    }

}