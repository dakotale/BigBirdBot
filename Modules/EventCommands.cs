using Discord;
using Discord.Commands;
using Discord.Interactions;
using DiscordBot.Constants;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using DiscordBot.Helper;

namespace DiscordBot.Modules
{
    public class EventCommands : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();
        [Command("event")]
        [Alias("eve", "plan", "remind")]
        [Discord.Commands.Summary("Plan an event and get a notification when it's ready.  In 'Date/Time, Event Name' format.")]
        public async Task HandleEventCommand([Remainder] string eventMsg)
        {
            audit.InsertAudit("event", Context.User.Username, Constants.Constants.discordBotConnStr);

            /*
             * Objects
             * 0 - Date/Time
             * 1 - Event Name
             */
            try
            {
                StoredProcedure storedProcedure = new StoredProcedure();
                string[] objects = eventMsg.Split(',');
                if (objects.Length >= 2)
                {
                    string eventTitle = objects[1];
                    //string eventDescription = objects[2];
                    DateTime eventDateTime;

                    eventTitle = eventTitle.Trim();
                    //eventDescription = eventDescription.Trim();

                    var test = objects[0];
                    bool hasAmPm = objects[0].Contains("PM", StringComparison.OrdinalIgnoreCase) || objects[0].Contains("AM", StringComparison.OrdinalIgnoreCase);
                    bool hasColon = objects[0].Contains(':');
                    bool hasDateCharacters = objects[0].Contains('/') || objects[0].Contains('-') || objects[0].Contains('.');

                    if (hasAmPm || hasColon)
                    {
                        // This is a valid time
                        // Lets check for a date, if no date then it's assumed to today
                        if (hasDateCharacters)
                        {
                            // There is a date
                            eventDateTime = DateTime.Parse(objects[0].Trim());
                        }
                        else
                        {
                            // Using todays date, it's not valid
                            eventDateTime = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd ") + objects[0].Trim());
                        }

                        if (eventDateTime < DateTime.Now)
                        {
                            EmbedHelper embedHelper = new EmbedHelper();
                            string dateTime = DateTime.Now.ToString("MM/dd/yyyy hh:mm:tt");
                            var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", $"You scheduled an event in the past, the bot is based on Eastern Time.  The current time in EST is {dateTime}", Constants.Constants.errorImageUrl, "", Color.Red, "");
                            await ReplyAsync(embed: embed.Build());
                        }
                        else
                        {
                            // Select the existing event if it exists so we don't create duplicate events with multiple unique Ids
                            DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "CheckIfEventExists", new List<SqlParameter>
                            {
                                new SqlParameter("EventDateTime", eventDateTime),
                                new SqlParameter("CreatedBy", Context.User.Mention)
                            });

                            DataTable dtNewEvent = storedProcedure.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                            {
                                new SqlParameter("@EventDateTime", eventDateTime),
                                new SqlParameter("@EventName", eventTitle),
                                new SqlParameter("@EventDescription", ""),
                                new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(eventDateTime, TimeZoneInfo.Local)),
                                new SqlParameter("@EventChannelSource", Context.Message.Channel.Id.ToString()),
                                new SqlParameter("@CreatedBy", Context.User.Mention)
                            });

                            foreach (DataRow dr in dtNewEvent.Rows)
                            {
                                // Defaulting to 15 minutes reminder
                                storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddEventReminder", new List<SqlParameter>
                                {
                                    new SqlParameter("@EventID", int.Parse(dr["EventID"].ToString())),
                                    new SqlParameter("@EventDateTime", eventDateTime),
                                    new SqlParameter("@EventName", eventTitle),
                                    new SqlParameter("@EventDescription", ""),
                                    new SqlParameter("@EventReminderTime", 15),
                                    new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(eventDateTime, TimeZoneInfo.Local)),
                                    new SqlParameter("@CreatedBy", Context.User.Mention)
                                });

                                var embed = new EmbedBuilder
                                {
                                    Title = ":calendar_spiral: BigBirdBot - Event - " + dr["EventName"].ToString(),
                                    Color = Color.Gold
                                };
                                embed
                                    .AddField("Time", dr["eventDateTime"].ToString())
                                    .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                                    .WithCurrentTimestamp();
                                await ReplyAsync(embed: embed.Build());
                            }
                        }
                    }
                    else if (!hasAmPm || hasColon)
                    {
                        // This is a valid time
                        // Lets check for a date, if no date then it's assumed to today
                        if (hasDateCharacters)
                        {
                            // There is a date
                            eventDateTime = DateTime.Parse(objects[0].Trim());
                        }
                        else
                        {
                            // Using todays date, it's not valid
                            eventDateTime = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd ") + objects[0].Trim());
                        }
                        if (eventDateTime < DateTime.Now)
                        {
                            // Date is not valid, can't schedule an event in the past
                            EmbedHelper embedHelper = new EmbedHelper();
                            string dateTime = DateTime.Now.ToString("MM/dd/yyyy hh:mm:tt");
                            var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", $"You scheduled an event in the past, the bot is based on Eastern Time.  The current time in EST is {dateTime}", Constants.Constants.errorImageUrl, "", Color.Red, "");
                            await ReplyAsync(embed: embed.Build());
                        }
                        else
                        {
                            // Select the existing event if it exists so we don't create duplicate events with multiple unique Ids
                            DataTable dt = storedProcedure.Select(Constants.Constants.discordBotConnStr, "CheckIfEventExists", new List<SqlParameter>
                            {
                                new SqlParameter("EventDateTime", eventDateTime),
                                new SqlParameter("CreatedBy", Context.User.Mention)
                            });

                            DataTable dtNewEvent = storedProcedure.Select(Constants.Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                            {
                                new SqlParameter("@EventDateTime", eventDateTime),
                                new SqlParameter("@EventName", eventTitle),
                                new SqlParameter("@EventDescription", ""),
                                new SqlParameter("@EventUserUTCDate", eventDateTime.AddHours(4)),
                                new SqlParameter("@EventChannelSource", Context.Message.Channel.Id.ToString()),
                                new SqlParameter("@CreatedBy", Context.User.Mention)
                            });

                            foreach (DataRow dr in dtNewEvent.Rows)
                            {
                                // Defaulting to 15 minutes reminder
                                storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddEventReminder", new List<SqlParameter>
                                {
                                    new SqlParameter("@EventID", int.Parse(dr["EventID"].ToString())),
                                    new SqlParameter("@EventDateTime", eventDateTime),
                                    new SqlParameter("@EventName", eventTitle),
                                    new SqlParameter("@EventDescription", ""),
                                    new SqlParameter("@EventReminderTime", 15),
                                    new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(eventDateTime, TimeZoneInfo.Local)),
                                    new SqlParameter("@CreatedBy", Context.User.Mention)
                                });

                                var embed = new EmbedBuilder
                                {
                                    Title = ":calendar_spiral: BigBirdBot - Reminder/Event - " + dr["EventName"].ToString(),
                                    Color = Color.Gold
                                };
                                embed
                                    .AddField("Time", dr["eventDateTime"].ToString())
                                    .WithFooter(footer => footer.Text = "Created by " + Context.User.Username)
                                    .WithCurrentTimestamp();
                                await ReplyAsync(embed: embed.Build());
                            }
                        }
                    }
                    else
                    {
                        // This is not a valid time
                        EmbedHelper embedHelper = new EmbedHelper();
                        string dateTime = DateTime.Now.ToString("MM/dd/yyyy hh:mm:tt");
                        string msg = $"{Context.User.Mention}, the date/time entered was not in valid format.  Example: {dateTime}";
                        var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", msg, Constants.Constants.errorImageUrl, "", Color.Red, "");
                        await ReplyAsync(embed: embed.Build());
                        await ReplyAsync(Context.User.Mention + " , the date/time entered was not in valid format.  Example: " + DateTime.Now.ToString("MM/dd/yyyy hh:mm:tt"));
                    }
                }
                else
                {
                    EmbedHelper embedHelper = new EmbedHelper();
                    var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a date/time and name.  Example for today -> 8PM, Movie", Constants.Constants.errorImageUrl, "", Color.Red, "");
                    await ReplyAsync(embed: embed.Build());
                }
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
