using Discord;
using Discord.Commands;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System.Data;
using DiscordBot.Helper;

namespace DiscordBot.Modules
{
    public class EventCommands : ModuleBase<SocketCommandContext>
    {
        [Command("event")]
        [Alias("eve", "plan", "remind")]
        [Discord.Commands.Summary("Plan an event and get a notification when it's ready.  In 'Date/Time, Event Name, Event Description' format.")]
        public async Task HandleEventCommand([Remainder] string eventMsg)
        {
            try
            {
                StoredProcedure storedProcedure = new StoredProcedure();
                EmbedHelper embedHelper = new EmbedHelper();

                string mention = "";
                if (Context.Message.MentionedRoles.Count > 0)
                    mention = Context.Message.MentionedRoles.Select(s => s.Mention).FirstOrDefault();
                else if (Context.Message.MentionedUsers.Count > 0)
                    mention = Context.Message.MentionedUsers.Select(s => s.Mention).FirstOrDefault();
                else
                    mention = Context.User.Mention;

                DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                {
                    new SqlParameter("@EventDetails", eventMsg),
                    new SqlParameter("@EventChannelSource", Context.Channel.Id.ToString()),
                    new SqlParameter("@CreatedBy", mention)
                });

                if (dt.Rows.Count > 0 )
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Event Created: **" + dr["EventName"].ToString() + "**", $"Date: {dr["EventDateTime"]}\n{dr["EventDescription"]}", "", "", Discord.Color.Blue, null, null);
                        await ReplyAsync(embed: embed.Build());
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("deleteevent")]
        [Alias("delevent", "deleve")]
        public async Task HandleDeleteEvent([Remainder] string eventName)
        {
            StoredProcedure storedProcedure = new StoredProcedure();
            EmbedHelper embedHelper = new EmbedHelper();

            if (eventName.Trim().Length > 0)
            {
                try
                {
                    DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "DeleteEventByName", new List<SqlParameter>
                    {
                        new SqlParameter("@EventName", eventName.Trim())
                    });

                    if (dt.Rows.Count > 0)
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Event Deleted Successfully.", "", "", "", Discord.Color.Blue, null, null);
                        await ReplyAsync(embed: embed.Build());
                    }
                    else
                    {
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Event does not exist by the name entered.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                        await ReplyAsync(embed: embed.Build());
                    }
                }
                catch (Exception e)
                {
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }
            else
            {
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a valid event name", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
