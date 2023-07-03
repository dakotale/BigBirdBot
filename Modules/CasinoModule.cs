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
            string connStr = Constants.Constants.discordBotConnStr;
            BlackjackSuit suit = new BlackjackSuit();
            BlackjackDeck deck = new BlackjackDeck();

            List<BlackjackSuit> suits = suit.GetBlackjackSuits(connStr);
            List<BlackjackDeck> decks = deck.GetBlackjackDecks(connStr);

            suit = suit.GetRandomSuit(connStr);
            deck = deck.GetRandomDeck(connStr);


        }
    }
}
