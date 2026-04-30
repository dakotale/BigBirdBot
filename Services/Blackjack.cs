using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /blackjack — single-player blackjack against the dealer.
/// Game state is stored per-user. Hit/Stand/Double are handled via buttons.
/// DB state: AddBlackjackGame / GetBlackjackByUser / UpdateBlackjackGame / DeleteBlackjackGame
/// Optional bet uses the credits economy — defaults to 0 for free play.
/// </summary>
public class Blackjack : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();
    private readonly Economy _eco = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    // ── Button IDs ────────────────────────────────────────────────────────────

    private const string BtnHit = "bj:hit";
    private const string BtnStand = "bj:stand";
    private const string BtnDouble = "bj:double";
    private const string BtnPlayAgain = "bj:again";

    // ── Suits / values ────────────────────────────────────────────────────────

    private static readonly string[] Suits = ["♠️", "♥️", "♦️", "♣️"];
    private static readonly string[] Ranks = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];

    // ── Command ───────────────────────────────────────────────────────────────

    [SlashCommand("blackjack", "Play a hand of blackjack against the dealer!")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleBlackjackAsync([MinValue(0)] long bet = 0)
    {
        await DeferAsync();

        // Validate bet if one was placed
        if (bet > 0)
        {
            decimal balance = _eco.GetBalance(UserId, ServerId);
            if (!CreditHelper.IsValidBet((decimal)bet, balance, out string betError))
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed("Blackjack", betError, Username).Build());
                return;
            }
            _eco.DeductCredits(UserId, ServerId, (decimal)bet, "blackjack");
        }

        // One active game per user
        var existing = _sp.Select(Constants.Constants.discordBotConnStr, "GetBlackjackByUser",
            [new SqlParameter("@UserID", UserId)]);

        if (existing.Rows.Count > 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Blackjack", "You already have an active game! Finish it first.", Username).Build());
            return;
        }

        var deck = BuildDeck();
        var player = new List<string> { Deal(deck), Deal(deck) };
        var dealer = new List<string> { Deal(deck), Deal(deck) };

        int playerTotal = HandValue(player);
        int dealerTotal = HandValue(dealer);

        bool playerBJ = playerTotal == 21;
        bool dealerBJ = dealerTotal == 21;

        if (playerBJ || dealerBJ)
        {
            string outcome = (playerBJ, dealerBJ) switch
            {
                (true, true) => "Push — both have Blackjack! 🤝",
                (true, false) => "🃏 Blackjack! You win!",
                _ => "Dealer has Blackjack. You lose. 😔"
            };
            Color colour = (playerBJ, dealerBJ) switch
            {
                (true, true) => Color.Blue,
                (true, false) => Color.Green,
                _ => Color.Red
            };

            if (bet > 0)
            {
                decimal creditResult = (playerBJ, dealerBJ) switch
                {
                    (true, true) => (decimal)bet,
                    (true, false) => (decimal)bet * 2.5m,
                    _ => 0m
                };
                if (creditResult > 0m) _eco.AddCredits(UserId, ServerId, creditResult, "blackjack_win");
                if (creditResult > (decimal)bet)
                {
                    try
                    {
                        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "IncrementChallengeProgress",
                        [ new SqlParameter("@UserID", UserId), new SqlParameter("@ServerID", ServerId),
                          new SqlParameter("@GameType", "blackjack") ]);
                    }
                    catch { }
                }
                outcome += $"\n{CreditHelper.FormatDelta(creditResult - (decimal)bet)} | Balance: {CreditHelper.Format(_eco.GetBalance(UserId, ServerId))}";
            }

            await FollowupAsync(
                embed: BuildEmbed(player, dealer, outcome, colour, revealDealer: true).Build(),
                components: PlayAgainButton());
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBlackjackGame",
        [
            new SqlParameter("@UserID",    UserId),
            new SqlParameter("@MessageID", "0"),
            new SqlParameter("@Deck",      string.Join(",", deck)),
            new SqlParameter("@Player",    string.Join(",", player)),
            new SqlParameter("@Dealer",    string.Join(",", dealer)),
            new SqlParameter("@Doubled",   false),
            new SqlParameter("@Bet",       bet)
        ]);

        var msg = await FollowupAsync(
            embed: BuildEmbed(player, dealer, "Your turn — Hit, Stand, or Double?",
                              Color.Blue, revealDealer: false).Build(),
            components: GameButtons(canDouble: true));

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateBlackjackMessageID",
        [
            new SqlParameter("@UserID",    UserId),
            new SqlParameter("@MessageID", msg.Id.ToString())
        ]);
    }

    // ── Button: Hit ───────────────────────────────────────────────────────────

    [ComponentInteraction(BtnHit)]
    public async Task OnHitAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = LoadGame();
        if (player is null) return;

        if (userId != UserId)
        {
            await FollowupAsync("This isn't your game!", ephemeral: true);
            return;
        }

        player.Add(Deal(deck!));
        int total = HandValue(player);

        if (total > 21)
        {
            EndGame(userId);
            string bustSuffix = bet > 0
                ? $"\n{CreditHelper.FormatDelta(-bet)} | Balance: {CreditHelper.Format(_eco.GetBalance(userId, ServerId))}"
                : "";
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BuildEmbed(player, dealer!, $"Bust! You hit {total}. Dealer wins. 💥{bustSuffix}",
                                          Color.Red, revealDealer: true).Build();
                m.Components = PlayAgainButton();
            });
            return;
        }

        if (total == 21)
        {
            await ResolveStandAsync(player, dealer!, deck!, userId, bet);
            return;
        }

        SaveGame(userId, deck!, player, dealer!, doubled: false, bet);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer!, $"Your turn — Hit or Stand? (Total: {total})",
                                      Color.Blue, revealDealer: false).Build();
            m.Components = GameButtons(canDouble: false);
        });
    }

    // ── Button: Stand ─────────────────────────────────────────────────────────

    [ComponentInteraction(BtnStand)]
    public async Task OnStandAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = LoadGame();
        if (player is null) return;

        if (userId != UserId)
        {
            await FollowupAsync("This isn't your game!", ephemeral: true);
            return;
        }

        await ResolveStandAsync(player, dealer!, deck!, userId, bet);
    }

    // ── Button: Double Down ───────────────────────────────────────────────────

    [ComponentInteraction(BtnDouble)]
    public async Task OnDoubleAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = LoadGame();
        if (player is null) return;

        if (userId != UserId)
        {
            await FollowupAsync("This isn't your game!", ephemeral: true);
            return;
        }

        if (bet > 0)
        {
            decimal balance = _eco.GetBalance(userId, ServerId);
            if (balance < (decimal)bet)
            {
                // Can't afford to double — treat as stand
                await ResolveStandAsync(player, dealer!, deck!, userId, bet);
                return;
            }
            _eco.DeductCredits(userId, ServerId, (decimal)bet, "blackjack_double");
            bet *= 2;
        }

        player.Add(Deal(deck!));
        int total = HandValue(player);

        if (total > 21)
        {
            EndGame(userId);
            string bustSuffix = bet > 0
                ? $"\n{CreditHelper.FormatDelta(-bet)} | Balance: {CreditHelper.Format(_eco.GetBalance(userId, ServerId))}"
                : "";
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BuildEmbed(player, dealer!,
                                          $"Doubled down — Bust! You hit {total}. 💥{bustSuffix}",
                                          Color.Red, revealDealer: true).Build();
                m.Components = PlayAgainButton();
            });
            return;
        }

        await ResolveStandAsync(player, dealer!, deck!, userId, bet);
    }

    // ── Button: Play Again ────────────────────────────────────────────────────

    [ComponentInteraction(BtnPlayAgain)]
    public async Task OnPlayAgainAsync()
    {
        await DeferAsync();

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteBlackjackGame",
            [new SqlParameter("@UserID", UserId)]);

        var deck = BuildDeck();
        var player = new List<string> { Deal(deck), Deal(deck) };
        var dealer = new List<string> { Deal(deck), Deal(deck) };

        int playerTotal = HandValue(player);
        int dealerTotal = HandValue(dealer);
        bool playerBJ = playerTotal == 21;
        bool dealerBJ = dealerTotal == 21;

        if (playerBJ || dealerBJ)
        {
            string outcome = (playerBJ, dealerBJ) switch
            {
                (true, true) => "Push — both have Blackjack! 🤝",
                (true, false) => "🃏 Blackjack! You win!",
                _ => "Dealer has Blackjack. You lose. 😔"
            };
            Color colour = (playerBJ, dealerBJ) switch
            {
                (true, true) => Color.Blue,
                (true, false) => Color.Green,
                _ => Color.Red
            };
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BuildEmbed(player, dealer, outcome, colour, revealDealer: true).Build();
                m.Components = PlayAgainButton();
            });
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBlackjackGame",
        [
            new SqlParameter("@UserID",    UserId),
            new SqlParameter("@MessageID", "0"),
            new SqlParameter("@Deck",      string.Join(",", deck)),
            new SqlParameter("@Player",    string.Join(",", player)),
            new SqlParameter("@Dealer",    string.Join(",", dealer)),
            new SqlParameter("@Doubled",   false),
            new SqlParameter("@Bet",       0L)
        ]);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer, "Your turn — Hit, Stand, or Double?",
                                      Color.Blue, revealDealer: false).Build();
            m.Components = GameButtons(canDouble: true);
        });
    }

    // ── Dealer resolution ─────────────────────────────────────────────────────

    private async Task ResolveStandAsync(
        List<string> player, List<string> dealer, List<string> deck, string userId, long bet)
    {
        // Step 1: reveal dealer's hole card, remove action buttons
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer,
                $"Dealer reveals — {HandValue(dealer)}. Drawing…",
                Color.Blue, revealDealer: true).Build();
            m.Components = new ComponentBuilder().Build();
        });
        await Task.Delay(900);

        // Step 2: dealer draws cards one at a time
        while (HandValue(dealer) < 17)
        {
            dealer.Add(Deal(deck));
            int dv = HandValue(dealer);
            await ModifyOriginalResponseAsync(m =>
                m.Embed = BuildEmbed(player, dealer,
                    dv > 21 ? $"Dealer draws — busts at {dv}! 💥" : $"Dealer draws — {dv}.",
                    dv > 21 ? Color.Green : Color.Blue,
                    revealDealer: true).Build());
            await Task.Delay(800);
        }

        int playerTotal = HandValue(player);
        int dealerTotal = HandValue(dealer);

        string outcome;
        Color colour;
        decimal creditReturn = 0m;

        if (dealerTotal > 21)
        {
            outcome = $"Dealer busts at {dealerTotal}! You win! 🎉";
            colour = Color.Green;
            creditReturn = (decimal)bet * 2m;
        }
        else if (playerTotal > dealerTotal)
        {
            outcome = $"You win! {playerTotal} vs {dealerTotal} 🎉";
            colour = Color.Green;
            creditReturn = (decimal)bet * 2m;
        }
        else if (playerTotal == dealerTotal)
        {
            outcome = $"Push! Both have {playerTotal}. 🤝";
            colour = Color.Blue;
            creditReturn = (decimal)bet;
        }
        else
        {
            outcome = $"Dealer wins. {dealerTotal} vs {playerTotal}. 😔";
            colour = Color.Red;
        }

        if (bet > 0 && creditReturn > 0m)
        {
            _eco.AddCredits(userId, ServerId, creditReturn, "blackjack_win");
            if (creditReturn > (decimal)bet)
            {
                try
                {
                    _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "IncrementChallengeProgress",
                    [ new SqlParameter("@UserID", userId), new SqlParameter("@ServerID", ServerId),
                      new SqlParameter("@GameType", "blackjack") ]);
                }
                catch { }
            }
        }

        if (bet > 0)
        {
            decimal net = creditReturn > 0m ? creditReturn - (decimal)bet : -(decimal)bet;
            outcome += $"\n{CreditHelper.FormatDelta(net)} | Balance: {CreditHelper.Format(_eco.GetBalance(userId, ServerId))}";
        }

        EndGame(userId);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer, outcome, colour, revealDealer: true).Build();
            m.Components = PlayAgainButton();
        });
    }

    // ── Deck helpers ──────────────────────────────────────────────────────────

    private static List<string> BuildDeck()
    {
        var deck = (from suit in Suits
                    from rank in Ranks
                    select $"{rank}|{suit}").ToList();

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }

    private static string Deal(List<string> deck)
    {
        string card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    private static int HandValue(IEnumerable<string> hand)
    {
        int total = 0;
        int aces = 0;

        foreach (var card in hand)
        {
            string rank = card.Split('|')[0];
            int val = rank switch
            {
                "A" => 11,
                "J" or "Q" or "K" => 10,
                _ => int.Parse(rank)
            };
            if (rank == "A") aces++;
            total += val;
        }

        while (total > 21 && aces > 0) { total -= 10; aces--; }

        return total;
    }

    private static string FormatHand(List<string> hand, bool hideSecond = false)
    {
        var cards = hand.Select((c, i) =>
        {
            if (i == 1 && hideSecond) return "🂠";
            var parts = c.Split('|');
            return $"{parts[0]}{parts[1]}";
        });

        string display = string.Join("  ", cards);
        return hideSecond ? display : $"{display}  **({HandValue(hand)})**";
    }

    // ── Embed / component builders ────────────────────────────────────────────

    private EmbedBuilder BuildEmbed(
        List<string> player, List<string> dealer,
        string status, Color colour, bool revealDealer) =>
        new EmbedBuilder()
            .WithTitle("🃏  Blackjack")
            .WithColor(colour)
            .AddField("Dealer", FormatHand(dealer, hideSecond: !revealDealer), inline: false)
            .AddField($"{Username}'s Hand", FormatHand(player), inline: false)
            .WithDescription($"**{status}**")
            .WithFooter($"Player: {Username}", AvatarUrl)
            .WithCurrentTimestamp();

    private static MessageComponent GameButtons(bool canDouble) =>
        new ComponentBuilder()
            .WithButton("Hit", BtnHit, ButtonStyle.Success, new Emoji("👊"), row: 0)
            .WithButton("Stand", BtnStand, ButtonStyle.Danger, new Emoji("🖐️"), row: 0)
            .WithButton("Double", BtnDouble, ButtonStyle.Primary, new Emoji("💰"), row: 0,
                        disabled: !canDouble)
            .Build();

    private static MessageComponent PlayAgainButton() =>
        new ComponentBuilder()
            .WithButton("Play Again", BtnPlayAgain, ButtonStyle.Success, new Emoji("🔄"))
            .Build();

    // ── DB helpers ────────────────────────────────────────────────────────────

    private (List<string>? player, List<string>? dealer, List<string>? deck,
             bool doubled, string userId, long bet) LoadGame()
    {
        string userId = UserId;
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetBlackjackByUser",
            [new SqlParameter("@UserID", userId)]);

        if (dt.Rows.Count == 0) return (null, null, null, false, userId, 0);

        var row = dt.Rows[0];
        var player = row["Player"].ToString()!.Split(',').ToList();
        var dealer = row["Dealer"].ToString()!.Split(',').ToList();
        var deck = row["Deck"].ToString()!.Split(',').ToList();
        bool doubled = bool.TryParse(row["Doubled"]?.ToString(), out bool d) && d;
        long bet = long.TryParse(row["Bet"]?.ToString(), out long b) ? b : 0;

        return (player, dealer, deck, doubled, userId, bet);
    }

    private void SaveGame(string userId, List<string> deck,
                          List<string> player, List<string> dealer, bool doubled, long bet) =>
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateBlackjackGame",
        [
            new SqlParameter("@UserID",  userId),
            new SqlParameter("@Deck",    string.Join(",", deck)),
            new SqlParameter("@Player",  string.Join(",", player)),
            new SqlParameter("@Dealer",  string.Join(",", dealer)),
            new SqlParameter("@Doubled", doubled),
            new SqlParameter("@Bet",     bet)
        ]);

    private void EndGame(string userId) =>
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteBlackjackGame",
            [new SqlParameter("@UserID", userId)]);
}