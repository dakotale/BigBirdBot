using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;
using System.Data;
using Discord.Net.Extensions.Interactions;

namespace DiscordBot.SlashCommands
{
    [GuildModule(1057033598940745728)]
    public class GuildSpecific : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("fireapologized", "Fire continuing the apology arc")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleApology(int? additionalCount = null)
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = new DataTable();

            if (additionalCount > 1)
                for (int i = 0; i < additionalCount; i++)
                    dt = stored.Select(connStr, "AddFireApologized", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });
            else
                dt = stored.Select(connStr, "AddFireApologized", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });

            string counterHistory = "Here are the most recent times *fire apologized*.\n";
            string currentCounter = "";
            string currentDateTime = "";
            string averagePerDay = "";

            foreach (DataRow dr in dt.Rows)
            {
                currentCounter = dr["CurrentCounter"].ToString();
                currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET");
                counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET")}\n";
                averagePerDay = dr["AveragePerDay"].ToString();
            }

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Fire Apologized", $"**The fire apology counter was updated to {currentCounter} on {currentDateTime}**\n**Average Per Day: {averagePerDay}**\n{counterHistory}", "", Context.User.Username, Discord.Color.Green).Build());
        }

        [SlashCommand("brendancounter", "Low level content")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleLowLevel(int? additionalCount = null)
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = new DataTable();

            if (additionalCount > 1)
                for (int i = 0; i < additionalCount; i++)
                    dt = stored.Select(connStr, "AddLowLevel", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });
            else
                dt = stored.Select(connStr, "AddLowLevel", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });

            string counterHistory = "Here are the most recent times Brendan let everyone know about the disdain towards low level content or Dark Knight in the critically acclaimed game Final Fantasy 14 Online.\n";
            string currentCounter = "";
            string currentDateTime = "";

            foreach (DataRow dr in dt.Rows)
            {
                currentCounter = dr["CurrentCounter"].ToString();
                currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET");
                counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET")}\n";
            }

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Low Level", $"**The low level counter was updated to {currentCounter} on {currentDateTime}**\n{counterHistory}", "", Context.User.Username, Discord.Color.Green).Build());
        }

        [SlashCommand("kaylacounter", "Being normal for 5 seconds")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleNormal(int? additionalCount = null)
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();
            DataTable dt = new DataTable();

            if (additionalCount > 1)
                for (int i = 0; i < additionalCount; i++)
                    dt = stored.Select(connStr, "AddKaylaNormal", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });
            else
                dt = stored.Select(connStr, "AddKaylaNormal", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username) });

            string counterHistory = "Here are the most recent times Kayla was not normal for 5 seconds.\n";
            string currentCounter = "";
            string currentDateTime = "";

            foreach (DataRow dr in dt.Rows)
            {
                currentCounter = dr["CurrentCounter"].ToString();
                currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET");
                counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET")}\n";
            }

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Normal", $"**The not-normal counter was updated to {currentCounter} on {currentDateTime}**\n{counterHistory}", "", Context.User.Username, Discord.Color.Green).Build());
        }

        [SlashCommand("burncounter", "Bad speller and awesome")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleBurn(int? additionalCount = null)
        {
            await DeferAsync();
            string connStr = Constants.Constants.discordBotConnStr;
            StoredProcedure stored = new StoredProcedure();

            DataTable dt = new DataTable();

            if (additionalCount > 1)
                for(int i = 0; i < additionalCount; i++)
                    dt = stored.Select(connStr, "AddBurnNormal", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username)});
            else
                dt = stored.Select(connStr, "AddBurnNormal", new List<SqlParameter> { new SqlParameter("@CreatedBy", Context.User.Username)});

            string counterHistory = "Here are the most recent times Burn was a bad speller and awesome.\n";
            string currentCounter = "";
            string currentDateTime = "";
            string averagePerDay = "";

            foreach (DataRow dr in dt.Rows)
            {
                currentCounter = dr["CurrentCounter"].ToString();
                currentDateTime = DateTime.Parse(dr["CurrentDateTime"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET");
                counterHistory += $"{dr["UpdatedCounter"].ToString()} - {DateTime.Parse(dr["TimeStamp"].ToString()).ToString("MM/dd/yyyy HH:mm:ss ET")}\n";
                averagePerDay = dr["AveragePerDay"].ToString();
            }

            EmbedHelper embed = new EmbedHelper();
            await FollowupAsync(embed: embed.BuildMessageEmbed("BigBirdBot - Burn", $"**The Burn is awesome and a bad speller counter was updated to {currentCounter} on {currentDateTime}**\n**Average Per Day: {averagePerDay}**\n{counterHistory}", "", Context.User.Username, Discord.Color.Green).Build());
        }
    }
}
