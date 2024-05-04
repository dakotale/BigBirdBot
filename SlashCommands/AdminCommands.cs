using Discord.Commands;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;

namespace DiscordBot.SlashCommands
{
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("announcement", "ONLY THE BOT OWNER CAN RUN THIS - Broadcast a message to all server.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleAnnouncement([Remainder] string message)
        {
            await DeferAsync();
            try
            {
                StoredProcedure stored = new StoredProcedure();

                // GetServer ulong IDs
                // var test = Context.Client.GetGuild(id).Users.Where(s => s.IsBot == false).ToList();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServersNonNullDefaultChannel", new List<SqlParameter>());
                EmbedHelper embedHelper = new EmbedHelper();
                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        await Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).GetTextChannel(ulong.Parse(dr["DefaultChannelID"].ToString())).SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Announcement", message, "", "BigBirdBot", Discord.Color.Gold).Build());
                    }
                }

                await FollowupAsync("Announcement sent.");
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Discord.Color.Red, "");
                await FollowupAsync(embed: embed.Build());
            }
        }
    }
}
