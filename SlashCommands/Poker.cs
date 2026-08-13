using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Grouped under /game poker.
/// Multiplayer Texas Hold'em poker.
///
/// Flow:
///   1. /poker [bet] — host creates a lobby, bot auto-joins. Lobby message shows
///      Join / Start buttons.
///   2. Up to 4 humans click "Join" — each gets their 2 hole cards ephemerally.
///   3. Host (or anyone) clicks "Start" — game animates through flop → turn → river,
///      then reveals all hands and pays the winner.
///
/// One active game per channel at a time.
/// Bot entry is free (house money). Only human bets form the real pot.
/// If the bot wins, the house takes the pot.
/// </summary>
public partial class Games
{
    private readonly Economy _eco = new();

    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";
    private string ChannelId => Context.Channel.Id.ToString();

    internal static readonly Color ColourWin  = EmbedColors.Green;
    internal static readonly Color ColourLoss  = EmbedColors.Red;
    internal static readonly Color ColourGold  = EmbedColors.Gold;
    internal static readonly Color ColourInfo  = EmbedColors.Blue;

    internal const int MaxHumans = 4;

    // ── /poker ────────────────────────────────────────────────────────────────

    /// <summary>Creates a poker lobby in the channel (one at a time), auto-seating the bot and the host after deducting the host's buy-in, and posts Join/Start buttons.</summary>
    [SlashCommand("poker", "Start a Texas Hold'em table! Up to 4 players vs the bot.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePokerAsync([MinValue(50)] long bet)
    {
        await DeferAsync();

        // One active game per channel
        var existing = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerGame",
            [new SqlParameter("@ChannelID", ChannelId)]);

        if (existing.Rows.Count > 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Poker",
                "There's already an active game in this channel! Wait for it to finish.", Username).Build());
            return;
        }

        decimal balance = _eco.GetBalance(UserId, ServerId);
        if ((decimal)bet > balance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Poker",
                $"You don't have enough credits. Balance: {CreditHelper.Format(balance)}.", Username).Build());
            return;
        }

        // Build and shuffle deck, deal 2 cards to the bot
        var deck = CreditHelper.BuildPokerDeck();
        var botHand = new List<string> { deck[0], deck[1] };
        var remaining = deck.Skip(2).ToList();

        // Create game row
        var gameIdDt = _sp.Select(Constants.Constants.discordBotConnStr, "CreatePokerGame",
        [
            new SqlParameter("@ChannelID",    ChannelId),
            new SqlParameter("@ServerID",     ServerId),
            new SqlParameter("@BetPerPlayer", bet),
            new SqlParameter("@Deck",         string.Join(",", remaining))
        ]);

        int gameId = int.Parse(gameIdDt.Rows[0]["GameID"].ToString()!);

        // Add bot player
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPokerPlayer",
        [
            new SqlParameter("@GameID",  gameId),
            new SqlParameter("@UserID",  CreditHelper.PokerBotId),
            new SqlParameter("@Hand",    string.Join(",", botHand))
        ]);

        // Deduct host's bet and add them as a player
        _eco.DeductCredits(UserId, ServerId, (decimal)bet, "poker_buy_in");
        var (hostHand, deckAfterHost) = DealFromDeck(remaining, 2);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePokerDeck",
        [
            new SqlParameter("@GameID", gameId),
            new SqlParameter("@Deck",   string.Join(",", deckAfterHost))
        ]);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPokerPlayer",
        [
            new SqlParameter("@GameID",  gameId),
            new SqlParameter("@UserID",  UserId),
            new SqlParameter("@Hand",    string.Join(",", hostHand))
        ]);

        // Post lobby message
        var components = BuildLobbyButtons(gameId);
        var players = new List<(string userId, bool isBot)>
        {
            (CreditHelper.PokerBotId, true),
            (UserId, false)
        };

        var lobbyEmbed = BuildLobbyEmbed(bet, players, 1).Build();
        var msg = await FollowupAsync(embed: lobbyEmbed, components: components);

        // Store message ID so subsequent joins can edit it
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePokerMessage",
        [
            new SqlParameter("@GameID",    gameId),
            new SqlParameter("@MessageID", msg.Id.ToString())
        ]);

        // DM hole cards to host
        await TrySendHoleCards(Context.User, hostHand, bet);
    }

    // poker:join and poker:start are in GameComponentHandlers below (outside [Group])

    // ── Embed builders ─────────────────────────────────────────────────────────

    /// <summary>Builds the pre-game lobby embed listing seated players, empty seats, buy-in, and current pot.</summary>
    internal static EmbedBuilder BuildLobbyEmbed(
        long bet,
        IEnumerable<(string userId, bool isBot)> players,
        int humanCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🤖 **{CreditHelper.PokerBotId.Replace("BOT", "BigBirdBot")}** *(dealer)*");
        int seat = 1;
        foreach (var (userId, isBot) in players.Where(p => !p.isBot))
            sb.AppendLine($"{seat++}. ✅ <@{userId}>");
        for (int i = seat; i <= MaxHumans; i++)
            sb.AppendLine($"{i}. *Waiting…*");

        return new EmbedHelper().BuildSimpleEmbed(
            "🃏  Texas Hold'em — Lobby",
            $"💰 **Buy-in:** {CreditHelper.Format((decimal)bet)} per player\n" +
            $"🏆 **Pot:** {CreditHelper.Format(humanCount * (decimal)bet)} ({humanCount} human{(humanCount == 1 ? "" : "s")})\n\n" +
            $"**Players:**\n{sb.ToString().TrimEnd()}",
            ColourInfo, footer: "Click Join to enter • Host clicks Start when ready", timestamp: false);
    }

    /// <summary>Builds one in-progress frame of the game embed for a given phase (shuffle/deal/flop/turn/river/etc.), revealing <paramref name="reveal"/> community cards with hole cards kept hidden.</summary>
    internal static EmbedBuilder BuildGameEmbed(
        string phase,
        List<string> community,
        int reveal,
        IEnumerable<(string userId, List<string> hand, bool isBot)> players,
        long bet,
        string? focusUserId)
    {
        // Community row
        var commParts = community.Take(reveal).Select(CreditHelper.ShowCard).ToList();
        for (int i = commParts.Count; i < 5; i++)
            commParts.Add("🂠");
        string commLine = string.Join("  ", commParts);

        var sb = new System.Text.StringBuilder();
        foreach (var p in players)
        {
            string name = p.isBot ? "🤖 BigBirdBot" : $"<@{p.userId}>";
            string cards = "🂠  🂠";  // always hidden pre-showdown
            sb.AppendLine($"{name} — {cards}");
        }

        return new EmbedHelper().BuildSimpleEmbed(
            $"🃏  Texas Hold'em — {phase}",
            $"**Community:** {commLine}\n\n" +
            $"**Players:**\n{sb.ToString().TrimEnd()}",
            ColourInfo, footer: $"Buy-in: {CreditHelper.Format((decimal)bet)} each", timestamp: false);
    }

    /// <summary>Builds the final showdown embed: every player's revealed hand ranked by score, the winning hand name, and the payout (or house-win) message.</summary>
    internal static EmbedBuilder BuildShowdownEmbed(
        List<string> community,
        IEnumerable<(string userId, List<string> hand, bool isBot, string handName, int score)> results,
        string winnerId,
        bool botWon,
        long bet,
        decimal humanPot,
        decimal winnerPay)
    {
        string commLine = string.Join("  ", community.Select(CreditHelper.ShowCard));
        var sb = new System.Text.StringBuilder();

        int rank = 1;
        foreach (var r in results)
        {
            string name = r.isBot ? "🤖 BigBirdBot" : $"<@{r.userId}>";
            string cards = CreditHelper.ShowHand(r.hand);
            string medal = rank == 1 ? "🏆" : rank == 2 ? "🥈" : "🥉";
            string payout = rank == 1 && !botWon ? $" **(+{CreditHelper.Format(winnerPay)})**" : "";
            sb.AppendLine($"{medal} {name} — {cards} | **{r.handName}**{payout}");
            rank++;
        }

        string title = botWon
            ? $"🤖  BigBirdBot wins! (House takes {CreditHelper.Format(humanPot)})"
            : $"🏆  <@{winnerId}> wins {CreditHelper.Format(winnerPay)}!";

        Color colour = botWon ? ColourLoss : ColourWin;

        return new EmbedHelper().BuildSimpleEmbed(
            title,
            $"**Community:** {commLine}\n\n" +
            $"**Showdown:**\n{sb.ToString().TrimEnd()}",
            colour, footer: $"Buy-in was {CreditHelper.Format((decimal)bet)} per player • Pot: {CreditHelper.Format(humanPot)}");
    }

    // ── Component builder ──────────────────────────────────────────────────────

    /// <summary>Builds the lobby's Join/Start buttons, disabling Join once the table is full.</summary>
    internal static MessageComponent BuildLobbyButtons(int gameId, bool joinDisabled = false) =>
        new ComponentBuilder()
            .WithButton("🃏 Join Game", $"poker:join:{gameId}", ButtonStyle.Primary, disabled: joinDisabled)
            .WithButton("▶ Start", $"poker:start:{gameId}", ButtonStyle.Success)
            .Build();

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Takes the next <paramref name="count"/> cards off the top of the deck, returning the dealt hand and the remaining deck.</summary>
    internal static (List<string> hand, List<string> remaining) DealFromDeck(
        List<string> deck, int count)
    {
        var hand = deck.Take(count).ToList();
        var remaining = deck.Skip(count).ToList();
        return (hand, remaining);
    }

    /// <summary>Attempts to DM a player their hole cards, silently ignoring failures (e.g. DMs disabled) since the cards are also shown ephemerally.</summary>
    internal static async Task TrySendHoleCards(IUser user, List<string> hand, long bet)
    {
        try
        {
            var dm = await user.CreateDMChannelAsync();
            await dm.SendMessageAsync(
                $"🃏 **Your hole cards for the current poker game:**\n" +
                $"{CreditHelper.ShowHand(hand)}\n\n" +
                $"*Buy-in: {CreditHelper.Format((decimal)bet)}*");
        }
        catch { /* DMs disabled — cards were already shown ephemerally */ }
    }
}

// ── Component handlers for poker buttons (must be outside [Group("game")]) ───────

/// <summary>Button handlers for the poker lobby's Join/Start flow — declared outside [Group] since component interaction IDs aren't routed through the slash-command group.</summary>
public class GameComponentHandlers : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp  = new();
    private readonly Economy         _eco = new();

    private string UserId    => Context.User.Id.ToString();
    private string ServerId  => Context.Guild?.Id.ToString() ?? "DM";

    // ── Join button ────────────────────────────────────────────────────────────

    /// <summary>Seats the clicking user at the table (validating the game is still open, they're not already seated, the table isn't full, and they can afford the buy-in), deducts their buy-in, deals their hand, and refreshes the lobby embed.</summary>
    [ComponentInteraction("poker:join:*")]
    public async Task OnPokerJoinAsync(string gameIdStr)
    {
        await DeferAsync();

        int gameId = int.Parse(gameIdStr);

        var gameDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerGameById",
            [new SqlParameter("@GameID", gameId)]);

        if (gameDt.Rows.Count == 0 || gameDt.Rows[0]["Status"].ToString() != "waiting")
        {
            await FollowupAsync("This game is no longer accepting players.", ephemeral: true);
            return;
        }

        var gameRow = gameDt.Rows[0];
        long bet = long.Parse(gameRow["BetPerPlayer"].ToString()!);

        var playersDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerPlayers",
            [new SqlParameter("@GameID", gameId)]);

        var players = playersDt.Rows.Cast<DataRow>().ToList();

        if (players.Any(p => p["UserID"].ToString() == UserId))
        {
            await FollowupAsync("You're already at the table!", ephemeral: true);
            return;
        }

        int humanCount = players.Count(p => p["UserID"].ToString() != CreditHelper.PokerBotId);
        if (humanCount >= Games.MaxHumans)
        {
            await FollowupAsync("This table is full.", ephemeral: true);
            return;
        }

        decimal balance = _eco.GetBalance(UserId, ServerId);
        if ((decimal)bet > balance)
        {
            await FollowupAsync(
                $"You need {CreditHelper.Format((decimal)bet)} to join. You have {CreditHelper.Format(balance)}.",
                ephemeral: true);
            return;
        }

        var deckCards = gameRow["Deck"].ToString()!.Split(',').ToList();
        var (hand, deckAfter) = Games.DealFromDeck(deckCards, 2);

        _eco.DeductCredits(UserId, ServerId, (decimal)bet, "poker_buy_in");

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePokerDeck",
        [
            new SqlParameter("@GameID", gameId),
            new SqlParameter("@Deck",   string.Join(",", deckAfter))
        ]);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPokerPlayer",
        [
            new SqlParameter("@GameID",  gameId),
            new SqlParameter("@UserID",  UserId),
            new SqlParameter("@Hand",    string.Join(",", hand))
        ]);

        var updatedPlayersDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerPlayers",
            [new SqlParameter("@GameID", gameId)]);
        var updatedPlayers = updatedPlayersDt.Rows.Cast<DataRow>()
            .Select(r => (r["UserID"].ToString()!, r["UserID"].ToString() == CreditHelper.PokerBotId))
            .ToList();

        int newHumanCount = updatedPlayers.Count(p => !p.Item2);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = Games.BuildLobbyEmbed(bet, updatedPlayers, newHumanCount).Build();
            m.Components = newHumanCount >= Games.MaxHumans
                ? Games.BuildLobbyButtons(gameId, joinDisabled: true)
                : Games.BuildLobbyButtons(gameId);
        });

        await FollowupAsync(
            $"🃏 **Your hole cards:** {CreditHelper.ShowHand(hand)}\n*(Also check your DMs!)*",
            ephemeral: true);

        await Games.TrySendHoleCards(Context.User, hand, bet);
    }

    // ── Start button ───────────────────────────────────────────────────────────

    /// <summary>Runs the full hand once at least one human has joined: animates through shuffle/deal/flop/turn/river/showdown via successive message edits, scores every hand, and pays the winner from the human pot (or the house keeps it if the bot wins).</summary>
    [ComponentInteraction("poker:start:*")]
    public async Task OnPokerStartAsync(string gameIdStr)
    {
        await DeferAsync();

        int gameId = int.Parse(gameIdStr);

        var gameDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerGameById",
            [new SqlParameter("@GameID", gameId)]);

        if (gameDt.Rows.Count == 0 || gameDt.Rows[0]["Status"].ToString() != "waiting")
        {
            await FollowupAsync("Game already started or not found.", ephemeral: true);
            return;
        }

        var gameRow = gameDt.Rows[0];

        var playersDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPokerPlayers",
            [new SqlParameter("@GameID", gameId)]);
        var players = playersDt.Rows.Cast<DataRow>().ToList();

        int humanCount = players.Count(p => p["UserID"].ToString() != CreditHelper.PokerBotId);
        if (humanCount < 1)
        {
            await FollowupAsync("Need at least 1 human player to start!", ephemeral: true);
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePokerStatus",
        [
            new SqlParameter("@GameID",    gameId),
            new SqlParameter("@Status",    "active"),
            new SqlParameter("@Community", "")
        ]);

        long bet      = long.Parse(gameRow["BetPerPlayer"].ToString()!);
        var  deckList = gameRow["Deck"].ToString()!.Split(',').ToList();
        var  community = deckList.Take(5).ToList();

        var playerList = players.Select(p => (
            userId: p["UserID"].ToString()!,
            hand:   p["Hand"].ToString()!.Split(',').ToList(),
            isBot:  p["UserID"].ToString() == CreditHelper.PokerBotId
        )).ToList();

        // ── Step 1: Shuffle ────────────────────────────────────────────────────
        var gameMsg = await ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = Games.BuildGameEmbed("🂠  Shuffling the deck…", community, 0, playerList, bet, null).Build();
            m.Components = new ComponentBuilder().Build();
        });

        await Task.Delay(900);

        // ── Step 2: Deal ───────────────────────────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🃏  Dealing cards…", community, 0, playerList, bet, null).Build());

        await Task.Delay(1400);

        // ── Step 3: Flop — one card at a time ─────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🌊  Flop  *(1 / 3)*", community, 1, playerList, bet, null).Build());

        await Task.Delay(750);

        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🌊  Flop  *(2 / 3)*", community, 2, playerList, bet, null).Build());

        await Task.Delay(750);

        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🌊  Flop", community, 3, playerList, bet, null).Build());

        await Task.Delay(1800);

        // ── Step 4: Turn (4th card) ────────────────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🎯  Turn", community, 4, playerList, bet, null).Build());

        await Task.Delay(2000);

        // ── Step 5: River (5th card) ───────────────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("💧  River", community, 5, playerList, bet, null).Build());

        await Task.Delay(2000);

        // ── Step 6: Showdown tease ─────────────────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("⚡  Showdown!", community, 5, playerList, bet, null).Build());

        await Task.Delay(1100);

        // ── Step 7: Calculating hands ──────────────────────────────────────────
        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildGameEmbed("🧮  Calculating hands…", community, 5, playerList, bet, null).Build());

        await Task.Delay(1000);

        // ── Step 8: Showdown ───────────────────────────────────────────────────
        var results = playerList.Select(p =>
        {
            var seven = p.hand.Concat(community).ToList();
            var (_, name) = CreditHelper.BestHandType(seven);
            int score = CreditHelper.HandScore(seven);
            return (p.userId, p.hand, p.isBot, handName: name, score);
        }).OrderByDescending(r => r.score).ToList();

        var winner    = results.First();
        bool botWon   = winner.isBot;
        decimal humanPot  = humanCount * (decimal)bet;
        decimal winnerPay = botWon ? 0m : humanPot;

        if (!botWon)
        {
            _eco.AddCredits(winner.userId, ServerId, winnerPay, "poker_win");
            try
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "IncrementChallengeProgress",
                [
                    new SqlParameter("@UserID",   winner.userId),
                    new SqlParameter("@ServerID", ServerId),
                    new SqlParameter("@GameType", "poker")
                ]);
            }
            catch { }
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePokerStatus",
        [
            new SqlParameter("@GameID",    gameId),
            new SqlParameter("@Status",    "done"),
            new SqlParameter("@Community", string.Join(",", community))
        ]);

        await gameMsg.ModifyAsync(m =>
            m.Embed = Games.BuildShowdownEmbed(community, results, winner.userId, botWon,
                                               bet, humanPot, winnerPay).Build());
    }
}