using Newtonsoft.Json.Linq;

namespace DiscordBot.Json
{
    public class Trivia
    {
        public string CategoryName { get; set; } = null;

        public string TypeName { get; set; } = null;

        public string Difficulty { get; set; } = null;

        public string Question { get; set; } = null;

        public string CorrectAnswer { get; set; } = null;

        public List<string> IncorrectAnswers { get; set; } = null;
        public Trivia() { }

        public Trivia(string trivia)
        {
            JObject obj = JObject.Parse(trivia);
            if (obj.Count > 0)
            {
                foreach (KeyValuePair<string, JToken> item in obj)
                {
                    string key = item.Key;
                    if (key.Equals("results"))
                    {
                        JArray arr = JArray.Parse(item.Value.ToString());
                        foreach (JObject o in arr.Children<JObject>())
                        {
                            foreach (JProperty p in o.Properties())
                            {
                                if (p.Name.Equals("category"))
                                {
                                    CategoryName = (string)p.Value;
                                }
                                if (p.Name.Equals("type"))
                                {
                                    TypeName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(p.Value.ToString().ToLower());
                                }
                                if (p.Name.Equals("difficulty"))
                                {
                                    Difficulty = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(p.Value.ToString().ToLower());
                                }
                                if (p.Name.Equals("question"))
                                {
                                    Question = System.Net.WebUtility.HtmlDecode((string)p.Value);
                                }
                                if (p.Name.Equals("correct_answer"))
                                {
                                    CorrectAnswer = System.Net.WebUtility.HtmlDecode((string)p.Value);
                                }
                                if (p.Name.Equals("incorrect_answers"))
                                {
                                    List<string> badAnswers = new List<string>();
                                    foreach (JToken ans in p.Value)
                                    {
                                        badAnswers.Add(System.Net.WebUtility.HtmlDecode(ans.ToString()));
                                    }
                                    IncorrectAnswers = badAnswers;
                                }
                            }
                        }
                    }
                }
            }
        }

        public List<Trivia> GetTriviaDetails(string trivia)
        {
            List<Trivia> triviaDetails = new List<Trivia>();
            JObject obj = JObject.Parse(trivia);
            if (obj.Count > 0)
            {
                triviaDetails.Add(new Trivia(trivia));
            }

            return triviaDetails;
        }
    }
}
