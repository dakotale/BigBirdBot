using System.Data;
using Microsoft.Data.SqlClient;
using Discord;
using Discord.Interactions;
using Discord.Net.Extensions.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    /// <summary>Bot-owner-only maintenance commands: cross-server announcements, schedule/connection listings, user table backfill, and manual keyword-image cleanup.</summary>
    // GuildModule decoration limits these commands to only show by the guild below.
    [GuildModule(880569055856185354)]
    public class OwnerCommands(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
    {
        /// <summary>Broadcasts a message (with optional attachment) to every server's default channel where the bot has permission to post, reporting which servers were skipped.</summary>
        [SlashCommand("announcement", "Broadcast a message to all servers.")]
        [CommandContextType(InteractionContextType.Guild)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleAnnouncement([MinValue(1), MaxLength(4000)] string message, Attachment attachment = null)
        {
            await DeferAsync(ephemeral: true);
            List<string> serverList = new List<string>();
            List<string> serverListNoPerms = new List<string>();
            try
            {
                StoredProcedure stored = new StoredProcedure();
                string imageUrl = "";

                if (attachment != null)
                    imageUrl = attachment.Url;

                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServers", new List<SqlParameter>());
                EmbedHelper embedHelper = new EmbedHelper();
                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        SocketGuild guild = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString()));
                        SocketTextChannel textChannel = guild.GetTextChannel(ulong.Parse(dr["DefaultChannelID"].ToString()));
                        if (textChannel != null)
                        {
                            IUser bot = guild.Users.Where(s => s.IsBot && s.Username.Contains("BigBirdBot")).FirstOrDefault();
                            if (bot != null)
                            {
                                SocketGuildUser? user = textChannel.Users.Where(s => s.Id == bot.Id).FirstOrDefault();
                                if (user != null)
                                {
                                    ChannelPermissions permissions = user.GetPermissions(textChannel);
                                    if (permissions.SendMessages)
                                    {
                                        serverList.Add(guild.Name);
                                        await textChannel.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("Announcement", message, "", "BigBirdBot", Discord.Color.Gold, imageUrl).Build()).ConfigureAwait(false);
                                    }
                                    else
                                        serverListNoPerms.Add(guild.Name);
                                }
                            }
                        }
                    }
                }
                string delimiter = ", ";
                string result = string.Join(delimiter, serverList);
                await FollowupAsync($"Announcement sent to **{result}**.\nNot Sent: {string.Join(delimiter, serverListNoPerms)}", ephemeral: true).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("", e.Message, Context.User.Username).Build());
            }
        }

        /// <summary>Lists every user with a scheduled keyword delivery, along with their table and scheduled time.</summary>
        [SlashCommand("schedulelist", "Get list of all users scheduled times.")]
        [CommandContextType(InteractionContextType.Guild)]
        [Discord.Interactions.RequireOwner]
        public async Task HandleServerList()
        {
            await DeferAsync(ephemeral: true);

            var events = await keywords.GetAllScheduledEventUsersAsync();
            EmbedHelper embedHelper = new EmbedHelper();
            string description = "";

            foreach (var ev in events)
                description += "- " + ev.Username + " - " + ev.Keyword + " - " + ev.ScheduledFor.ToString("MM/dd hh:mm tt") + "\n";

            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("Scheduled List", description, "", Context.User.Username, Discord.Color.Blue).Build(), ephemeral: true).ConfigureAwait(false);
        }

        /// <summary>Lists every server where the music player is currently connected to a voice channel.</summary>
        [SlashCommand("connplayers", "List of all connected players in voice channels.")]
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePlayersConnected()
        {
            await DeferAsync(ephemeral: true);
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetPlayerConnected", new List<SqlParameter>());
            EmbedHelper embed = new EmbedHelper();

            string title = "Players Connected";
            string desc = "";
            string thumbnailUrl = "";
            string imageUrl = "";
            string embedCreatedBy = "Command from: " + Context.User.Username;

            if (dt.Rows.Count > 0)
            {
                desc = $"Total Players Connected: {dt.Rows.Count}\n";
                foreach (DataRow dr in dt.Rows)
                {
                    desc += "\n- " + dr["ServerName"];
                }
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build(), ephemeral: true);
            }
            else
            {
                desc = "No Players are connected at this time.";
                await FollowupAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, embedCreatedBy, Discord.Color.Blue, imageUrl).Build(), ephemeral: true);
            }
        }

        /// <summary>Backfills the user table with every non-bot member of every known server — a manual re-sync tool for when the DB falls out of date.</summary>
        [SlashCommand("populateallusers", "Populate users into the DB.")]
        [CommandContextType(InteractionContextType.Guild)]
        [Discord.Interactions.RequireOwner]
        public async Task HandlePopulateAllUserCommand()
        {
            await DeferAsync(ephemeral: true);
            try
            {
                StoredProcedure stored = new StoredProcedure();

                // GetServer ulong IDs
                // var test = Context.Client.GetGuild(id).Users.Where(s => s.IsBot == false).ToList();
                DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetServers", new List<SqlParameter>());

                foreach (DataRow dr in dt.Rows)
                {
                    // Need to check if Guild exists
                    if (Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())) != null)
                    {
                        List<SocketGuildUser> users = Context.Client.GetGuild(ulong.Parse(dr["ServerUID"].ToString())).Users.Where(s => s.IsBot == false && s.IsWebhook == false).ToList() ?? new List<SocketGuildUser>();
                        if (users.Count > 0)
                        {
                            foreach (SocketGuildUser? u in users)
                            {
                                stored.UpdateCreate(Constants.Constants.discordBotConnStr, "AddUser", new List<SqlParameter>
                                {
                                    new SqlParameter("@UserID", u.Id.ToString()),
                                    new SqlParameter("@Username", u.Username),
                                    new SqlParameter("@JoinDate", u.JoinedAt),
                                    new SqlParameter("@ServerUID", Int64.Parse(u.Guild.Id.ToString())),
                                    new SqlParameter("@Nickname", u.Nickname)
                                });
                            }
                        }
                    }
                }

                await FollowupAsync("User table updated.", ephemeral: true);
            }
            catch (Exception e)
            {
                EmbedHelper embedHelper = new EmbedHelper();
                await FollowupAsync(embed: embedHelper.BuildErrorEmbed("", e.Message, Context.User.Username).Build());
            }
        }

        /// <summary>Removes one specific image file's DB entry from a keyword (companion to Keyword.UrlCommands.HandleDeleteAsync for local files).</summary>
        [SlashCommand("delmultiimage", "Deletes a multi-keyword image with a given path")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireOwner]
        public async Task HandleThirstImageDelete([MinLength(1)] string fileName, [MinLength(1)] string chatName)
        {
            await DeferAsync(ephemeral: true);

            EmbedHelper embedHelper = new EmbedHelper();
            string tableName = chatName.Trim();
            fileName = @"C:\Temp\DiscordBot\" + tableName + @"\" + fileName.Trim();

            await keywords.DeleteEntryAsync(fileName, tableName);

            EmbedBuilder embed = embedHelper.BuildMessageEmbed("Delete Successful", $"Image {fileName} was successfully deleted from the {tableName} table.", "", Context.User.Username, Color.Blue, "");
            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}

