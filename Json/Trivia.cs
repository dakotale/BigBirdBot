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
            var obj = JObject.Parse(trivia);

            if (obj.TryGetValue("results", out JToken? resultsToken) && resultsToken is JArray resultsArray && resultsArray.Count > 0)
            {
                var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;

                var firstResult = resultsArray.First as JObject;
                if (firstResult != null)
                {
                    if (firstResult.TryGetValue("category", out JToken? categoryToken))
                        CategoryName = categoryToken.ToString();

                    if (firstResult.TryGetValue("type", out JToken? typeToken))
                        TypeName = textInfo.ToTitleCase(typeToken.ToString().ToLower());

                    if (firstResult.TryGetValue("difficulty", out JToken? difficultyToken))
                        Difficulty = textInfo.ToTitleCase(difficultyToken.ToString().ToLower());

                    if (firstResult.TryGetValue("question", out JToken? questionToken))
                        Question = System.Net.WebUtility.HtmlDecode(questionToken.ToString());

                    if (firstResult.TryGetValue("correct_answer", out JToken? correctToken))
                        CorrectAnswer = System.Net.WebUtility.HtmlDecode(correctToken.ToString());

                    if (firstResult.TryGetValue("incorrect_answers", out JToken? incorrectToken) && incorrectToken is JArray incorrectArray)
                    {
                        IncorrectAnswers = incorrectArray
                            .Select(ans => System.Net.WebUtility.HtmlDecode(ans.ToString()))
                            .ToList();
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
