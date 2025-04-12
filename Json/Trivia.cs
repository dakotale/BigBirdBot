using Newtonsoft.Json.Linq;

namespace DiscordBot.Json
{
    public class Trivia
    {
        private string categoryName = null;
        private string typeName = null;
        private string difficulty = null;
        private string question = null;
        private string correctAnswer = null;
        private List<string> incorrectAnswer = null;

        public string CategoryName
        {
            get { return categoryName; }
            set { categoryName = value; }
        }

        public string TypeName
        {
            get { return typeName; }
            set { typeName = value; }
        }

        public string Difficulty
        {
            get { return difficulty; }
            set { difficulty = value; }
        }

        public string Question
        {
            get { return question; }
            set { question = value; }
        }

        public string CorrectAnswer
        {
            get { return correctAnswer; }
            set { correctAnswer = value; }
        }

        public List<string> IncorrectAnswers
        {
            get { return incorrectAnswer; }
            set { incorrectAnswer = value; }
        }
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
                                    categoryName = (string)p.Value;
                                }
                                if (p.Name.Equals("type"))
                                {
                                    typeName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(p.Value.ToString().ToLower());
                                }
                                if (p.Name.Equals("difficulty"))
                                {
                                    difficulty = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(p.Value.ToString().ToLower());
                                }
                                if (p.Name.Equals("question"))
                                {
                                    question = System.Net.WebUtility.HtmlDecode((string)p.Value);
                                }
                                if (p.Name.Equals("correct_answer"))
                                {
                                    correctAnswer = System.Net.WebUtility.HtmlDecode((string)p.Value);
                                }
                                if (p.Name.Equals("incorrect_answers"))
                                {
                                    List<string> badAnswers = new List<string>();
                                    foreach (var ans in p.Value)
                                    {
                                        badAnswers.Add(System.Net.WebUtility.HtmlDecode(ans.ToString()));
                                    }
                                    incorrectAnswer = badAnswers;
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
