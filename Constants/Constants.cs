using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Constants
{
    public static class Constants
    {
        public static Int32 ToUnixTimestamp(this DateTime dateTime) => (int)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        public static string ToDiscordUnixTimeestampFormat(this DateTime dateTime) => $"<t:{dateTime.ToUnixTimestamp()}:R>";
    }
}
