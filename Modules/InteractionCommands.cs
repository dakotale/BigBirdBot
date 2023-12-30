using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Json;
using KillersLibrary.Extensions;
using System.Data;
using System.Net;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using System.Web;

namespace DiscordBot.Modules
{
    public class InteractionCommands : ModuleBase<SocketCommandContext>
    {
        [Command("trivia")]
        public async Task HandleTrivia()
        {
            StoredProcedure stored = new StoredProcedure();
            string token = "";

            DataTable dtToken = stored.Select(Constants.Constants.discordBotConnStr, "GetTriviaToken", new List<System.Data.SqlClient.SqlParameter> { });
            if (dtToken.Rows.Count > 0)
            {
                foreach (DataRow dr in dtToken.Rows)
                    token = dr["Token"].ToString();

                DataTable dtTrivia = stored.Select(Constants.Constants.discordBotConnStr, "GetTrivia", new List<System.Data.SqlClient.SqlParameter> { new System.Data.SqlClient.SqlParameter("@Token", token) });

                if (dtTrivia.Rows.Count > 0)
                {
                    List<string> answers = new List<string>();
                    string answerList = "";
                    foreach (DataRow dr in dtTrivia.Rows)
                    {
                        EmbedHelper embedHelper = new EmbedHelper();

                        answers.Add(dr["CorrectAnswer"].ToString());
                        answers.Add(dr["FirstIncorrect"].ToString());
                        if (dr["SecondIncorrect"] != null)
                            answers.Add(dr["SecondIncorrect"].ToString());
                        if (dr["ThirdIncorrect"] != null)
                            answers.Add(dr["ThirdIncorrect"].ToString());

                        Random r = new Random();

                        answers = answers.OrderBy(s => r.Next()).ToList();

                        for(int i = 0; i < answers.Count; i++)
                            answers[i] = HttpUtility.HtmlDecode(answers[i]);

                        string question = HttpUtility.HtmlDecode(dr["Question"].ToString());
                        string difficulty = dr["Difficulty"].ToString();
                        difficulty = char.ToUpper(difficulty[0]) + difficulty.Substring(1);
                        string title = "BigBirdBot - Trivia";
                        string thumbnailUrl = "https://www.mtzion.lib.il.us/kids-teens/question-mark.jpg/@@images/image.jpeg";
                        string createdBy = "Command from: " + Context.User.Username;

                        EmbedBuilder embed = new EmbedBuilder();
                        embed.Title = title;
                        embed.ThumbnailUrl = thumbnailUrl;
                        embed.WithFooter(footer => footer.Text = createdBy);
                        embed.Color = Discord.Color.Green;
                        embed.AddField("Category", dr["Category"].ToString());
                        embed.AddField("Difficulty", difficulty);
                        embed.AddField("Question", question);
                        embed.AddField("A. ", answers[0]);

                        if (answers.Count == 2)
                            embed.AddField("B. ", answers[1]);
                        if (answers.Count == 3)
                        {
                            embed.AddField("B. ", answers[1]);
                            embed.AddField("C. ", answers[2]);
                        }
                        if (answers.Count == 4)
                        {
                            embed.AddField("B. ", answers[1]);
                            embed.AddField("C. ", answers[2]);
                            embed.AddField("D. ", answers[3]);
                        }

                        var message = await ReplyAsync(embed: embed.Build());
                        var messageId = Int64.Parse(message.Id.ToString());
                        stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddTriviaMessage", new List<System.Data.SqlClient.SqlParameter>
                        {
                            new System.Data.SqlClient.SqlParameter("@TriviaMessageID", messageId),
                            new System.Data.SqlClient.SqlParameter("@CorrectAnswer", dr["CorrectAnswer"].ToString())
                        });
                        
                        Emoji triviaA = new Emoji("🇦");
                        Emoji triviaB = new Emoji("🇧");
                        Emoji triviaC = new Emoji("🇨");
                        Emoji triviaD = new Emoji("🇩");

                        if (answers.Count == 2)
                        {
                            await message.AddReactionAsync(triviaA);
                            await message.AddReactionAsync(triviaB);
                        }

                        if (answers.Count == 3)
                        {
                            await message.AddReactionAsync(triviaA);
                            await message.AddReactionAsync(triviaB);
                            await message.AddReactionAsync(triviaC);
                        }

                        if (answers.Count == 4)
                        {
                            await message.AddReactionAsync(triviaA);
                            await message.AddReactionAsync(triviaB);
                            await message.AddReactionAsync(triviaC);
                            await message.AddReactionAsync(triviaD);
                        }
                    }
                }
                else
                {
                    // Can't get Trivia
                    EmbedHelper errorEmbed = new EmbedHelper();
                    await ReplyAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", "Unable to retrieve Token", Constants.Constants.errorImageUrl, "", Color.Red, "").Build());
                }
            }
            else
            {
                // Can't get token
                EmbedHelper errorEmbed = new EmbedHelper();
                await ReplyAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", "Unable to retrieve Token", Constants.Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
    }
}
