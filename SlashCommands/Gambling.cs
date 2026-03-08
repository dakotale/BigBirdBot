using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Gambling commands — all require a credit bet.
/// Games: Slots, Coinflip, Dice, Roulette, Scratch Card, Horse Race, RPS,
///        High-Low, Jackpot, Transfer.
/// Stats: /gamblestats
///
/// Per-user cooldowns are tracked in memory (reset on restart — intentional).
/// Daily loss limit is enforced via GambleLog table.
/// </summary>
public class Gambling : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();
    private readonly Economy _eco = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourWin = new(87, 242, 135);
    private static readonly Color ColourLoss = new(237, 66, 69);
    private static readonly Color ColourPush = new(88, 101, 242);
    private static readonly Color ColourGold = new(255, 215, 0);
    private static readonly Color ColourInfo = new(88, 101, 242);

    // Key: "userId:game"  Value: last-used UTC time

    private static readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(8);

    private bool IsOnCooldown(string game, out TimeSpan remaining)
    {
        string key = $"{UserId}:{game}";
        if (_cooldowns.TryGetValue(key, out var last))
        {
            var elapsed = DateTime.UtcNow - last;
            if (elapsed < CooldownDuration)
            {
                remaining = CooldownDuration - elapsed;
                return true;
            }
        }
        remaining = TimeSpan.Zero;
        return false;
    }

    private void SetCooldown(string game) =>
        _cooldowns[$"{UserId}:{game}"] = DateTime.UtcNow;


    [SlashCommand("slots", "Spin the slot machine!")]
    [EnabledInDm(false)]
    public async Task HandleSlotsAsync([MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("slots", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "slots")) return;

        SetCooldown("slots");

        string r1 = CreditHelper.SpinReel();
        string r2 = CreditHelper.SpinReel();
        string r3 = CreditHelper.SpinReel();

        var (payout, result) = CreditHelper.CalculateSlotPayout(r1, r2, r3, bet);
        long newBalance = ApplyGamble(bet, payout, "slots");

        EmbedBuilder SpinFrame(string a, string b, string c, string? label = null) =>
            new EmbedBuilder()
                .WithTitle("🎰  Slot Machine")
                .WithColor(ColourInfo)
                .WithDescription(
                    $"╔══════════════╗\n" +
                    $"║  {a}  {b}  {c}  ║\n" +
                    $"╚══════════════╝\n\n" +
                    (label ?? "*Spinning…*"))
                .WithFooter(Username, AvatarUrl);

        var msg = await FollowupAsync(embed: SpinFrame(
            CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom()).Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = SpinFrame(
            r1, CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom()).Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🎰  Slot Machine")
            .WithColor(payout >= bet ? ColourWin : payout > 0 ? ColourPush : ColourLoss)
            .WithDescription(
                $"╔══════════════╗\n" +
                $"║  {r1}  {r2}  {r3}  ║\n" +
                $"╚══════════════╝\n\n" +
                $"**{result}**")
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("coinflip", "Flip a coin and bet on the outcome!")]
    [EnabledInDm(false)]
    public async Task HandleCoinflipAsync(
        [Choice("Heads", "heads"),
         Choice("Tails", "tails")]
        string side,
        [MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("coinflip", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "coinflip")) return;

        SetCooldown("coinflip");

        string result = Random.Shared.Next(2) == 0 ? "heads" : "tails";
        bool won = result == side;
        long payout = won ? (long)(bet * 1.9) : 0;

        long newBalance = ApplyGamble(bet, payout, "coinflip");

        string coinEmoji = result == "heads" ? "🪙" : "⚫";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{coinEmoji}  Coin Flip — {char.ToUpper(result[0])}{result[1..]}")
            .WithColor(won ? ColourWin : ColourLoss)
            .WithDescription(
                won
                    ? $"You called **{side}** — correct! {CreditHelper.FormatDelta(payout - bet)}"
                    : $"You called **{side}** — it was **{result}**. {CreditHelper.FormatDelta(-bet)}")
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("dice", "Roll two dice and bet on the total!")]
    [EnabledInDm(false)]
    public async Task HandleDiceAsync(
        [Choice("Over 7",    "over"),
         Choice("Under 7",   "under"),
         Choice("Exactly 7", "seven"),
         Choice("Doubles (6x)", "doubles")]
        string pick,
        [MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("dice", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "dice")) return;

        SetCooldown("dice");

        int d1 = Random.Shared.Next(1, 7);
        int d2 = Random.Shared.Next(1, 7);
        int total = d1 + d2;

        long payout = CreditHelper.DicePayout(pick, d1, d2, bet);
        bool won = payout > 0;

        long newBalance = ApplyGamble(bet, payout, "dice");

        string pickLabel = pick switch
        {
            "over" => "Over 7",
            "under" => "Under 7",
            "seven" => "Exactly 7",
            "doubles" => "Doubles",
            _ => pick
        };

        string outcomeText = won
            ? $"You picked **{pickLabel}** — correct! {CreditHelper.FormatDelta(payout - bet)}"
            : $"You picked **{pickLabel}** — rolled **{d1}+{d2}={total}**. {CreditHelper.FormatDelta(-bet)}";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎲  Dice Roll — {d1} + {d2} = **{total}**{(d1 == d2 ? " (doubles!)" : "")}")
            .WithColor(won ? ColourWin : ColourLoss)
            .WithDescription(outcomeText)
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("roulette", "Spin the roulette wheel!")]
    [EnabledInDm(false)]
    public async Task HandleRouletteAsync(
        [Choice("Red",    "red"),
         Choice("Black",  "black"),
         Choice("Even",   "even"),
         Choice("Odd",    "odd"),
         Choice("1-18",   "low"),
         Choice("19-36",  "high"),
         Choice("Number", "number")]
        string betType,
        [MinValue(10)] long bet,
        [MinValue(0), MaxValue(36)] int number = 0)
    {
        await DeferAsync();

        if (IsOnCooldown("roulette", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "roulette")) return;

        SetCooldown("roulette");

        string resolvedBet = betType == "number" ? number.ToString() : betType;

        int spin = CreditHelper.SpinRoulette();
        var (payout, result) = CreditHelper.CalculateRoulettePayout(spin, resolvedBet, bet);

        long newBalance = ApplyGamble(bet, payout, "roulette");
        bool won = payout > 0;

        // Prominent number display
        bool isRed = CreditHelper.RedNumbers.Contains(spin.ToString());
        string spinTitle = spin == 0
            ? "🟢 0 — Green!"
            : isRed ? $"🔴 {spin} — Red" : $"⚫ {spin} — Black";

        string betDesc = betType == "number"
            ? $"Bet on **{number}** — {result}"
            : $"Bet on **{betType}** — {result}";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎡  Roulette — {spinTitle}")
            .WithColor(won ? ColourWin : spin == 0 ? ColourPush : ColourLoss)
            .WithDescription(betDesc)
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("scratchcard", "Buy and scratch a card for instant prizes!")]
    [EnabledInDm(false)]
    public async Task HandleScratchCardAsync()
    {
        await DeferAsync();

        if (IsOnCooldown("scratchcard", out var cd)) { await CooldownAsync(cd); return; }

        long balance = _eco.GetBalance(UserId, ServerId);

        if (balance < CreditHelper.ScratchCardCost)
        {
            await ErrorAsync($"Scratch cards cost {CreditHelper.Format(CreditHelper.ScratchCardCost)}. You have {CreditHelper.Format(balance)}.");
            return;
        }

        SetCooldown("scratchcard");

        var (s1, s2, s3, payout, label) = CreditHelper.ScratchCard(CreditHelper.ScratchCardCost);
        long newBalance = ApplyGamble(CreditHelper.ScratchCardCost, payout, "scratchcard");
        bool won = payout > 0;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎟️  Scratch Card")
            .WithColor(won ? (label == "JACKPOT" ? ColourGold : ColourWin) : ColourLoss)
            .WithDescription(
                $"╔═══════════════╗\n" +
                $"║  {s1}  {s2}  {s3}  ║\n" +
                $"╚═══════════════╝\n\n" +
                (won
                    ? $"🎉 **{label}!** You win {CreditHelper.Format(payout)}!"
                    : $"**No match.** Better luck next time!"))
            .AddField("Cost", CreditHelper.Format(CreditHelper.ScratchCardCost), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("horses", "Bet on a horse race!")]
    [EnabledInDm(false)]
    public async Task HandleHorsesAsync(
        [Choice("Thunderbolt (favourite, 2x)",  "0"),
         Choice("Silver Wind (2.5x)",            "1"),
         Choice("Crimson Dawn (3.5x)",           "2"),
         Choice("Dark Matter (5x)",              "3"),
         Choice("Lucky Star (longshot, 8x)",     "4")]
        string horsePick,
        [MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("horses", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "horses")) return;

        SetCooldown("horses");

        int pick = int.Parse(horsePick);
        int winner = CreditHelper.RunRace();
        bool won = pick == winner;
        var horse = CreditHelper.Horses[pick];
        var winHorse = CreditHelper.Horses[winner];
        long payout = won ? (long)(bet * horse.odds) : 0;

        long newBalance = ApplyGamble(bet, payout, "horses");

        // Generate 3 frames: random mid-race positions → final result
        string[] place = ["🥇", "🥈", "🥉", "4️⃣", "5️⃣"];

        string RaceFrame(int[] positions, bool final)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < positions.Length; i++)
            {
                var h = CreditHelper.Horses[positions[i]];
                string arrow = final && positions[i] == winner ? " ← 🏆" : "";
                sb.AppendLine($"{place[i]} {h.emoji} **{h.name}**{arrow}");
            }
            return sb.ToString().TrimEnd();
        }

        // Frame 1 — scrambled mid-race order
        var midOrder = Enumerable.Range(0, CreditHelper.Horses.Length)
            .OrderBy(_ => Random.Shared.Next()).ToArray();
        var midMsg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🏇  Race in progress…")
            .WithColor(ColourInfo)
            .AddField("Current Standings", RaceFrame(midOrder, false))
            .WithDescription("*The horses are rounding the final bend!*")
            .WithFooter($"You backed {horse.emoji} {horse.name}")
            .Build());

        await Task.Delay(1400);

        // Frame 2 — another scramble
        var mid2 = Enumerable.Range(0, CreditHelper.Horses.Length)
            .OrderBy(_ => Random.Shared.Next()).ToArray();
        await midMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🏇  Final straight!")
            .WithColor(ColourInfo)
            .AddField("Current Standings", RaceFrame(mid2, false))
            .WithDescription("*It's neck and neck!*")
            .WithFooter($"You backed {horse.emoji} {horse.name}")
            .Build());

        await Task.Delay(1400);

        // Final — correct order with winner flagged
        var finalOrder = CreditHelper.BuildRaceResult(winner);
        await midMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle($"🏇  {winHorse.emoji} {winHorse.name} wins!")
            .WithColor(won ? ColourWin : ColourLoss)
            .AddField("Final Standings", RaceFrame(finalOrder, true))
            .WithDescription(
                won
                    ? $"🎉 Your horse **{horse.name}** won at **{horse.odds}x**! {CreditHelper.FormatDelta(payout - bet)}"
                    : $"Your horse **{horse.name}** didn't place. {CreditHelper.FormatDelta(-bet)}")
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("rps", "Play Rock Paper Scissors against the bot with a credit bet!")]
    [EnabledInDm(false)]
    public async Task HandleRpsAsync(
        [Choice("🪨 Rock",     "rock"),
         Choice("📄 Paper",    "paper"),
         Choice("✂️ Scissors", "scissors")]
        string pick,
        [MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("rps", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "rps")) return;

        SetCooldown("rps");

        string[] choices = ["rock", "paper", "scissors"];
        string botPick = choices[Random.Shared.Next(3)];

        bool won = (pick, botPick) is ("rock", "scissors") or ("paper", "rock") or ("scissors", "paper");
        bool draw = pick == botPick;

        long payout = draw ? bet : won ? (long)(bet * 1.9) : 0;
        long net = draw ? 0 : won ? payout - bet : -bet;

        // Draw: no money changes hands — skip ApplyGamble entirely but log it
        long newBalance;
        if (draw)
        {
            newBalance = _eco.GetBalance(UserId, ServerId);
            LogGamble("rps", bet, bet); // net 0
        }
        else
        {
            newBalance = ApplyGamble(bet, payout, "rps");
        }

        string pickEmoji = pick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string botEmoji = botPick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string outcome = draw ? "🤝 Draw!" : won ? "🎉 You win!" : "😔 Bot wins!";
        Color colour = draw ? ColourPush : won ? ColourWin : ColourLoss;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"✊  Rock Paper Scissors — {outcome}")
            .WithColor(colour)
            .WithDescription($"You: **{pickEmoji} {pick}** vs Bot: **{botEmoji} {botPick}**\n\n{CreditHelper.FormatDelta(net)}")
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("highlow", "Draw a card — guess if the next one is higher or lower!")]
    [EnabledInDm(false)]
    public async Task HandleHighLowAsync(
        [Choice("Higher", "higher"),
         Choice("Lower",  "lower")]
        string guess,
        [MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("highlow", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "highlow")) return;

        SetCooldown("highlow");

        var (card1Display, card1Value) = CreditHelper.DrawCard();
        var (card2Display, card2Value) = CreditHelper.DrawCard();

        bool higher = card2Value > card1Value;
        bool lower = card2Value < card1Value;
        bool tie = card2Value == card1Value;

        bool won = (guess == "higher" && higher) || (guess == "lower" && lower);
        long payout = tie ? bet : won ? (long)(bet * 1.9) : 0; // tie = push
        long net = tie ? 0 : won ? payout - bet : -bet;

        long newBalance;
        if (tie)
        {
            newBalance = _eco.GetBalance(UserId, ServerId);
            LogGamble("highlow", bet, bet);
        }
        else
        {
            newBalance = ApplyGamble(bet, payout, "highlow");
        }

        string outcomeText = tie
            ? $"🤝 **Tie!** Both cards are **{card1Display}** — push, no money changes hands."
            : won
                ? $"✅ **Correct!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}"
                : $"❌ **Wrong!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🃏  High-Low — {(tie ? "Push" : won ? "You Win!" : "You Lose!")}")
            .WithColor(tie ? ColourPush : won ? ColourWin : ColourLoss)
            .WithDescription(
                $"**First card:** `{card1Display}`\n" +
                $"**Second card:** `{card2Display}`\n\n" +
                $"You guessed **{guess}** — {outcomeText}")
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("jackpot", "Contribute to the server jackpot — drawn every hour!")]
    [EnabledInDm(false)]
    public async Task HandleJackpotAsync([MinValue(10)] long amount)
    {
        await DeferAsync();

        long balance = _eco.GetBalance(UserId, ServerId);
        if (amount > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return;
        }

        // Deduct entry fee
        _eco.DeductCredits(UserId, ServerId, amount, "jackpot_entry");

        // Record entry
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddJackpotEntry",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@Amount",   amount)
        ]);

        // Show current pot
        var potDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetJackpotTotal",
            [new SqlParameter("@ServerID", ServerId)]);

        long pot = potDt.Rows.Count > 0 ? long.Parse(potDt.Rows[0]["Total"].ToString()!) : amount;
        int entries = potDt.Rows.Count > 0 ? int.Parse(potDt.Rows[0]["Entries"].ToString()!) : 1;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎰  Jackpot Entry Confirmed!")
            .WithColor(ColourGold)
            .WithDescription(
                $"{Context.User.Mention} entered **{CreditHelper.Format(amount)}** into the jackpot!\n\n" +
                $"💰 **Current Pot:** {CreditHelper.Format(pot)}\n" +
                $"🎟️ **Total Entries:** {entries}\n\n" +
                $"*The winner is drawn every hour — weighted by contribution amount.*")
            .WithFooter("More entries = better odds!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("transfer", "Send credits to another user.")]
    [EnabledInDm(false)]
    public async Task HandleTransferAsync(
        IUser recipient,
        [MinValue(1)] long amount)
    {
        await DeferAsync();

        if (recipient.Id == Context.User.Id)
        {
            await ErrorAsync("You can't transfer credits to yourself.");
            return;
        }

        if (recipient.IsBot)
        {
            await ErrorAsync("Bots don't have credit accounts.");
            return;
        }

        long balance = _eco.GetBalance(UserId, ServerId);
        if (amount > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return;
        }

        _eco.DeductCredits(UserId, ServerId, amount, $"transfer_to_{recipient.Id}");
        _eco.AddCredits(recipient.Id.ToString(), ServerId, amount, $"transfer_from_{UserId}");

        long newBalance = _eco.GetBalance(UserId, ServerId);
        long theirBalance = _eco.GetBalance(recipient.Id.ToString(), ServerId);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("💸  Transfer Complete")
            .WithColor(ColourWin)
            .WithDescription(
                $"{Context.User.Mention} sent {CreditHelper.Format(amount)} to {recipient.Mention}!")
            .AddField("Your Balance", CreditHelper.Format(newBalance), inline: true)
            .AddField("Their Balance", CreditHelper.Format(theirBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("gamblestats", "View your gambling statistics.")]
    [EnabledInDm(false)]
    public async Task HandleGambleStatsAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        string tId = target.Id.ToString();
        bool isSelf = target.Id == Context.User.Id;

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetGambleStats",
        [
            new SqlParameter("@UserID",   tId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"📊  {target.Username}'s Gambling Stats")
                .WithColor(ColourInfo)
                .WithDescription("No gambling history yet! Try `/slots` or `/coinflip`.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        long totalWagered = 0, totalNet = 0, biggestWin = 0, biggestLoss = 0;
        int totalGames = 0, totalWins = 0, totalLosses = 0;
        var gameLines = new System.Text.StringBuilder();

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string game = row["Game"].ToString()!;
            int games = int.Parse(row["GamesPlayed"].ToString()!);
            int wins = int.Parse(row["Wins"].ToString()!);
            int losses = int.Parse(row["Losses"].ToString()!);
            long wagered = long.Parse(row["TotalWagered"].ToString()!);
            long net = long.Parse(row["NetTotal"].ToString()!);
            long bWin = long.Parse(row["BiggestWin"].ToString()!);
            long bLoss = long.Parse(row["BiggestLoss"].ToString()!);

            totalWagered += wagered;
            totalNet += net;
            totalGames += games;
            totalWins += wins;
            totalLosses += losses;
            if (bWin > biggestWin) biggestWin = bWin;
            if (bLoss < biggestLoss) biggestLoss = bLoss;

            string winRate = games > 0 ? $"{(wins * 100 / games)}%" : "—";
            string netStr = net >= 0
                ? $"+{CreditHelper.CurrencyEmoji} {net:N0}"
                : $"-{CreditHelper.CurrencyEmoji} {Math.Abs(net):N0}";

            gameLines.AppendLine($"**{game}** — {games} games, {winRate} win rate, {netStr} net");
        }

        string overallNet = totalNet >= 0
            ? $"+{CreditHelper.CurrencyEmoji} {totalNet:N0}"
            : $"-{CreditHelper.CurrencyEmoji} {Math.Abs(totalNet):N0}";

        int overallWinRate = totalGames > 0 ? totalWins * 100 / totalGames : 0;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📊  {target.Username}'s Gambling Stats")
            .WithColor(totalNet >= 0 ? ColourWin : ColourLoss)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .AddField("Total Wagered", CreditHelper.Format(totalWagered), inline: true)
            .AddField("Overall Net", overallNet, inline: true)
            .AddField("Win Rate", $"{overallWinRate}% ({totalWins}W/{totalLosses}L)", inline: true)
            .AddField("Biggest Win", CreditHelper.Format(biggestWin), inline: true)
            .AddField("Biggest Loss", CreditHelper.Format(Math.Abs(biggestLoss)), inline: true)
            .AddField("Total Games", $"{totalGames:N0}", inline: true)
            .AddField("Breakdown", gameLines.ToString().TrimEnd(), inline: false)
            .WithFooter(isSelf ? Username : $"Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("fish", "Cast your line and see what you catch!")]
    [EnabledInDm(false)]
    public async Task HandleFishAsync()
    {
        await DeferAsync();

        string cooldownKey = $"{UserId}:fish";
        if (_cooldowns.TryGetValue(cooldownKey, out var lastFish))
        {
            var fishElapsed = DateTime.UtcNow - lastFish;
            var fishCooldown = TimeSpan.FromMinutes(CreditHelper.FishCooldownMinutes);
            if (fishElapsed < fishCooldown)
            {
                var remaining = fishCooldown - fishElapsed;
                int m = (int)remaining.TotalMinutes;
                int s = remaining.Seconds;
                await FollowupAsync(embed: _embed.BuildErrorEmbed("Fishing",
                    $"Your line is still in the water! Try again in **{m}m {s}s**.", Username).Build(),
                    ephemeral: true);
                return;
            }
        }
        _cooldowns[cooldownKey] = DateTime.UtcNow;

        var (name, emoji, credits, flavour) = CreditHelper.CastLine();

        long newBalance = _eco.GetBalance(UserId, ServerId);
        if (credits > 0)
        {
            _eco.AddCredits(UserId, ServerId, credits, "fishing");
            newBalance = _eco.GetBalance(UserId, ServerId);
        }

        Color colour = credits switch
        {
            0 => ColourLoss,
            < 100 => ColourInfo,
            < 500 => ColourWin,
            _ => ColourGold
        };

        var eb = new EmbedBuilder()
            .WithTitle($"{emoji}  Fishing — {name}")
            .WithColor(colour)
            .WithDescription(flavour)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

        if (credits > 0)
            eb.AddField("Caught", $"{emoji} {name}", inline: true)
              .AddField("Reward", CreditHelper.Format(credits), inline: true)
              .AddField("Balance", CreditHelper.Format(newBalance), inline: true);

        await FollowupAsync(embed: eb.Build());
    }


    [SlashCommand("bigwheel", "Spin the Big Wheel and multiply your bet!")]
    [EnabledInDm(false)]
    public async Task HandleBigWheelAsync([MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("bigwheel", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "bigwheel")) return;

        SetCooldown("bigwheel");

        int winIdx = CreditHelper.SpinWheel();
        var (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];

        // If the spin landed on BANKRUPT and the user has a shield active, re-spin once.
        string? shieldNote = null;
        if (wMult == 0.0 && ShopHelper.HasActiveEffect(UserId, ServerId, "bk_shield"))
        {
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "bk_shield");
            winIdx = CreditHelper.SpinWheel();
            (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];
            shieldNote = "🛡️ **Bankrupt Shield** blocked the BANKRUPT and re-spun!";
        }

        long payout = (long)(bet * wMult);
        long newBalance = ApplyGamble(bet, payout, "bigwheel");

        // If the spin was a loss AND the user has insurance, refund 50% of bet.
        string? insuranceNote = null;
        if (payout < bet && ShopHelper.HasActiveEffect(UserId, ServerId, "insurance"))
        {
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "insurance");
            long refund = bet / 2;
            newBalance = _eco.AddCredits(UserId, ServerId, refund, "insurance_refund");
            payout += refund;
            insuranceNote = $"📋 **Gamble Insurance** refunded {CreditHelper.Format(refund)}!";
        }

        int total = CreditHelper.WheelSegments.Length;
        bool won = payout > bet;
        bool push = payout == bet;
        Color final = payout == 0 ? ColourLoss : won ? ColourWin : push ? ColourPush : ColourLoss;

        // 12 spin frames approaching winIdx from 3 full rotations out.
        // Delays follow a cubic ease-out: short at start, stretch toward the end.
        int overshoot = total * 3 + winIdx;

        // offsets from overshoot (negative = earlier in the spin)
        // delays in ms — short bursts early, long pauses near the end
        (int posOffset, int delayMs, string status)[] frames =
        [
            (-11, 180, "🌀  Spinning…"),
            (-10, 180, "🌀  Spinning…"),
            ( -9, 200, "🌀  Spinning…"),
            ( -8, 220, "💨  Spinning…"),
            ( -7, 260, "💨  Spinning…"),
            ( -6, 320, "💨  Slowing…"),
            ( -5, 390, "😮  Slowing…"),
            ( -4, 470, "😮  Almost…"),
            ( -3, 560, "👀  Almost there…"),
            ( -2, 680, "🤞  Any second…"),
            ( -1, 820, "🤞  Come on…"),
        ];

        EmbedBuilder SpinFrame(int pos, string status, Color? colour = null) =>
            new EmbedBuilder()
                .WithTitle("🎡  Big Wheel — Spinning!")
                .WithColor(colour ?? ColourInfo)
                .WithDescription(
                    CreditHelper.BuildWheelDisplay(((pos % total) + total) % total) +
                    $"\n*{status}*")
                .WithFooter($"Bet: {CreditHelper.Format(bet)} • {Username}", AvatarUrl);

        var msg = await FollowupAsync(embed: SpinFrame(overshoot + frames[0].posOffset, frames[0].status).Build());

        foreach (var (posOffset, delayMs, status) in frames[1..])
        {
            await Task.Delay(delayMs);
            await msg.ModifyAsync(m => m.Embed = SpinFrame(overshoot + posOffset, status).Build());
        }

        var resultEmbed = new EmbedBuilder()
            .WithTitle($"🎡  {wEmoji}  {wLabel}!")
            .WithColor(final)
            .WithDescription(CreditHelper.BuildWheelDisplay(winIdx))
            .AddField("Bet", CreditHelper.Format(bet), inline: true)
            .AddField("Multiplier", wLabel, inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

        if (shieldNote is not null || insuranceNote is not null)
        {
            string extra = string.Join("\n", new[] { shieldNote, insuranceNote }.Where(n => n is not null)!);
            resultEmbed.WithDescription(CreditHelper.BuildWheelDisplay(winIdx) + $"\n\n{extra}");
        }

        await Task.Delay(1100);
        await msg.ModifyAsync(m => m.Embed = resultEmbed.Build());
    }


    [SlashCommand("invest", "Lock away credits for 24 hours — collect your return when they mature.")]
    [EnabledInDm(false)]
    public async Task HandleInvestAsync([MinValue(100)] long amount = 0)
    {
        await DeferAsync();

        // Check if user has a pending investment ready to collect
        var pendingDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPendingInvestment",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (pendingDt.Rows.Count > 0)
        {
            var row = pendingDt.Rows[0];
            int invId = int.Parse(row["InvestmentID"].ToString()!);
            long invAmt = long.Parse(row["Amount"].ToString()!);
            var returnsAt = DateTime.Parse(row["ReturnsAt"].ToString()!);

            if (DateTime.UtcNow >= returnsAt)
            {
                // Ready — collect
                decimal mult = decimal.Parse(row["Multiplier"].ToString()!);
                long payout = (long)(invAmt * mult);
                long profit = payout - invAmt;
                var (_, _, label) = CreditHelper.InvestOutcomes.First(o => o.multiplier == mult);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ClaimInvestment",
                [
                    new SqlParameter("@InvestmentID", invId),
                    new SqlParameter("@UserID",        UserId)
                ]);

                _eco.AddCredits(UserId, ServerId, payout, "invest_return");
                long newBalance = _eco.GetBalance(UserId, ServerId);

                string outcomeEmoji = mult >= 1.5m ? "🚀" : mult >= 1.0m ? "📈" : "📉";
                Color colour = mult >= 1.0m ? ColourWin : ColourLoss;

                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle($"{outcomeEmoji}  Investment Matured!")
                    .WithColor(colour)
                    .WithDescription(
                        $"{label}\n\n" +
                        $"Your {CreditHelper.Format(invAmt)} investment returned **{mult:0.00}×**.")
                    .AddField("Invested", CreditHelper.Format(invAmt), inline: true)
                    .AddField("Return", CreditHelper.Format(payout), inline: true)
                    .AddField("Profit", CreditHelper.FormatDelta(profit), inline: true)
                    .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
                return;
            }

            // Still pending — show status
            var timeLeft = returnsAt - DateTime.UtcNow;
            string tlStr = $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";

            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("💼  Investment Pending")
                .WithColor(ColourInfo)
                .WithDescription(
                    $"Your investment of {CreditHelper.Format(invAmt)} is still maturing.\n\n" +
                    $"⏳ Returns in **{tlStr}**\n\n" +
                    $"Run `/invest` again when it's ready to collect!")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        // No pending investment — create a new one
        if (amount <= 0)
        {
            await ErrorAsync("Specify an amount to invest, e.g. `/invest 1000`.");
            return;
        }

        long balance = _eco.GetBalance(UserId, ServerId);
        if (amount > balance)
        {
            await ErrorAsync($"You only have {CreditHelper.Format(balance)}.");
            return;
        }

        var (mult2, label2) = CreditHelper.RollInvestment();   // multiplier hidden until collect
        var returnsAt2 = DateTime.UtcNow.AddHours(24);

        _eco.DeductCredits(UserId, ServerId, amount, "invest_lock");

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddInvestment",
        [
            new SqlParameter("@UserID",      UserId),
            new SqlParameter("@ServerID",    ServerId),
            new SqlParameter("@Amount",      amount),
            new SqlParameter("@Multiplier",  mult2),
            new SqlParameter("@ReturnsAt",   returnsAt2)
        ]);

        long remaining2 = _eco.GetBalance(UserId, ServerId);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("💼  Investment Locked In!")
            .WithColor(ColourGold)
            .WithDescription(
                $"You've invested {CreditHelper.Format(amount)} — the market will do its thing.\n\n" +
                $"⏳ Returns in **24 hours** — run `/invest` to collect.\n" +
                $"*(Your return is sealed but hidden until you collect.)*")
            .AddField("Invested", CreditHelper.Format(amount), inline: true)
            .AddField("Matures", $"<t:{new DateTimeOffset(returnsAt2).ToUnixTimeSeconds()}:R>", inline: true)
            .AddField("Balance", CreditHelper.Format(remaining2), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    /// <summary>
    /// Validates bet, checks daily loss limit, and returns false with an error if invalid.
    /// Respects the mega_bet active effect which temporarily raises the max bet cap to 100k.
    /// </summary>
    private async Task<bool> ValidateBet(long bet, string game)
    {
        long balance = _eco.GetBalance(UserId, ServerId);

        bool hasMegaBet = ShopHelper.HasActiveEffect(UserId, ServerId, "mega_bet");
        long effectiveCap = hasMegaBet ? 1100000000000 : CreditHelper.MaxBet;

        if (bet < CreditHelper.MinBet)
        {
            await ErrorAsync($"Minimum bet is {CreditHelper.Format(CreditHelper.MinBet)}.");
            return false;
        }
        if (bet > effectiveCap)
        {
            string capNote = hasMegaBet
                ? $"Maximum bet is {CreditHelper.Format(effectiveCap)} *(Bet Limit Booster active)*."
                : $"Maximum bet is {CreditHelper.Format(effectiveCap)}.";
            await ErrorAsync(capNote);
            return false;
        }
        if (bet > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return false;
        }

        // Daily loss limit check
        var lossRow = _sp.Select(Constants.Constants.discordBotConnStr, "GetDailyLoss",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        long dailyLost = lossRow.Rows.Count > 0
            ? long.Parse(lossRow.Rows[0]["TotalLost"].ToString()!)
            : 0;

        //if (dailyLost >= CreditHelper.DailyLossLimit)
        //{
        //    await ErrorAsync(
        //        $"You've hit the daily loss limit of {CreditHelper.Format(CreditHelper.DailyLossLimit)}.\n" +
        //        $"Come back tomorrow — your limit resets every 24 hours.");
        //    return false;
        //}

        return true;
    }

    /// <summary>
    /// Clears all in-memory gambling cooldowns for a specific user.
    /// Called by <c>/shop use cd_reset</c>.
    /// </summary>
    public static void ClearUserCooldowns(string userId)
    {
        var keys = _cooldowns.Keys
            .Where(k => k.StartsWith(userId + ":"))
            .ToList();
        foreach (var key in keys)
            _cooldowns.TryRemove(key, out _);
    }

    /// <summary>
    /// Deducts bet, adds payout, logs to GambleLog. Returns new balance.
    /// </summary>
    private long ApplyGamble(long bet, long payout, string game)
    {
        _eco.DeductCredits(UserId, ServerId, bet, "gamble");
        if (payout > 0)
            _eco.AddCredits(UserId, ServerId, payout, "gamble_win");

        LogGamble(game, bet, payout);

        return _eco.GetBalance(UserId, ServerId);
    }

    /// <summary>Write a gamble result to the log (used for draws/pushes that skip ApplyGamble).</summary>
    private void LogGamble(string game, long bet, long payout)
    {
        try
        {
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddGambleLog",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@Game",     game),
                new SqlParameter("@Bet",      bet),
                new SqlParameter("@Payout",   payout)
            ]);
        }
        catch { /* log failure is non-fatal */ }
    }

    private async Task CooldownAsync(TimeSpan remaining) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed(
            "Gambling",
            $"Slow down! Try again in **{remaining.Seconds}s**.",
            Username).Build(), ephemeral: true);

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Gambling", message, Username).Build());
}
