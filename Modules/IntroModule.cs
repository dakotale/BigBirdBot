using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using DiscordBot.Helper;
using KillersLibrary.Services;
using System.Data;
using System.Numerics;
using System.Runtime.InteropServices;

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
            audit.InsertAudit("ban", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());
            await user.Guild.AddBanAsync(user, reason: reason);
            await ReplyAsync("ok!");
        }

        [Command("help")]
        [Discord.Commands.Summary("Get a list of commands and descriptions available to the bot.")]
        public async Task TaskHelpCommand()
        {
            audit.InsertAudit("help", Context.User.Username, Constants.Constants.discordBotConnStr, Context.Guild.Id.ToString());

            StoredProcedure storedProcedure = new StoredProcedure();
            EmbedHelper helper = new EmbedHelper();
            DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "GetCommandList", new List<System.Data.SqlClient.SqlParameter>());
            string output = "";

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    output += $"**{dr["CommandName"].ToString()} ({dr["CommandAliases"]})\nDescription:** {dr["CommandDescription"].ToString()}\n\n";
                }
            }

            await ReplyAsync(embed: helper.BuildMessageEmbed("BigBirdBot - Help Commands", output, "", "BigBirdBot", Discord.Color.Gold, null, null).Build());
        }
    }
}
