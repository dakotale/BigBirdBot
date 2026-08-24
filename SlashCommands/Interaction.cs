using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Json;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Web;

namespace DiscordBot.SlashCommands;

public partial class Games
{
    /// <summary>
    /// Fetches one question from the Open Trivia DB, posts it with shuffled answer choices as
    /// lettered fields, and adds matching emoji reactions so members can answer by reacting
    /// (scored later in BotHost.HandleTriviaReactionAsync).
    /// </summary>
    [SlashCommand("trivia", "Trivia Bot")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleTrivia()
    {
        await DeferAsync();

        // GetTriviaToken/GetTrivia were never real data-access operations — the SQL Server
        // procs used sp_OACreate/sp_OAMethod (OLE Automation) to make outbound HTTP calls and
        // OPENJSON to parse the response, entirely from within T-SQL. Postgres has no
        // equivalent mechanism, and there's nothing to query against a database for either
        // step, so this converts to doing the HTTP call and JSON parsing directly in C#
        // (exactly the pattern already used two lines down for the question fetch itself).
        string? token = null;
        try
        {
            using var tokenClient = new HttpClient();
            string tokenResponse = await tokenClient.GetStringAsync("https://opentdb.com/api_token.php?command=request");
            using var tokenDoc = JsonDocument.Parse(tokenResponse);
            token = tokenDoc.RootElement.GetProperty("token").GetString();
        }
        catch { /* token fetch failed */ }

        if (string.IsNullOrEmpty(token))
        {
            await SendTriviaError("Unable to retrieve token");
            return;
        }

        HttpClient client = new HttpClient();
        string responseBody = await client.GetStringAsync($"https://opentdb.com/api.php?amount=1&multiple&token={token}");

        if (!string.IsNullOrEmpty(responseBody))
        {
            using var triviaDoc = JsonDocument.Parse(responseBody);
            var results = triviaDoc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0)
            {
                await SendTriviaError("Unable to retrieve trivia.");
                return;
            }

            foreach (var result in results.EnumerateArray())
            {
                string category = result.GetProperty("category").GetString()!;
                string difficulty = result.GetProperty("difficulty").GetString()!;
                string question = result.GetProperty("question").GetString()!;
                string correctAnswer = result.GetProperty("correct_answer").GetString()!;
                var incorrectArr = result.GetProperty("incorrect_answers");

                // Build and shuffle answers
                var answers = new List<string> { correctAnswer, incorrectArr[0].GetString()! };
                if (incorrectArr.GetArrayLength() > 1) answers.Add(incorrectArr[1].GetString()!);
                if (incorrectArr.GetArrayLength() > 2) answers.Add(incorrectArr[2].GetString()!);

                // Decode HTML entities and shuffle
                answers = answers
                    .Select(HttpUtility.HtmlDecode)
                    .OrderBy(_ => Guid.NewGuid())
                    .ToList();

                // Build embed
                var embed = new EmbedBuilder
                {
                    Title = "Trivia",
                    ThumbnailUrl = "https://www.mtzion.lib.il.us/kids-teens/question-mark.jpg/@@images/image.jpeg",
                    Color = Color.Green,
                    Footer = new EmbedFooterBuilder { Text = $"Command from: {Username}" }
                };

                embed.AddField("Category", category);
                embed.AddField("Difficulty", Capitalize(difficulty));
                embed.AddField("Question", HttpUtility.HtmlDecode(question));

                var optionLabels = new[] { "A. ", "B. ", "C. ", "D. " };
                for (int i = 0; i < answers.Count; i++)
                    embed.AddField(optionLabels[i], answers[i]);

                var message = await FollowupAsync(embed: embed.Build());
                long messageId = Int64.Parse(message.Id.ToString());

                db.TriviaMessages.Add(new TriviaMessage
                {
                    TriviaMessageId = messageId,
                    CorrectAnswer = correctAnswer
                });
                await db.SaveChangesAsync();

                // Add emoji reactions
                var emojiOptions = new[] { "🇦", "🇧", "🇨", "🇩" }
                    .Take(answers.Count)
                    .Select(e => new Emoji(e));

                foreach (var emoji in emojiOptions)
                    await message.AddReactionAsync(emoji);
            }
        }
        else
        {
            await SendTriviaError("Unable to retrieve trivia.");
        }
    }

    /// <summary>Capitalizes only the first letter, lowercasing the rest.</summary>
    private string Capitalize(string input) =>
        string.IsNullOrEmpty(input) ? input : char.ToUpper(input[0]) + input[1..].ToLower();

    /// <summary>Posts a standard trivia error embed.</summary>
    private async Task SendTriviaError(string message)
    {
        await FollowupAsync(embed: _embed.BuildErrorEmbed("", message, Username).Build());
    }
}
