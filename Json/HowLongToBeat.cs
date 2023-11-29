using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class HowLongToBeat
    {
        public string name { get; set; }
        public int gameplayMain { get; set; }
        public int gameplayMainExtra { get; set; }
        public int gameplayCompletionist { get; set; }
        public string platforms { get; set; }
        public string imageUrl { get; set; }
    }
}
