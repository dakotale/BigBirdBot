using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Misc;
using Figgle;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using WolframAlphaNet;
using WolframAlphaNet.Objects;

namespace DiscordBot.SlashCommands
{
    public class Parameter : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("random", "Randomize a number from the range provided.")]
        [EnabledInDm(true)]
        public async Task GenerateRandomNumber([MinValue(1), MaxValue(int.MaxValue)]int number)
        {
            await DeferAsync();
            Random r = new Random();
            int i = r.Next(1, number + 1);

            string title = "BigBirdBot - Random";
            string desc = $"{Context.User.Mention} rolled a **{i}**";
            string thumbnailUrl = "";
            string createdBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [SlashCommand("etext", "Convert your message into emojis.")]
        [EnabledInDm(true)]
        public async Task HandleEmojiTextCommand([MinLength(1), MaxLength(1000)] string message)
        {
            await DeferAsync();
            EmojiText emoji = new EmojiText();
            await FollowupAsync(emoji.GetEmojiString(message));
        }

        [SlashCommand("8ball", "Shake the figurative eight ball.")]
        [EnabledInDm(true)]
        public async Task HandleEightBallCommand([MinLength(1), MaxLength(4000)] string message)
        {
            await DeferAsync();
            Random r = new Random();
            EightBall eight = new EightBall();
            List<EightBall> list = new List<EightBall>();
            list = eight.GetEightBall(Constants.Constants.discordBotConnStr);
            int i = r.Next(1, list.Count + 1);

            string title = "BigBirdBot - 8ball";
            string desc = $"{list.Where(s => s.ID == i).Select(s => s.Saying).FirstOrDefault()}";
            string thumbnailUrl = "https://www.nicepng.com/png/detail/568-5689463_magic-8-ball-eight-ball-billiard-balls-billiards.png";
            string createdBy = "Command from: " + Context.User.Username;

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Discord.Color.Green).Build());
        }

        [SlashCommand("ascii", "Turn words into ascii.")]
        [EnabledInDm(true)]
        public async Task HandleAscii([MinLength(1), MaxLength(4000)] string message)
        {
            await DeferAsync();
            await FollowupAsync($"```{FiggleFonts.Standard.Render(message.Trim())}```");
        }

        [SlashCommand("delete", "Removes messages from the chat.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleDelete([MinValue(1), MaxValue(20)] int numToDelete)
        {
            var channel = Context.Channel as SocketTextChannel;
            var messages = await channel.GetMessagesAsync(numToDelete + 1).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);
        }

        [SlashCommand("poll", "Create a poll for people to vote on.")]
        [EnabledInDm(true)]
        public async Task HandlePoll([MinLength(1), MaxLength(4000)] string statement, [MinLength(1)] string pollAnswer1, [MinLength(1)] string pollAnswer2, string pollAnswer3 = null, string pollAnswer4 = null, string pollAnswer5 = null, string pollAnswer6 = null, string pollAnswer7 = null, string pollAnswer8 = null, string pollAnswer9 = null, string pollAnswer10 = null)
        {
            await DeferAsync();
            List<Emoji> emojis = new List<Emoji>
            {
                new Emoji("1️⃣"),
                new Emoji("2️⃣"),
                new Emoji("3️⃣"),
                new Emoji("4️⃣"),
                new Emoji("5️⃣"),
                new Emoji("6️⃣"),
                new Emoji("7️⃣"),
                new Emoji("8️⃣"),
                new Emoji("9️⃣"),
                new Emoji("🔟")
            };
            List<string> items = new List<string>() { pollAnswer1, pollAnswer2, pollAnswer3, pollAnswer4, pollAnswer5, pollAnswer6, pollAnswer7, pollAnswer8, pollAnswer9, pollAnswer10 };
            items = items.Where(s => !string.IsNullOrEmpty(s)).Select(s => s.Trim()).ToList();
            EmbedHelper embed = new EmbedHelper();
            string title = "BigBirdBot - Poll";
            string desc = $"Poll Item: **{statement.Trim()}**\n\nChoices:";
            string createdByMsg = "Command from: " + Context.User.Username;

            for (int i = 0; i < items.Count; i++)
                desc += "\n" + i.ToString() + ". **" + items[i] + "**";

            IUserMessage msg = await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, "", createdByMsg, Discord.Color.Blue).Build());

            for (int i = 0; i < items.Count; i++)
                await msg.AddReactionAsync(emojis[i]);
        }

        [SlashCommand("addbirthday", "Adds a role members birthday to celebrate.")]
        [EnabledInDm(false)]
        public async Task HandleBirthday(SocketGuildUser user, DateTime birthday)
        {
            await DeferAsync();
            StoredProcedure storedProcedure = new StoredProcedure();
            try
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var serverId = Int64.Parse(Context.Guild.Id.ToString());
                var guild = Context.Client.GetGuild(ulong.Parse(serverId.ToString()));

                if (guild.Roles.Where(s => s.Name.Contains("birthday")).Count() == 0)
                {
                    // Create the birthday role and add all the users in the server
                    await guild.CreateRoleAsync("birthday", null, Discord.Color.Purple, false, true, null);

                    var embed = new EmbedBuilder
                    {
                        Title = "BigBirdBot - Birthday",
                        Color = Color.Gold,
                        Description = "A **birthday** role was created, please have an administrator add the users to this role before running this command again."
                    };

                    await FollowupAsync(embed: embed.Build());

                    return;
                }

                storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBirthday", new List<SqlParameter>
                {
                    new SqlParameter("@BirthdayDate", birthday),
                    new SqlParameter("@BirthdayUser", user.Mention),
                    new SqlParameter("@BirthdayGuild", Context.Guild.Id.ToString())
                });

                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Birthday Added", $"{user.DisplayName} birthday was added to the bot.", "", Context.User.Username, Discord.Color.Blue).Build());
                //DataTable dtNewEvent = storedProcedure.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                //{
                //    new SqlParameter("@EventDateTime", birthday),
                //    new SqlParameter("@EventName", user.DisplayName + " Birthday"),
                //    new SqlParameter("@EventDescription", "Happy Birthday to " + user.DisplayName),
                //    new SqlParameter("@EventChannelSource", Context.Channel.Id.ToString()),
                //    new SqlParameter("@CreatedBy", guild.Roles.Where(s => s.Name.Contains("birthday")).Select(s => s.Mention).FirstOrDefault())
                //});

                //foreach (DataRow dr in dtNewEvent.Rows)
                //{
                //    var embed = new EmbedBuilder
                //    {
                //        Title = ":calendar_spiral: BigBirdBot - Birthday - " + dr["EventName"].ToString(),
                //        Color = Color.Gold
                //    };
                //    embed
                //        .AddField("Time", dr["eventDateTime"].ToString())
                //        .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                //        .WithCurrentTimestamp();
                //    await FollowupAsync(embed: embed.Build());
                //}
            }
            catch (Exception e)
            {
                EmbedHelper embed = new EmbedHelper();
                string title = "BigBirdBot - Birthday Error";
                string desc = e.Message;
                string createdByMsg = "Command from: " + Context.User.Username;
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, Constants.Constants.errorImageUrl, createdByMsg, Color.Red).Build());
            }
        }

        [SlashCommand("avatar", "See you or someone else's avatar in high quality.")]
        [EnabledInDm(true)]
        public async Task HandleAvatarCommand(SocketGuildUser user = null)
        {
            await DeferAsync();
            try
            {
                if (user == null)
                    user = Context.User as SocketGuildUser;

                var embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot - {user.Username}'s Avatar",
                    Color = Color.Blue,
                    ImageUrl = user.GetDisplayAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl()
                };
                embed
                    .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                    .WithCurrentTimestamp();
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("reportbug", "Found an issue with the bot?  Report it here, please.")]
        [EnabledInDm(true)]
        public async Task HandleBugReport([MinLength(1), MaxLength(4000)] string bugFound)
        {
            ulong guildId = ulong.Parse("880569055856185354");
            ulong textChannelId = ulong.Parse("1156625507840954369");
            await Context.Client.GetGuild(guildId).GetTextChannel(textChannelId).SendMessageAsync($"**Bug Report from {Context.User.Username} in {Context.Guild.Name}**: \n" + bugFound);
            await ReplyAsync("Bug report submitted.");
        }
    }
}
