using System.Text;

namespace DiscordBot.Misc
{
    /// <summary>Converts plain text into a string of Discord emoji-code letters/digits for a "big text" chat effect.</summary>
    public class EmojiText
    {
        private static readonly Dictionary<char, string> CharMap = new()
        {
            [' '] = " ",
            ['a'] = " :a: ",
            ['b'] = " :b: ",
            ['c'] = " :regional_indicator_c: ",
            ['d'] = " :regional_indicator_d: ",
            ['e'] = " :regional_indicator_e: ",
            ['f'] = " :regional_indicator_f: ",
            ['g'] = " :regional_indicator_g: ",
            ['h'] = " :regional_indicator_h: ",
            ['i'] = " :regional_indicator_i: ",
            ['j'] = " :regional_indicator_j: ",
            ['k'] = " :regional_indicator_k: ",
            ['l'] = " :regional_indicator_l: ",
            ['m'] = " :m: ",
            ['n'] = " :regional_indicator_n: ",
            ['o'] = " :o: ",
            ['p'] = " :regional_indicator_p: ",
            ['q'] = " :regional_indicator_q: ",
            ['r'] = " :regional_indicator_r: ",
            ['s'] = " :regional_indicator_s: ",
            ['t'] = " :regional_indicator_t: ",
            ['u'] = " :regional_indicator_u: ",
            ['v'] = " :v: ",
            ['w'] = " :regional_indicator_w: ",
            ['x'] = " :x: ",
            ['y'] = " :regional_indicator_y: ",
            ['z'] = " :regional_indicator_z: ",
            ['0'] = " :zero: ",
            ['1'] = " :one: ",
            ['2'] = " :two: ",
            ['3'] = " :three: ",
            ['4'] = " :four: ",
            ['5'] = " :five: ",
            ['6'] = " :six: ",
            ['7'] = " :seven: ",
            ['8'] = " :eight: ",
            ['9'] = " :nine: ",
        };

        /// <summary>Converts each letter/digit in the input to its emoji-code equivalent, joined with spaces; unmapped characters are dropped.</summary>
        public string GetEmojiString(string emojiSentence)
        {
            var sb = new StringBuilder();
            foreach (char c in emojiSentence.ToLower())
                if (CharMap.TryGetValue(c, out string token))
                    sb.Append(token);
            return sb.ToString();
        }
    }
}
