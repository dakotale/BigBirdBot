//using Discord.Interactions;
//using DiscordBot.Constants;
//using DiscordBot.Helper;
//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DiscordBot.SlashCommands
//{
//    public class Budget : InteractionModuleBase<SocketInteractionContext>
//    {
//        [SupportGuildOnly(new ulong[] { 880569055856185354 })]
//        [SlashCommand("addcost", "Can only be ran by Bot Owner :)")]
//        [EnabledInDm(true)]
//        [Discord.Interactions.RequireOwner]
//        public async Task AddCost([MinLength(1), MaxLength(250)] string costName, [MinValue(0.00)] decimal costValue, 
//                [Choice("Doordash", "Doordash"), Choice("Restaurant", "Restaurant"), Choice("Amazon", "Amazon"), Choice("Bot Cost", "OVH Cloud"), 
//                Choice("Groceries", "Groceries"), Choice("Steam", "Steam"), Choice("Riot Games", "Riot Games"), Choice("Mogstation", "Mogstation"),
//                Choice("Mental Health", "Mental Health"), Choice("Patreon", "Patreon"), Choice("Gas", "Gas"), Choice("Entertainment", "Entertainment"),
//                Choice("Subscription", "Subscription"), Choice("Miscellanous", "Miscellanous"), Choice("Mortgage", "Mortgage"), Choice("HOA", "HOA")] string costCategory)
//        {
//            await DeferAsync();
//            costName = costName.Trim();

//            EmbedHelper embedHelper = new EmbedHelper();
//            StoredProcedure stored = new StoredProcedure();
//            string connStr = Constants.Constants.discordBotConnStr;
//            string description = "";
//            decimal totalCost = 0.0m;

//            DataTable dt = stored.Select(connStr, "AddBudget", new List<System.Data.SqlClient.SqlParameter>
//            {
//                new System.Data.SqlClient.SqlParameter("@CostName", costName),
//                new System.Data.SqlClient.SqlParameter("@CostValue", costValue),
//                new System.Data.SqlClient.SqlParameter("@BudgetCategory", costCategory)
//            });

//            foreach (DataRow dr in dt.Rows)
//            {
//                totalCost += decimal.Parse(dr["Cost"].ToString());
//                description += dr["BudgetCategory"] + " - $" + decimal.Parse(dr["Cost"].ToString()) + "\n";
//            }

//            description += "\n**Total Cost for the Month: $" + totalCost.ToString() + "**";

//            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Budget Item Added", description, "", Context.User.Username, Discord.Color.Blue).Build());
//        }
//        [SupportGuildOnly(new ulong[] { 880569055856185354 })]
//        [SlashCommand("editcost", "Can only be ran by Bot Owner :)")]
//        [EnabledInDm(true)]
//        [Discord.Interactions.RequireOwner]
//        public async Task ModifyCost([MinLength(1), MaxLength(250)] string costName, [MinValue(0.00)] decimal costValue)
//        {
//            await DeferAsync();
//            costName = costName.Trim();

//            EmbedHelper embedHelper = new EmbedHelper();
//            StoredProcedure stored = new StoredProcedure();
//            string connStr = Constants.Constants.discordBotConnStr;
//            string description = "";
//            decimal totalCost = 0.0m;

//            DataTable dt = stored.Select(connStr, "UpdateBudget", new List<System.Data.SqlClient.SqlParameter>
//            {
//                new System.Data.SqlClient.SqlParameter("@CostName", costName),
//                new System.Data.SqlClient.SqlParameter("@CostValue", costValue),
//            });

//            foreach (DataRow dr in dt.Rows)
//            {
//                totalCost += decimal.Parse(dr["Cost"].ToString());
//                description += dr["BudgetCategory"] + " - $" + decimal.Parse(dr["Cost"].ToString()) + "\n";
//            }

//            description += "\n**Total Cost for the Month: $" + totalCost.ToString() + "**";

//            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Budget Item Updated", description, "", Context.User.Username, Discord.Color.Blue).Build());
//        }

//        [SupportGuildOnly(new ulong[] { 880569055856185354 })]
//        [SlashCommand("delcost", "Can only be ran by Bot Owner :)")]
//        [EnabledInDm(true)]
//        [Discord.Interactions.RequireOwner]
//        public async Task DeleteCost([MinLength(1), MaxLength(250)] string costName, [MinValue(0.00)] decimal costValue)
//        {
//            await DeferAsync();
//            costName = costName.Trim();

//            EmbedHelper embedHelper = new EmbedHelper();
//            StoredProcedure stored = new StoredProcedure();
//            string connStr = Constants.Constants.discordBotConnStr;
//            string description = "";
//            decimal totalCost = 0.0m;

//            DataTable dt = stored.Select(connStr, "AddBudget", new List<System.Data.SqlClient.SqlParameter>
//            {
//                new System.Data.SqlClient.SqlParameter("@CostName", costName)
//            });

//            foreach (DataRow dr in dt.Rows)
//            {
//                totalCost += decimal.Parse(dr["Cost"].ToString());
//                description += dr["BudgetCategory"] + " - $" + decimal.Parse(dr["Cost"].ToString()) + "\n";
//            }

//            description += "\n**Total Cost for the Month: $" + totalCost.ToString() + "**";

//            await FollowupAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Budget Item Deleted", description, "", Context.User.Username, Discord.Color.Blue).Build());
//        }
//    }
//}
