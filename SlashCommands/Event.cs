using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;
using Discord.WebSocket;

namespace DiscordBot.SlashCommands
{
    public class Event : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("event", "Create an event with a notification in chat.")]
        [EnabledInDm(false)]
        public async Task HandleEvent([MinLength(1), MaxLength(255)] string eventName, [MinLength(1), MaxLength(500)] string eventDescription, [MinValue(1)] int reminderTimeInMinutes, DateTime eventDateTime, SocketGuildUser user = null, SocketRole role = null)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();
            try
            {
                string createdBy = "";
                if (user != null)
                {
                    createdBy = user.Mention;
                }
                if (role != null)
                {
                    createdBy = role.Mention;
                }

                if (user == null && role == null)
                {
                    createdBy = Context.User.Mention;
                }

                StoredProcedure stored = new StoredProcedure();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                {
                    new SqlParameter("@EventName", eventName.Trim()),
                    new SqlParameter("@EventDescription", eventDescription.Trim()),
                    new SqlParameter("@EventDateTime", eventDateTime),
                    new SqlParameter("@EventReminderTime", reminderTimeInMinutes),
                    new SqlParameter("@EventChannelSource", Context.Channel.Id.ToString()),
                    new SqlParameter("@CreatedBy", createdBy.Trim())
                });

                foreach (DataRow dr in dt.Rows)
                {
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - " + dr["EventName"].ToString(), "Description: " + eventDescription + "\nDate: " + dr["EventDateTime"].ToString(), "", Context.User.Username, Discord.Color.Blue).Build());
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Error", ex.Message, Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Red).Build());
            }
        }

        [SlashCommand("delevent", "Delete an event that was created.")]
        [EnabledInDm(false)]
        [Discord.Interactions.RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        public async Task HandleDeleteEvent([MinLength(1), MaxLength(255)] string eventName)
        {
            await DeferAsync();
            EmbedHelper embedHelper = new EmbedHelper();
            StoredProcedure stored = new StoredProcedure();
            
            try
            {
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "DeleteEventByName", new List<SqlParameter>
                {
                    new SqlParameter("@EventName", eventName.Trim())
                });

                if (dt.Rows.Count > 0)
                {
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Event Deleted", $"{eventName} was deleted successfully.", Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Blue).Build());
                }
                else
                {
                    await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The event entered does not exist.", Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Red).Build());
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Error", ex.Message, Constants.Constants.errorImageUrl, Context.User.Username, Discord.Color.Red).Build());
            }
        }
    }
}
