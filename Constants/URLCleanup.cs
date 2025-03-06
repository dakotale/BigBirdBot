namespace DiscordBot.Constants
{
    public class URLCleanup
    {
        public URLCleanup() { }

        public string CleanURLEmbed(string message)
        {
            if (message.Contains("https://fxtwitter.com"))
                message = message.Replace("fxtwitter.com", "dl.fxtwitter.com");
            if (message.Contains("https://vxtwitter.com"))
                message = message.Replace("vxtwitter.com", "dl.vxtwitter.com");
            if (message.Contains("https://twitter.com"))
                message = message.Replace("twitter.com", "dl.fxtwitter.com");
            if (message.Contains("https://x.com"))
                message = message.Replace("x.com", "dl.fxtwitter.com");
            if (message.Contains("https://tiktok.com"))
                message = message.Replace("tiktok.com", "vxtiktok.com");
            if (message.Contains("https://bsky.app"))
                message = message.Replace("bsky.app", "bskx.app");

            return message;
        }
    }
}