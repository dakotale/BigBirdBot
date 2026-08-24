using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /blackjack — single-player blackjack against the dealer.
/// Game state is stored per-user. Hit/Stand/Double are handled via buttons.
/// DB state: AddBlackjackGame / GetBlackjackByUser / UpdateBlackjackGame / DeleteBlackjackGame
/// Optional bet uses the credits economy — defaults to 0 for free play.
/// </summary>
public class Blackjack(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

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

    /// <summary>
    /// Starts a new hand: deducts the optional bet, deals two cards each, and resolves
    /// immediately on a natural Blackjack for either side; otherwise saves the game and
    /// waits for a Hit/Stand/Double button press.
    /// </summary>
    [SlashCommand("blackjack", "Play a hand of blackjack against the dealer!")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleBlackjackAsync([MinValue(0)] long bet = 0)
    {
        await DeferAsync();

        // Validate bet if one was placed
        if (bet > 0)
        {
            decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
            if (!CreditHelper.IsValidBet((decimal)bet, balance, out string betError))
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed("Blackjack", betError, Username).Build());
                return;
            }
            await CreditService.DeductCreditsAsync(db, UserId, ServerId, (decimal)bet, "blackjack");
        }

        // One active game per user
        bool existing = await db.BlackjackGames.AnyAsync(g => g.UserId == UserId);

        if (existing)
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
                if (creditResult > 0m) await CreditService.AddCreditsAsync(db, UserId, ServerId, creditResult, "blackjack_win");
                if (creditResult > (decimal)bet)
                {
                    try { await ChallengeService.IncrementProgressAsync(db, UserId, ServerId, "blackjack"); }
                    catch { }
                }
                outcome += $"\n{CreditHelper.FormatDelta(creditResult - (decimal)bet)} | Balance: {CreditHelper.Format(await CreditService.GetBalanceAsync(db, UserId, ServerId))}";
            }

            await FollowupAsync(
                embed: BuildEmbed(player, dealer, outcome, colour, revealDealer: true).Build(),
                components: PlayAgainButton());
            return;
        }

        db.BlackjackGames.Add(new BlackjackGame
        {
            UserId = UserId, MessageId = "0", Deck = string.Join(",", deck),
            Player = string.Join(",", player), Dealer = string.Join(",", dealer),
            Doubled = false, Bet = bet
        });
        await db.SaveChangesAsync();

        var msg = await FollowupAsync(
            embed: BuildEmbed(player, dealer, "Your turn — Hit, Stand, or Double?",
                              Color.Blue, revealDealer: false).Build(),
            components: GameButtons(canDouble: true));

        await db.BlackjackGames.Where(g => g.UserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.MessageId, msg.Id.ToString()));
    }

    // ── Button: Hit ───────────────────────────────────────────────────────────

    /// <summary>Draws one card for the player; ends the game on a bust, auto-stands on exactly 21, otherwise saves and waits for the next action.</summary>
    [ComponentInteraction(BtnHit)]
    public async Task OnHitAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = await LoadGame();
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
            await EndGame(userId);
            string bustSuffix = bet > 0
                ? $"\n{CreditHelper.FormatDelta(-bet)} | Balance: {CreditHelper.Format(await CreditService.GetBalanceAsync(db, userId, ServerId))}"
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

        await SaveGame(userId, deck!, player, dealer!, doubled: false, bet);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer!, $"Your turn — Hit or Stand? (Total: {total})",
                                      Color.Blue, revealDealer: false).Build();
            m.Components = GameButtons(canDouble: false);
        });
    }

    // ── Button: Stand ─────────────────────────────────────────────────────────

    /// <summary>Player stands — hands off to the dealer's draw-to-17 resolution.</summary>
    [ComponentInteraction(BtnStand)]
    public async Task OnStandAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = await LoadGame();
        if (player is null) return;

        if (userId != UserId)
        {
            await FollowupAsync("This isn't your game!", ephemeral: true);
            return;
        }

        await ResolveStandAsync(player, dealer!, deck!, userId, bet);
    }

    // ── Button: Double Down ───────────────────────────────────────────────────

    /// <summary>Doubles the bet (if affordable — otherwise treated as a stand), draws exactly one card, then stands automatically.</summary>
    [ComponentInteraction(BtnDouble)]
    public async Task OnDoubleAsync()
    {
        await DeferAsync();

        var (player, dealer, deck, _, userId, bet) = await LoadGame();
        if (player is null) return;

        if (userId != UserId)
        {
            await FollowupAsync("This isn't your game!", ephemeral: true);
            return;
        }

        if (bet > 0)
        {
            decimal balance = await CreditService.GetBalanceAsync(db, userId, ServerId);
            if (balance < (decimal)bet)
            {
                // Can't afford to double — treat as stand
                await ResolveStandAsync(player, dealer!, deck!, userId, bet);
                return;
            }
            await CreditService.DeductCreditsAsync(db, userId, ServerId, (decimal)bet, "blackjack_double");
            bet *= 2;
        }

        player.Add(Deal(deck!));
        int total = HandValue(player);

        if (total > 21)
        {
            await EndGame(userId);
            string bustSuffix = bet > 0
                ? $"\n{CreditHelper.FormatDelta(-bet)} | Balance: {CreditHelper.Format(await CreditService.GetBalanceAsync(db, userId, ServerId))}"
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

    /// <summary>Clears the finished game and deals a fresh hand in-place on the same message.</summary>
    [ComponentInteraction(BtnPlayAgain)]
    public async Task OnPlayAgainAsync()
    {
        await DeferAsync();

        await db.BlackjackGames.Where(g => g.UserId == UserId).ExecuteDeleteAsync();

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

        db.BlackjackGames.Add(new BlackjackGame
        {
            UserId = UserId, MessageId = "0", Deck = string.Join(",", deck),
            Player = string.Join(",", player), Dealer = string.Join(",", dealer),
            Doubled = false, Bet = 0m
        });
        await db.SaveChangesAsync();

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer, "Your turn — Hit, Stand, or Double?",
                                      Color.Blue, revealDealer: false).Build();
            m.Components = GameButtons(canDouble: true);
        });
    }

    // ── Dealer resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Reveals the dealer's hole card, draws for the dealer one card at a time (animating
    /// each draw) until they reach 17+ or bust, then settles the bet and ends the game.
    /// </summary>
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
            await CreditService.AddCreditsAsync(db, userId, ServerId, creditReturn, "blackjack_win");
            if (creditReturn > (decimal)bet)
            {
                try { await ChallengeService.IncrementProgressAsync(db, userId, ServerId, "blackjack"); }
                catch { }
            }
        }

        if (bet > 0)
        {
            decimal net = creditReturn > 0m ? creditReturn - (decimal)bet : -(decimal)bet;
            outcome += $"\n{CreditHelper.FormatDelta(net)} | Balance: {CreditHelper.Format(await CreditService.GetBalanceAsync(db, userId, ServerId))}";
        }

        await EndGame(userId);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildEmbed(player, dealer, outcome, colour, revealDealer: true).Build();
            m.Components = PlayAgainButton();
        });
    }

    // ── Deck helpers ──────────────────────────────────────────────────────────

    /// <summary>Builds a full 52-card deck (as "rank|suit" strings) and shuffles it via Fisher-Yates.</summary>
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

    /// <summary>Removes and returns the top card of the deck.</summary>
    private static string Deal(List<string> deck)
    {
        string card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    /// <summary>Computes a hand's best blackjack total, counting Aces as 11 and dropping them to 1 one at a time if that would otherwise bust.</summary>
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

    /// <summary>Formats a hand for display, optionally hiding the dealer's second (hole) card while the player is still acting.</summary>
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

    /// <summary>Builds the standard game-state embed: both hands plus a status line, with the dealer's hole card hidden until reveal.</summary>
    private EmbedBuilder BuildEmbed(
        List<string> player, List<string> dealer,
        string status, Color colour, bool revealDealer) =>
        _embed.BuildSimpleEmbed(
            "🃏  Blackjack", $"**{status}**", colour,
            footer: $"Player: {Username}", footerIconUrl: AvatarUrl,
            fields: [("Dealer", FormatHand(dealer, hideSecond: !revealDealer), false),
                     ($"{Username}'s Hand", FormatHand(player), false)]);

    /// <summary>Builds the Hit/Stand/Double button row; Double is disabled once the player has already hit.</summary>
    private static MessageComponent GameButtons(bool canDouble) =>
        new ComponentBuilder()
            .WithButton("Hit", BtnHit, ButtonStyle.Success, new Emoji("👊"), row: 0)
            .WithButton("Stand", BtnStand, ButtonStyle.Danger, new Emoji("🖐️"), row: 0)
            .WithButton("Double", BtnDouble, ButtonStyle.Primary, new Emoji("💰"), row: 0,
                        disabled: !canDouble)
            .Build();

    /// <summary>Builds the single "Play Again" button shown once a hand finishes.</summary>
    private static MessageComponent PlayAgainButton() =>
        new ComponentBuilder()
            .WithButton("Play Again", BtnPlayAgain, ButtonStyle.Success, new Emoji("🔄"))
            .Build();

    // ── DB helpers ────────────────────────────────────────────────────────────

    /// <summary>Loads the calling user's in-progress game from the DB, or a tuple of nulls if they have none active.</summary>
    private async Task<(List<string>? player, List<string>? dealer, List<string>? deck,
             bool doubled, string userId, long bet)> LoadGame()
    {
        string userId = UserId;
        var game = await db.BlackjackGames.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == userId);

        if (game is null) return (null, null, null, false, userId, 0);

        var player = game.Player.Split(',').ToList();
        var dealer = game.Dealer.Split(',').ToList();
        var deck = game.Deck.Split(',').ToList();

        return (player, dealer, deck, game.Doubled, userId, (long)game.Bet);
    }

    /// <summary>Persists the current hand/deck state so the player can act on it via a later button press.</summary>
    private async Task SaveGame(string userId, List<string> deck,
                          List<string> player, List<string> dealer, bool doubled, long bet) =>
        await db.BlackjackGames.Where(g => g.UserId == userId).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.Deck, string.Join(",", deck))
            .SetProperty(g => g.Player, string.Join(",", player))
            .SetProperty(g => g.Dealer, string.Join(",", dealer))
            .SetProperty(g => g.Doubled, doubled)
            .SetProperty(g => g.Bet, bet));

    /// <summary>Deletes the user's saved game row once a hand is finished.</summary>
    private async Task EndGame(string userId) =>
        await db.BlackjackGames.Where(g => g.UserId == userId).ExecuteDeleteAsync();
}