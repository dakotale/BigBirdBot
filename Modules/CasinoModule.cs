using Discord.Commands;
using DiscordBot.Casino;
using DiscordBot.Constants;

namespace DiscordBot.Modules
{
    public class CasinoModule : ModuleBase<SocketCommandContext>
    {
        Audit audit = new Audit();

        [Command("blackjack")]
        public async Task HandleBlackJack()
        {
            audit.InsertAudit("blackjack", Context.User.Username, Constants.Constants.discordBotConnStr);
            // SPs in use
            // GetCard - Gets TOP(1) Random card and stores it in CardInUse
            // DeleteCardInUse - Truncate CardInUse
            string connStr = Constants.Constants.discordBotConnStr;
            Card card = new Card();
            Card result = card.GetCard(connStr);

            // POC: Currency system
            // Use this for betting and provide a bet mechanic
            // Handle Hit/Stand
        }
    }
}
