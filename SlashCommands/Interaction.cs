using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Json;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Reflection.Metadata.Ecma335;
using System.Web;

namespace DiscordBot.SlashCommands
{
    public class Interaction : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("trivia", "Trivia Bot")]
        [EnabledInDm(true)]
        public async Task HandleTrivia()
        {
            await DeferAsync();

            var stored = new StoredProcedure();
            var connStr = Constants.Constants.DISCORD_BOT_CONN_STR;

            var dtToken = stored.Select(connStr, "GetTriviaToken", new List<SqlParameter>());
            if (dtToken.Rows.Count == 0)
            {
                await SendError("Unable to retrieve token");
                return;
            }

            string token = dtToken.Rows[0]["Token"].ToString();
            HttpClient client = new HttpClient();
            string responseBody = await client.GetStringAsync($"https://opentdb.com/api.php?amount=1&multiple&token={token}");

            if (!string.IsNullOrEmpty(responseBody))
            {
                DataTable dtTrivia = stored.Select(connStr, "GetTrivia", new List<SqlParameter> { new SqlParameter("@ResponseText", responseBody) });

                if (dtTrivia.Rows.Count == 0)
                {
                    await SendError("Unable to retrieve trivia.");
                    return;
                }

                foreach (DataRow dr in dtTrivia.Rows)
                {
                    // Build and shuffle answers
                    var answers = new List<string>
                    {
                        dr["CorrectAnswer"].ToString(),
                        dr["FirstIncorrect"].ToString()
                    };

                    if (dr["SecondIncorrect"] != DBNull.Value)
                        answers.Add(dr["SecondIncorrect"].ToString());
                    if (dr["ThirdIncorrect"] != DBNull.Value)
                        answers.Add(dr["ThirdIncorrect"].ToString());

                    // Decode HTML entities and shuffle
                    answers = answers
                        .Select(HttpUtility.HtmlDecode)
                        .OrderBy(_ => Guid.NewGuid()) // Better than new Random() inside loop
                        .ToList();

                    // Build embed
                    var embed = new EmbedBuilder
                    {
                        Title = "BigBirdBot - Trivia",
                        ThumbnailUrl = "https://www.mtzion.lib.il.us/kids-teens/question-mark.jpg/@@images/image.jpeg",
                        Color = Color.Green,
                        Footer = new EmbedFooterBuilder { Text = $"Command from: {Context.User.Username}" }
                    };

                    embed.AddField("Category", dr["Category"].ToString());
                    embed.AddField("Difficulty", Capitalize(dr["Difficulty"].ToString()));
                    embed.AddField("Question", HttpUtility.HtmlDecode(dr["Question"].ToString()));

                    var optionLabels = new[] { "A. ", "B. ", "C. ", "D. " };
                    for (int i = 0; i < answers.Count; i++)
                        embed.AddField(optionLabels[i], answers[i]);

                    var message = await FollowupAsync(embed: embed.Build());
                    long messageId = Int64.Parse(message.Id.ToString());

                    stored.UpdateCreate(connStr, "AddTriviaMessage", new List<SqlParameter>
                    {
                        new SqlParameter("@TriviaMessageID", messageId),
                        new SqlParameter("@CorrectAnswer", dr["CorrectAnswer"].ToString())
                    });

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
                await SendError("Unable to retrieve trivia.");
                return;
            }
        }

        // Utility function to capitalize a string
        private string Capitalize(string input) =>
            string.IsNullOrEmpty(input) ? input : char.ToUpper(input[0]) + input[1..].ToLower();

        // Utility to send error embed
        private async Task SendError(string message)
        {
            var errorEmbed = new EmbedHelper();
            await FollowupAsync(embed: errorEmbed.BuildErrorEmbed("", message, Context.User.Username).Build());
        }
    }
}
