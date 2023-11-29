using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Constants
{
    public static class Constants
    {
        public const string discordBotConnStr = "Server=localhost;DataBase=DiscordBot;Integrated Security=true";
        public const string campFireGif = "https://tenor.com/view/campfire-gif-24585871";
        public const string weatherQueryUrl = "http://api.weatherstack.com/current?access_key=b9f6c86ddb1225d6a44751e7f7312954&query=";
        public const string weatherApiUrl = "https://api.openweathermap.org/data/2.5/weather?";
        public const string weatherApiUrlKey = "26c4e15b39a791949c7e08ccba47df0c";
        public const string weatherGeoCodeApiUrl = "http://api.openweathermap.org/geo/1.0/direct?q=";
        public const string weatherGeoCodeReverseApiUrl = "http://api.openweathermap.org/geo/1.0/reverse?lat=";
        public const string uselessSiteUrl = "https://www.theuselesswebindex.com/website/";
        public const string botToken = "NTMyMzY3MDU4OTE1Mjk1MjMy.XJ_ypA.wgayR8pkXlju6Zwvv3YZus5n-xE";
        public const char msgPrefix = '-';
        public const string wikipediaUrl = "https://en.wikipedia.org/wiki/Special:Random";
        public const string lavaLinkPwd = "This 1s @ Sup3r S3cr3t P@ssword!";
        public const string spotifyClientId = "9d3327c7e115414386b546393c6e935d";
        public const string spotifyClientSecret = "e5c19c145b0e4ba68b8b76f3a5acf1b2";
        public const string openAiSecret = "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";
        public const string errorImageUrl = "https://cdn0.iconfinder.com/data/icons/shift-interfaces/32/Error-512.png";
        public const string minecraftModsLocation = @"C:\Users\Unmolded\Desktop\ForgeCreate_1182Server\Server\mods\";
        public static Int32 ToUnixTimestamp(this DateTime dateTime) => (int)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        public static string ToDiscordUnixTimeestampFormat(this DateTime dateTime) => $"<t:{dateTime.ToUnixTimestamp()}:R>";
        public const string testServerGuildId = "880569055856185354";
        public const string hltbApiUrl = "https://hltb-api.vercel.app/api?name=";
    }
}
