using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using KillersLibrary.Services;
using System.Data;
using System.Numerics;

namespace DiscordBot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class IntroModule : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();
        public EmbedPagesService EmbedPagesService { get; set; }
        public MultiButtonsService MultiButtonsService { get; set; }

        // Ban a user
        [Command("ban")]
        [RequireContext(ContextType.Guild)]
        // make sure the user invoking the command can ban
        [RequireUserPermission(GuildPermission.BanMembers)]
        // make sure the bot itself can ban
        [RequireBotPermission(GuildPermission.BanMembers)]
        [Discord.Commands.Summary("Bans a user but the bot must have the permission in order to do it.")]
        public async Task BanUserAsync(IGuildUser user, [Remainder] string reason = null)
        {
            audit.InsertAudit("ban", Context.User.Username, Constants.Constants.discordBotConnStr);
            await user.Guild.AddBanAsync(user, reason: reason);
            await ReplyAsync("ok!");
        }

        [Command("help")]
        [Discord.Commands.Summary("Get a list of commands and descriptions available to the bot.")]
        public async Task TaskHelpCommand()
        {
            audit.InsertAudit("help", Context.User.Username, Constants.Constants.discordBotConnStr);
            List<EmbedBuilder> list = new();
            EmbedBuilder embedBuilder = new();

            EmbedPagesStyles style = new();
            style.FirstLabel = "«";
            style.BackLabel = "‹";
            style.DeletionEmoji = "🗑";
            style.ForwardLabel = "›";
            style.LastLabel = "»";
            style.BtnColor = ButtonStyle.Primary;
            style.DeletionBtnColor = ButtonStyle.Danger;
            style.SkipBtnColor = ButtonStyle.Primary;
            style.FastChangeBtns = false; // Do you want there to be a button that goes directly to either ends?
            style.PageNumbers = true; //Do you want the embed to have page numbers like "Page: 1/4"? Depends on how many pages you have.

            StoredProcedure storedProcedure = new StoredProcedure();
            DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "GetCommandList", new List<System.Data.SqlClient.SqlParameter>());
            EmbedBuilder embed = new EmbedBuilder();

            int i = 0;
            string helpCommand = "";
            foreach (DataRow dr in dt.Rows)
            {
                i++;
                if (i % 20 == 0)
                {
                    embedBuilder = new();
                    embedBuilder.WithTitle($"BigBirdBot - Help");
                    embedBuilder.WithDescription(helpCommand);
                    embedBuilder.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username);
                    embedBuilder.Color = Discord.Color.Red;
                    embedBuilder.WithCurrentTimestamp();
                    list.Add(embedBuilder);
                }
                helpCommand += "**"+dr["CommandName"].ToString() + "**" + "\n*Alias:* " + dr["CommandAliases"].ToString() + "\n" + dr["CommandDescription"].ToString() + "\n\n";
            }

            embedBuilder = new();
            embedBuilder.WithTitle($"BigBirdBot - Help");
            embedBuilder.WithDescription(helpCommand);
            embedBuilder.WithFooter(footer => footer.Text = "Command from: " + Context.User.Username);
            embedBuilder.Color = Discord.Color.Red;
            embedBuilder.WithCurrentTimestamp();
            list.Add(embedBuilder);

            try
            {
                await EmbedPagesService.CreateEmbedPages(Context.Client, list, null, Context, null, style);
            }
            catch (Exception ex)
            {
                /* Eat it */
            }
        }
    }
}
