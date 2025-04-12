namespace DiscordBot.Misc
{
    public class EmojiText
    {
        private string emojiStr = null;
        private string emoji = null;

        public string EmojiStr
        {
            get { return emojiStr; }
            set { emojiStr = value; }
        }

        public string Emoji
        {
            get { return emoji; }
            set { emoji = value; }
        }

        public EmojiText() { }

        public string GetEmojiString(string emojiSentence)
        {
            string result = "";
            emojiStr = emojiSentence;
            foreach (var i in emojiStr.ToLower())
            {
                switch (i)
                {
                    case ' ':
                        result += " ";
                        break;
                    case 'a':
                        result += " :a: ";
                        break;
                    case 'b':
                        result += " :b: ";
                        break;
                    case 'c':
                        result += " :regional_indicator_c: ";
                        break;
                    case 'd':
                        result += " :regional_indicator_d: ";
                        break;
                    case 'e':
                        result += " :regional_indicator_e: ";
                        break;
                    case 'f':
                        result += " :regional_indicator_f: ";
                        break;
                    case 'g':
                        result += " :regional_indicator_g: ";
                        break;
                    case 'h':
                        result += " :regional_indicator_h: ";
                        break;
                    case 'i':
                        result += " :regional_indicator_i: ";
                        break;
                    case 'j':
                        result += " :regional_indicator_j: ";
                        break;
                    case 'k':
                        result += " :regional_indicator_k: ";
                        break;
                    case 'l':
                        result += " :regional_indicator_l: ";
                        break;
                    case 'm':
                        result += " :m: ";
                        break;
                    case 'n':
                        result += " :regional_indicator_n: ";
                        break;
                    case 'o':
                        result += " :o: ";
                        break;
                    case 'p':
                        result += " :regional_indicator_p: ";
                        break;
                    case 'q':
                        result += " :regional_indicator_q: ";
                        break;
                    case 'r':
                        result += " :regional_indicator_r: ";
                        break;
                    case 's':
                        result += " :regional_indicator_s: ";
                        break;
                    case 't':
                        result += " :regional_indicator_t: ";
                        break;
                    case 'u':
                        result += " :regional_indicator_u: ";
                        break;
                    case 'v':
                        result += " :v: ";
                        break;
                    case 'w':
                        result += " :regional_indicator_w: ";
                        break;
                    case 'x':
                        result += " :x: ";
                        break;
                    case 'y':
                        result += " :regional_indicator_y: ";
                        break;
                    case 'z':
                        result += " :regional_indicator_z: ";
                        break;
                    case '0':
                        result += " :zero: ";
                        break;
                    case '1':
                        result += " :one: ";
                        break;
                    case '2':
                        result += " :two: ";
                        break;
                    case '3':
                        result += " :three: ";
                        break;
                    case '4':
                        result += " :four: ";
                        break;
                    case '5':
                        result += " :five: ";
                        break;
                    case '6':
                        result += " :six: ";
                        break;
                    case '7':
                        result += " :seven: ";
                        break;
                    case '8':
                        result += " :eight: ";
                        break;
                    case '9':
                        result += " :nine: ";
                        break;
                }
            }

            return result;
        }
    }
}
