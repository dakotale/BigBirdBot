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

    // Key: "userId:serverId"  Value: consecutive loss / win count
    private static readonly ConcurrentDictionary<string, int> _lossStreaks = new();
    private static readonly ConcurrentDictionary<string, int> _winStreaks = new();

    // ── Lucky Mode ────────────────────────────────────────────────────────────
    // When active for a server, all losses are re-rolled at 60% win / 40% loss.
    // Toggle via /lucky — owner only.
    private static readonly HashSet<string> _luckyServers = new();
    private const ulong LuckyOwnerID = 171369791486033920UL;

    // ── Double-or-Nothing ─────────────────────────────────────────────────────
    // Key: "userId:serverId"  Value: (net win to double-or-lose, expiry)
    private static readonly ConcurrentDictionary<string, (decimal amount, DateTime expiry)> _donOffers = new();
    private const string BtnDonAccept = "don:accept";
    private const string BtnDonDecline = "don:decline";
    private static readonly TimeSpan DonWindow = TimeSpan.FromSeconds(30);

    private MessageComponent DonButtons() =>
        new ComponentBuilder()
            .WithButton("Double it!", BtnDonAccept, ButtonStyle.Success, new Emoji("⚡"), row: 0)
            .WithButton("Take winnings", BtnDonDecline, ButtonStyle.Secondary, new Emoji("💰"), row: 0)
            .Build();

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

        bool slotChaos = ShopHelper.HasActiveEffect(UserId, ServerId, "chaos_card");
        if (slotChaos) ShopHelper.ConsumeActiveEffect(UserId, ServerId, "chaos_card");

        string r1 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();
        string r2 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();
        string r3 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();

        var (payout, result) = CreditHelper.CalculateSlotPayout(r1, r2, r3, (decimal)bet);
        if (slotChaos) result = "🃏 " + result;
        decimal newBalance = ApplyGamble((decimal)bet, payout, "slots");

        // Passive jackpot — 0.5% chance on every spin
        var (pjWon, pjAmount) = await TryClaimPassiveJackpotAsync();
        if (pjWon) newBalance = _eco.GetBalance(UserId, ServerId);

        // Challenge tracking
        if (payout > 0m)
        {
            TrackChallenge("slots");
            if (r1 == r2 && r2 == r3 && r1 == "💎") TrackChallenge("slots_jack");
        }

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
        await msg.ModifyAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle("🎰  Slot Machine")
                .WithColor(payout >= (decimal)bet ? ColourWin : payout > 0m ? ColourPush : ColourLoss)
                .WithDescription(
                    $"╔══════════════╗\n" +
                    $"║  {r1}  {r2}  {r3}  ║\n" +
                    $"╚══════════════╝\n\n" +
                    $"**{result}**" +
                    (pjWon ? $"\n\n🎰 **PASSIVE JACKPOT!** You hit the server pool for **{CreditHelper.Format(pjAmount)}**!" : ""))
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build();
            decimal netWin = payout - (decimal)bet;
            if (!pjWon && netWin > 0m)
                m.Components = OfferDon(netWin);
        });
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
        decimal payout = won ? (decimal)bet * 1.9m : 0m;

        decimal newBalance = ApplyGamble((decimal)bet, payout, "coinflip");
        if (won) TrackChallenge("coinflip");

        string coinEmoji = result == "heads" ? "🪙" : "⚫";
        decimal netWinCf = payout - (decimal)bet;
        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle($"{coinEmoji}  Coin Flip — {char.ToUpper(result[0])}{result[1..]}")
                .WithColor(won ? ColourWin : ColourLoss)
                .WithDescription(
                    won
                        ? $"You called **{side}** — correct! {CreditHelper.FormatDelta(payout - (decimal)bet)}"
                        : $"You called **{side}** — it was **{result}**. {CreditHelper.FormatDelta(-(decimal)bet)}")
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(),
            components: won ? OfferDon(netWinCf) : null);
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

        decimal payout = CreditHelper.DicePayout(pick, d1, d2, (decimal)bet);
        bool won = payout > 0m;

        decimal newBalance = ApplyGamble((decimal)bet, payout, "dice");
        if (won) TrackChallenge("dice");

        string pickLabel = pick switch
        {
            "over" => "Over 7",
            "under" => "Under 7",
            "seven" => "Exactly 7",
            "doubles" => "Doubles",
            _ => pick
        };

        string outcomeText = won
            ? $"You picked **{pickLabel}** — correct! {CreditHelper.FormatDelta(payout - (decimal)bet)}"
            : $"You picked **{pickLabel}** — rolled **{d1}+{d2}={total}**. {CreditHelper.FormatDelta(-(decimal)bet)}";

        await FollowupAsync(
            embed: new EmbedBuilder()
                    .WithTitle($"🎲  Dice Roll — {d1} + {d2} = **{total}**{(d1 == d2 ? " (doubles!)" : "")}")
                    .WithColor(won ? ColourWin : ColourLoss)
                    .WithDescription(outcomeText)
                    .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                    .AddField("Payout", CreditHelper.Format(payout), inline: true)
                    .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build(),
                components: won ? OfferDon(payout - (decimal)bet) : null);
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
        var (payout, result) = CreditHelper.CalculateRoulettePayout(spin, resolvedBet, (decimal)bet);

        decimal newBalance = ApplyGamble((decimal)bet, payout, "roulette");
        bool won = payout > 0m;
        if (won) TrackChallenge("roulette");

        bool isRed = CreditHelper.RedNumbers.Contains(spin.ToString());
        string spinTitle = spin == 0
            ? "🟢 0 — Green!"
            : isRed ? $"🔴 {spin} — Red" : $"⚫ {spin} — Black";

        string betDesc = betType == "number"
            ? $"Bet on **{number}** — {result}"
            : $"Bet on **{betType}** — {result}";

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle($"🎡  Roulette — {spinTitle}")
                .WithColor(won ? ColourWin : spin == 0 ? ColourPush : ColourLoss)
                .WithDescription(betDesc)
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(),
            components: won ? OfferDon(payout - (decimal)bet) : null);
    }


    [SlashCommand("scratchcard", "Buy and scratch a card for instant prizes!")]
    [EnabledInDm(false)]
    public async Task HandleScratchCardAsync()
    {
        await DeferAsync();

        if (IsOnCooldown("scratchcard", out var cd)) { await CooldownAsync(cd); return; }

        decimal balance = _eco.GetBalance(UserId, ServerId);
        if (balance < CreditHelper.ScratchCardCost)
        {
            await ErrorAsync($"Scratch cards cost {CreditHelper.Format(CreditHelper.ScratchCardCost)}. You have {CreditHelper.Format(balance)}.");
            return;
        }

        SetCooldown("scratchcard");

        bool scratchChaos = ShopHelper.HasActiveEffect(UserId, ServerId, "chaos_card");
        if (scratchChaos) ShopHelper.ConsumeActiveEffect(UserId, ServerId, "chaos_card");

        var (s1, s2, s3, payout, label) = scratchChaos
            ? CreditHelper.ScratchCardChaos(CreditHelper.ScratchCardCost)
            : CreditHelper.ScratchCard(CreditHelper.ScratchCardCost);
        bool won = payout > 0m;
        bool jackpot = label == "JACKPOT";

        // ── Card renderer ──────────────────────────────────────────────────────
        // r1/r2/r3: the revealed symbol or "❓" if still hidden
        static string Card(string r1, string r2, string r3) =>
    $"```\n" +
    $"╔════════════════╗\n" +
    $"║  SCRATCH CARD  ║\n" +
    $"╠════════════════╣\n" +
    $"║                ║\n" +
    $"║   {r1}  {r2}  {r3}   ║\n" +
    $"║                ║\n" +
    $"╚════════════════╝\n" +
    $"```";

        // ── Phase 1: Unscratched card ──────────────────────────────────────────
        var msg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎟️  Scratch Card")
            .WithColor(ColourInfo)
            .WithDescription(Card("❓", "❓", "❓") + "\n*Scratching…*")
            .WithFooter(Username, AvatarUrl)
            .Build());

        // ── Phase 2: Reveal one symbol at a time ──────────────────────────────
        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🎟️  Scratch Card")
            .WithColor(ColourInfo)
            .WithDescription(Card(s1, "❓", "❓") + "\n*Scratching…*")
            .WithFooter(Username, AvatarUrl)
            .Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🎟️  Scratch Card")
            .WithColor(ColourInfo)
            .WithDescription(Card(s1, s2, "❓") + $"\n{(s1 == s2 ? "*Match so far… 👀*" : "*No match yet…*")}")
            .WithFooter(Username, AvatarUrl)
            .Build());

        await Task.Delay(900);

        // ── Phase 3: Final reveal ──────────────────────────────────────────────
        decimal newBalance = ApplyGamble(CreditHelper.ScratchCardCost, payout, "scratchcard");

        // Passive jackpot — 0.5% chance on every card
        var (pjWon, pjAmount) = await TryClaimPassiveJackpotAsync();
        if (pjWon) newBalance = _eco.GetBalance(UserId, ServerId);
        if (won) TrackChallenge("scratch");

        Color colour = jackpot ? ColourGold : won ? ColourWin : ColourLoss;

        string resultLine = won
            ? jackpot
                ? $"💰 **JACKPOT!** {CreditHelper.Format(payout)}!"
                : $"🎉 **{label}!** You win {CreditHelper.Format(payout)}!"
            : "💨 **No match.** Better luck next time!";

        string nextCastNote = $"-# ⏱ Cooldown: {CreditHelper.Format(CreditHelper.ScratchCardCost)} per card.";
        string pjNote = pjWon
            ? $"\n🎰 **PASSIVE JACKPOT!** You hit the server pool for **{CreditHelper.Format(pjAmount)}**!"
            : "";

        await msg.ModifyAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle(pjWon ? "🎰  PASSIVE JACKPOT!" : won ? (jackpot ? "💰  JACKPOT!" : "🎉  Winner!") : "🎟️  No Match")
                .WithColor(pjWon ? ColourGold : colour)
                .WithDescription(Card(s1, s2, s3) + $"\n{resultLine}{pjNote}\n\n{nextCastNote}")
                .AddField("Cost", CreditHelper.Format(CreditHelper.ScratchCardCost), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build();
            if (won && !pjWon) m.Components = OfferDon(payout - CreditHelper.ScratchCardCost);
        });
    }


    [SlashCommand("horses", "Bet on a horse race!")]
    [EnabledInDm(false)]
    public async Task HandleHorsesAsync(
    [Choice("Thunderbolt (favourite, 2×)",  "0"),
     Choice("Silver Wind (2.5×)",           "1"),
     Choice("Crimson Dawn (3.5×)",          "2"),
     Choice("Iron Fist (5×)",               "3"),
     Choice("Dark Matter (7×)",             "4"),
     Choice("Lucky Star (12×)",             "5"),
     Choice("Ghost Rider (25×)",            "6"),
     Choice("Miracle Run (longshot, 50×)",  "7")]
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
        decimal payout = won ? (decimal)bet * (decimal)horse.odds : 0m;
        decimal newBalance = ApplyGamble((decimal)bet, payout, "horses");
        if (won)
        {
            TrackChallenge("horses");
            if (horse.odds >= 7.0) TrackChallenge("horses_h");
        }
        int total = CreditHelper.Horses.Length;
        const int tWidth = 14;

        // Renders a race track. progress[i] = 0.0–1.0 for each horse.
        string TrackFrame(double[] progress, bool final)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("```");
            sb.AppendLine("     ╔════════════════╗ FINISH");
            for (int i = 0; i < total; i++)
            {
                var h = CreditHelper.Horses[i];
                int pos = Math.Clamp((int)Math.Round(progress[i] * tWidth), 0, tWidth);
                string track = new string('·', pos) + h.emoji + new string('·', tWidth - pos);
                string suffix = final && i == winner ? " 🏆" : i == pick ? " 📍" : "";
                sb.AppendLine($"  {track}  {h.name}{suffix}");
            }
            sb.AppendLine("     ╚════════════════╝");
            sb.AppendLine("```");
            return sb.ToString();
        }

        // Random mid-race spread, winner biased into the given range
        double[] Frame(double wMin, double wMax)
        {
            var p = new double[total];
            for (int i = 0; i < total; i++)
                p[i] = 0.05 + Random.Shared.NextDouble() * 0.55;
            p[winner] = wMin + Random.Shared.NextDouble() * (wMax - wMin);
            return p;
        }

        // Final frame — winner at 100%, others scattered behind
        var finalProg = new double[total];
        for (int i = 0; i < total; i++)
            finalProg[i] = i == winner ? 1.0 : 0.25 + Random.Shared.NextDouble() * 0.65;

        string footer = $"You backed: {horse.emoji} {horse.name} ({horse.odds}×)";

        // ── Frame 1 ────────────────────────────────────────────────────────────
        var msg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🏇  And they're off!")
            .WithColor(ColourInfo)
            .WithDescription(TrackFrame(Frame(0.1, 0.3), false) + "*The gates fly open!*")
            .WithFooter(footer, AvatarUrl)
            .Build());

        // ── Frame 2 ────────────────────────────────────────────────────────────
        await Task.Delay(1200);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🏇  Rounding the bend…")
            .WithColor(ColourInfo)
            .WithDescription(TrackFrame(Frame(0.35, 0.6), false) + "*Jostling for position!*")
            .WithFooter(footer, AvatarUrl)
            .Build());

        // ── Frame 3 ────────────────────────────────────────────────────────────
        await Task.Delay(1200);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🏇  Final straight!")
            .WithColor(ColourInfo)
            .WithDescription(TrackFrame(Frame(0.65, 0.88), false) + "*It's neck and neck!*")
            .WithFooter(footer, AvatarUrl)
            .Build());

        // ── Final result ───────────────────────────────────────────────────────
        await Task.Delay(1200);
        var winHorse = CreditHelper.Horses[winner];
        string result = won
            ? $"🎉 **{horse.name}** wins at **{horse.odds}×**! {CreditHelper.FormatDelta(payout - (decimal)bet)}"
            : $"**{winHorse.name}** takes the win. Your horse didn't place. {CreditHelper.FormatDelta(-(decimal)bet)}";

        await msg.ModifyAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle($"🏆  {winHorse.emoji} {winHorse.name} wins the race!")
                .WithColor(won ? ColourWin : ColourLoss)
                .WithDescription(TrackFrame(finalProg, true) + $"\n{result}")
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
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

        decimal payout = draw ? (decimal)bet : won ? (decimal)bet * 1.9m : 0m;
        decimal net = draw ? 0m : won ? payout - (decimal)bet : -(decimal)bet;

        decimal newBalance;
        if (draw)
        {
            newBalance = _eco.GetBalance(UserId, ServerId);
            LogGamble("rps", (decimal)bet, (decimal)bet);
        }
        else
        {
            newBalance = ApplyGamble((decimal)bet, payout, "rps");
            if (won) TrackChallenge("rps");
        }

        string pickEmoji = pick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string botEmoji = botPick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string outcome = draw ? "🤝 Draw!" : won ? "🎉 You win!" : "😔 Bot wins!";
        Color colour = draw ? ColourPush : won ? ColourWin : ColourLoss;

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle($"✊  Rock Paper Scissors — {outcome}")
                .WithColor(colour)
                .WithDescription($"You: **{pickEmoji} {pick}** vs Bot: **{botEmoji} {botPick}**\n\n{CreditHelper.FormatDelta(net)}")
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(),
            components: won ? OfferDon(payout - (decimal)bet) : null);
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
        decimal payout = tie ? (decimal)bet : won ? (decimal)bet * 1.9m : 0m;
        decimal net = tie ? 0m : won ? payout - (decimal)bet : -(decimal)bet;

        decimal newBalance;
        if (tie)
        {
            newBalance = _eco.GetBalance(UserId, ServerId);
            LogGamble("highlow", (decimal)bet, (decimal)bet);
        }
        else
        {
            newBalance = ApplyGamble((decimal)bet, payout, "highlow");
            if (won) TrackChallenge("highlow");
        }

        string outcomeText = tie
            ? $"🤝 **Tie!** Both cards are **{card1Display}** — push, no money changes hands."
            : won
                ? $"✅ **Correct!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}"
                : $"❌ **Wrong!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}";

        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle($"🃏  High-Low — {(tie ? "Push" : won ? "You Win!" : "You Lose!")}")
                .WithColor(tie ? ColourPush : won ? ColourWin : ColourLoss)
                .WithDescription(
                    $"**First card:** `{card1Display}`\n" +
                    $"**Second card:** `{card2Display}`\n\n" +
                    $"You guessed **{guess}** — {outcomeText}")
                .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
                .AddField("Payout", CreditHelper.Format(payout), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build(),
            components: won ? OfferDon(payout - (decimal)bet) : null);
    }


    [SlashCommand("jackpot", "View jackpot pools or contribute to the entry jackpot.")]
    [EnabledInDm(false)]
    public async Task HandleJackpotAsync([MinValue(10)] long? amount = null)
    {
        await DeferAsync();

        // ── Fetch both pools ───────────────────────────────────────────────────
        var potDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetJackpotTotal",
            [new SqlParameter("@ServerID", ServerId)]);
        decimal entryPot = potDt.Rows.Count > 0
            ? decimal.Parse(potDt.Rows[0]["Total"].ToString()!)
            : 0m;
        int entries = potDt.Rows.Count > 0
            ? int.Parse(potDt.Rows[0]["Entries"].ToString()!)
            : 0;

        long.TryParse(ServerId, out long jpServerId);
        var passiveDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPassiveJackpot",
            [new SqlParameter("@ServerID", jpServerId)]);
        decimal passivePot = passiveDt.Rows.Count > 0
            ? decimal.Parse(passiveDt.Rows[0]["Pool"].ToString()!)
            : 0m;

        // ── View-only (no amount given) ────────────────────────────────────────
        if (amount is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🎰  Server Jackpots")
                .WithColor(ColourGold)
                .WithDescription(
                    $"There are two jackpot pools running in this server.\n\n" +
                    $"**🎟️ Entry Jackpot** — enter via `/jackpot <amount>`\n" +
                    $"Weighted draw every hour. More you put in, better your odds.\n\n" +
                    $"**🌊 Passive Jackpot** — earned automatically\n" +
                    $"1% of every bet feeds this pool.\n" +
                    $"0.5% chance to win the entire pool on slots or scratch card.")
                .AddField("🎟️ Entry Pot", CreditHelper.Format(entryPot), inline: true)
                .AddField("🎟️ Entries", $"{entries}", inline: true)
                .AddField("🌊 Passive Pot", CreditHelper.Format(passivePot), inline: true)
                .WithFooter("Use /jackpot <amount> to enter the hourly draw!", AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        // ── Entry contribution ─────────────────────────────────────────────────
        decimal balance = _eco.GetBalance(UserId, ServerId);
        if ((decimal)amount > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return;
        }

        _eco.DeductCredits(UserId, ServerId, (decimal)amount, "jackpot_entry");

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddJackpotEntry",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@Amount",   (decimal)amount)
        ]);

        // Refresh totals after entry
        potDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetJackpotTotal",
            [new SqlParameter("@ServerID", ServerId)]);
        entryPot = potDt.Rows.Count > 0 ? decimal.Parse(potDt.Rows[0]["Total"].ToString()!) : (decimal)amount;
        entries = potDt.Rows.Count > 0 ? int.Parse(potDt.Rows[0]["Entries"].ToString()!) : 1;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎟️  Jackpot Entry Confirmed!")
            .WithColor(ColourGold)
            .WithDescription(
                $"{Context.User.Mention} entered **{CreditHelper.Format((decimal)amount)}** into the hourly jackpot!\n\n" +
                $"🎟️ **Entry Pot:** {CreditHelper.Format(entryPot)} across {entries} entr{(entries == 1 ? "y" : "ies")}\n" +
                $"🌊 **Passive Pot:** {CreditHelper.Format(passivePot)} *(win via slots or scratch card)*\n\n" +
                $"*Winner drawn every hour — weighted by contribution.*")
            .WithFooter("More entries = better odds!", AvatarUrl)
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

        decimal totalWagered = 0m, totalNet = 0m, biggestWin = 0m, biggestLoss = 0m;
        int totalGames = 0, totalWins = 0, totalLosses = 0;
        var gameLines = new System.Text.StringBuilder();

        foreach (System.Data.DataRow row in dt.Rows)
        {
            string game = row["Game"].ToString()!;
            int games = int.Parse(row["GamesPlayed"].ToString()!);
            int wins = int.Parse(row["Wins"].ToString()!);
            int losses = int.Parse(row["Losses"].ToString()!);
            decimal wagered = decimal.Parse(row["TotalWagered"].ToString()!);
            decimal net = decimal.Parse(row["NetTotal"].ToString()!);
            decimal bWin = decimal.Parse(row["BiggestWin"].ToString()!);
            decimal bLoss = decimal.Parse(row["BiggestLoss"].ToString()!);

            totalWagered += wagered;
            totalNet += net;
            totalGames += games;
            totalWins += wins;
            totalLosses += losses;
            if (bWin > biggestWin) biggestWin = bWin;
            if (bLoss < biggestLoss) biggestLoss = bLoss;

            string winRate = games > 0 ? $"{wins * 100 / games}%" : "—";
            string netStr = net >= 0m
                ? $"+{CreditHelper.CurrencyEmoji} {net:N0}"
                : $"-{CreditHelper.CurrencyEmoji} {Math.Abs(net):N0}";

            gameLines.AppendLine($"**{game}** — {games} games, {winRate} win rate, {netStr} net");
        }

        string overallNet = totalNet >= 0m
            ? $"+{CreditHelper.CurrencyEmoji} {totalNet:N0}"
            : $"-{CreditHelper.CurrencyEmoji} {Math.Abs(totalNet):N0}";

        int overallWinRate = totalGames > 0 ? totalWins * 100 / totalGames : 0;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📊  {target.Username}'s Gambling Stats")
            .WithColor(totalNet >= 0m ? ColourWin : ColourLoss)
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

        // ── Cooldown check ─────────────────────────────────────────────────────
        string cooldownKey = $"{UserId}:fish";
        if (_cooldowns.TryGetValue(cooldownKey, out var lastFish))
        {
            var elapsed = DateTime.UtcNow - lastFish;
            var cooldown = TimeSpan.FromMinutes(CreditHelper.FishCooldownMinutes);
            if (elapsed < cooldown)
            {
                var remaining = cooldown - elapsed;
                int m = (int)remaining.TotalMinutes;
                int s = remaining.Seconds;
                await FollowupAsync(embed: _embed.BuildErrorEmbed("Fishing",
                    $"Your line is still drying! Cast again in **{m}m {s}s**.", Username).Build(),
                    ephemeral: true);
                return;
            }
        }
        _cooldowns[cooldownKey] = DateTime.UtcNow;

        // ── Resolve catch immediately (hidden until reveal) ────────────────────
        var (name, emoji, credits, flavour) = CreditHelper.CastLine();

        string rarity;
        Color colour;
        string rarityLine;
        if (credits == 0m)
        {
            rarity = "Junk";
            colour = new Color(128, 128, 128);
            rarityLine = "⬜ **Junk**";
        }
        else if (credits < 10_000m)
        {
            rarity = "Common";
            colour = ColourInfo;
            rarityLine = "🟩 **Common**";
        }
        else if (credits < 25_000m)
        {
            rarity = "Uncommon";
            colour = ColourWin;
            rarityLine = "🟦 **Uncommon**";
        }
        else if (credits < 70_000m)
        {
            rarity = "Rare";
            colour = ColourGold;
            rarityLine = "🟨 **Rare**";
        }
        else
        {
            rarity = "Legendary";
            colour = new Color(255, 100, 220);
            rarityLine = "🌟 **LEGENDARY**";
        }

        // ── Scene renderer ─────────────────────────────────────────────────────
        static string Scene(int lineLen, bool bobbing, bool biting, bool reeling)
        {
            string line = new string('~', Math.Max(0, lineLen));
            string tip = biting ? "❗" :
                          reeling ? "💥" :
                          bobbing ? "🔵" : "·";

            string water1 = biting ? "≋≋≋≋≋≋≋≋≋≋≋≋≋≋≋≋≋≋" :
                            bobbing ? "〰〰〰〰〰〰〰〰〰〰" :
                                       "≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈";
            return
                $"```\n" +
                $"  ☁️           ☁️       ☁️\n\n" +
                $"  🎣{line}{tip}\n" +
                $"  {water1}\n" +
                $"  ≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈\n" +
                $"  ≈  🐟  ≈  🐠  ≈  🐡  ≈\n" +
                $"```";
        }

        // ── Phase 1: Casting ───────────────────────────────────────────────────
        var msg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🎣  Casting…")
            .WithColor(ColourInfo)
            .WithDescription(Scene(0, false, false, false) + "\n*Winding up…*")
            .WithFooter(Username, AvatarUrl)
            .Build());

        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🎣  Casting…")
            .WithColor(ColourInfo)
            .WithDescription(Scene(5, false, false, false) + "\n*Line flying…*")
            .WithFooter(Username, AvatarUrl)
            .Build());

        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🎣  Casting…")
            .WithColor(ColourInfo)
            .WithDescription(Scene(10, false, false, false) + "\n*Splash! Bobber is out.*")
            .WithFooter(Username, AvatarUrl)
            .Build());

        // ── Phase 2: Waiting / bobbing ─────────────────────────────────────────
        foreach (var waitLine in new[] { "*Waiting for a bite…*", "*The water is calm…*", "*Something stirs below…*" })
        {
            await Task.Delay(900);
            await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("🎣  Waiting…")
                .WithColor(ColourInfo)
                .WithDescription(Scene(10, true, false, false) + $"\n{waitLine}")
                .WithFooter(Username, AvatarUrl)
                .Build());
        }

        // ── Phase 3: Bite / reel ───────────────────────────────────────────────
        if (credits > 0m)
        {
            await Task.Delay(500);
            await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("❗  Bite detected!")
                .WithColor(new Color(255, 165, 0))
                .WithDescription(Scene(10, false, true, false) + "\n*Something grabbed the line!*")
                .WithFooter(Username, AvatarUrl)
                .Build());

            await Task.Delay(700);
            await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("🎣  Reeling in…!")
                .WithColor(new Color(255, 165, 0))
                .WithDescription(Scene(6, false, false, true) + "\n*Reel it in! Reel it in!*")
                .WithFooter(Username, AvatarUrl)
                .Build());

            await Task.Delay(700);
            await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("🎣  Almost there…!")
                .WithColor(new Color(255, 165, 0))
                .WithDescription(Scene(2, false, false, true) + "\n*So close…!*")
                .WithFooter(Username, AvatarUrl)
                .Build());

            await Task.Delay(600);
        }
        else
        {
            await Task.Delay(800);
            await msg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("🎣  Reeling in…")
                .WithColor(new Color(128, 128, 128))
                .WithDescription(Scene(6, false, false, false) + "\n*Something is on the line… feels heavy and weird.*")
                .WithFooter(Username, AvatarUrl)
                .Build());
            await Task.Delay(700);
        }

        // ── Phase 4: Final reveal ──────────────────────────────────────────────
        decimal newBalance = _eco.GetBalance(UserId, ServerId);
        if (credits > 0m)
        {
            // Golden Ticket multiplier
            decimal gtMult = ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket_ii") ? 3m :
                             ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket") ? 2m : 1m;
            if (gtMult > 1m) credits *= gtMult;

            _eco.AddCredits(UserId, ServerId, credits, "fishing");
            newBalance = _eco.GetBalance(UserId, ServerId);

            // Log catch for /stats
            try
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddFishLog",
                [
                    new SqlParameter("@UserID",   UserId),
                    new SqlParameter("@ServerID", ServerId),
                    new SqlParameter("@FishName", name),
                    new SqlParameter("@Rarity",   rarity),
                    new SqlParameter("@Credits",  credits)
                ]);
            }
            catch { }

            // Challenge tracking
            TrackChallenge("fish");
            if (rarity is "Rare" or "Legendary") TrackChallenge("fish_rare");
            if (rarity is "Rare" or "Legendary") TrackChallenge("fish_rare3");
            if (rarity == "Legendary") TrackChallenge("fish_leg");
        }

        string catchBlock =
            $"```\n" +
            $"  ╔══════════════════════════╗\n" +
            $"  ║  {emoji}  {name,-22}║\n" +
            $"  ║  {rarityLine,-30}║\n" +
            $"  ╚══════════════════════════╝\n" +
            $"```";

        string nextCast = $"-# ⏱ Next cast available in {CreditHelper.FishCooldownMinutes} minutes.";

        var finalEmbed = new EmbedBuilder()
            .WithTitle(credits > 0m ? $"{emoji}  You caught a {name}!" : $"{emoji}  You fished up… {name}.")
            .WithColor(colour)
            .WithDescription($"{catchBlock}\n_{flavour}_\n\n{nextCast}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

        if (credits > 0m)
            finalEmbed
                .AddField("Rarity", rarityLine, inline: true)
                .AddField("Reward", CreditHelper.Format(credits), inline: true)
                .AddField("Balance", CreditHelper.Format(newBalance), inline: true);
        else
            finalEmbed.AddField("Result", "Better luck next cast.", inline: false);

        await msg.ModifyAsync(m => m.Embed = finalEmbed.Build());
    }


    [SlashCommand("bigwheel", "Spin the Big Wheel and multiply your bet!")]
    [EnabledInDm(false)]
    public async Task HandleBigWheelAsync(string betStr)
    {
        await DeferAsync();

        betStr = betStr.Replace(",", "").Trim();

        if (!long.TryParse(betStr, out long bet))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Big Wheel", "Invalid bet amount. Please enter a number.", Username).Build(), ephemeral: true);
        }


        if (IsOnCooldown("bigwheel", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "bigwheel")) return;

        SetCooldown("bigwheel");

        bool wheelChaos = ShopHelper.HasActiveEffect(UserId, ServerId, "chaos_card");
        if (wheelChaos) ShopHelper.ConsumeActiveEffect(UserId, ServerId, "chaos_card");

        int winIdx = wheelChaos ? CreditHelper.SpinWheelChaos() : CreditHelper.SpinWheel();
        var (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];

        string? shieldNote = null;
        if (wMult == 0.0 && ShopHelper.HasActiveEffect(UserId, ServerId, "bk_shield"))
        {
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "bk_shield");
            winIdx = CreditHelper.SpinWheel();
            (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];
            shieldNote = "🛡️ **Bankrupt Shield** blocked the BANKRUPT and re-spun!";
        }

        decimal payout = (decimal)bet * (decimal)wMult;
        decimal newBalance = ApplyGamble((decimal)bet, payout, "bigwheel");

        string? insuranceNote = null;
        if (payout < bet && ShopHelper.HasActiveEffect(UserId, ServerId, "insurance"))
        {
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "insurance");
            decimal refund = (decimal)bet / 2m;
            newBalance = _eco.AddCredits(UserId, ServerId, refund, "insurance_refund");
            payout += refund;
            insuranceNote = $"📋 **Gamble Insurance** refunded {CreditHelper.Format(refund)}!";
        }

        int total = CreditHelper.WheelSegments.Length;
        bool won = payout > (decimal)bet;
        if (won)
        {
            TrackChallenge("bigwheel");
            if (wMult >= 10.0) TrackChallenge("bigwheel_h");
        }
        bool push = payout == (decimal)bet;
        Color final = wMult switch
        {
            >= 50.0 => ColourGold,
            _ => payout == 0 ? ColourLoss : won ? ColourWin : push ? ColourPush : ColourLoss
        };

        int rotations = Random.Shared.Next(2, 5);
        int overshoot = total * rotations + winIdx;

        var frameList = new List<(int posOffset, int delayMs, string status)>();

        // Phase 1: fast early spin — every 4th position
        for (int o = -(overshoot - 1); o <= -21; o += 4)
            frameList.Add((o, 80, "🌀  Spinning…"));

        // Phase 2: mid deceleration — every 2nd position
        for (int o = -20; o <= -12; o += 2)
            frameList.Add((o, 140, "💨  Spinning…"));

        // Phase 3: ease-out finale — every position
        frameList.AddRange(
        [
            (-11, 200, "🌀  Spinning…"),
            (-10, 240, "💨  Spinning…"),
            ( -9, 290, "💨  Slowing…"),
            ( -8, 350, "😮  Slowing…"),
            ( -7, 420, "😮  Almost…"),
            ( -6, 500, "👀  Almost there…"),
            ( -5, 590, "👀  Almost there…"),
            ( -4, 680, "🤞  Any second…"),
            ( -3, 770, "🤞  Come on…"),
            ( -2, 860, "🤞  So close…"),
            ( -1, 950, "🤞  Come on…"),
        ]);

        EmbedBuilder SpinFrame(int pos, string status) =>
            new EmbedBuilder()
                .WithTitle("🎡  Big Wheel — Spinning!")
                .WithColor(ColourInfo)
                .WithDescription(
                    CreditHelper.BuildWheelDisplay(((pos % total) + total) % total) +
                    $"\n*{status}*")
                .WithFooter($"Bet: {CreditHelper.Format((decimal)bet)} • {Username}", AvatarUrl);

        var (firstOffset, _, firstStatus) = frameList[0];
        var msg = await FollowupAsync(embed: SpinFrame(overshoot + firstOffset, firstStatus).Build());

        foreach (var (posOffset, delayMs, status) in frameList[1..])
        {
            await Task.Delay(delayMs);
            await msg.ModifyAsync(m => m.Embed = SpinFrame(overshoot + posOffset, status).Build());
        }

        string wheelDisplay = CreditHelper.BuildWheelDisplay(winIdx);
        string? chaosNote = wheelChaos ? "🃏 **Chaos Card** randomized the wheel weights!" : null;
        string resultDesc = shieldNote is not null || insuranceNote is not null || chaosNote is not null
            ? wheelDisplay + "\n\n" + string.Join("\n", new[] { chaosNote, shieldNote, insuranceNote }.Where(n => n is not null)!)
            : wheelDisplay;

        var resultEmbed = new EmbedBuilder()
            .WithTitle($"🎡  {wEmoji}  {wLabel}!")
            .WithColor(final)
            .WithDescription(resultDesc)
            .AddField("Bet", CreditHelper.Format((decimal)bet), inline: true)
            .AddField("Multiplier", wLabel, inline: true)
            .AddField("Payout", CreditHelper.Format(payout), inline: true)
            .AddField("Balance", CreditHelper.Format(newBalance), inline: true)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp();

        await Task.Delay(1100);
        await msg.ModifyAsync(m =>
        {
            m.Embed = resultEmbed.Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("invest", "Lock away credits for 24 hours — collect your return when they mature.")]
    [EnabledInDm(false)]
    public async Task HandleInvestAsync([MinValue(100)] long amount = 0)
    {
        await DeferAsync();

        var pendingDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPendingInvestment",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (pendingDt.Rows.Count > 0)
        {
            var row = pendingDt.Rows[0];
            int invId = int.Parse(row["InvestmentID"].ToString()!);
            decimal invAmt = decimal.Parse(row["Amount"].ToString()!);
            var returnsAt = DateTime.Parse(row["ReturnsAt"].ToString()!);

            if (DateTime.UtcNow >= returnsAt)
            {
                decimal mult = decimal.Parse(row["Multiplier"].ToString()!);
                decimal payout = invAmt * mult;
                decimal profit = payout - invAmt;
                var (_, _, label) = CreditHelper.InvestOutcomes.First(o => o.multiplier == mult);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ClaimInvestment",
                [
                    new SqlParameter("@InvestmentID", invId),
                    new SqlParameter("@UserID",        UserId)
                ]);

                _eco.AddCredits(UserId, ServerId, payout, "invest_return");
                decimal newBalance = _eco.GetBalance(UserId, ServerId);

                string outcomeEmoji = mult >= 1.5m ? "🚀" : mult >= 1.0m ? "📈" : "📉";
                Color final = mult switch
                {
                    >= 50.0m => ColourGold,
                    > 1.0m => ColourWin,
                    1.0m => ColourPush,
                    _ => ColourLoss
                };

                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle($"{outcomeEmoji}  Investment Matured!")
                    .WithColor(final)
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

        if (amount <= 0)
        {
            await ErrorAsync("Specify an amount to invest, e.g. `/invest 1000`.");
            return;
        }

        decimal balance = _eco.GetBalance(UserId, ServerId);
        if ((decimal)amount > balance)
        {
            await ErrorAsync($"You only have {CreditHelper.Format(balance)}.");
            return;
        }

        var (mult2, label2) = CreditHelper.RollInvestment();
        var returnsAt2 = DateTime.UtcNow.AddHours(24);

        _eco.DeductCredits(UserId, ServerId, (decimal)amount, "invest_lock");

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddInvestment",
        [
            new SqlParameter("@UserID",     UserId),
            new SqlParameter("@ServerID",   ServerId),
            new SqlParameter("@Amount",     (decimal)amount),
            new SqlParameter("@Multiplier", mult2),
            new SqlParameter("@ReturnsAt",  returnsAt2)
        ]);

        decimal remaining2 = _eco.GetBalance(UserId, ServerId);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("💼  Investment Locked In!")
            .WithColor(ColourGold)
            .WithDescription(
                $"You've invested {CreditHelper.Format((decimal)amount)} — the market will do its thing.\n\n" +
                $"⏳ Returns in **24 hours** — run `/invest` to collect.\n" +
                $"*(Your return is sealed but hidden until you collect.)*")
            .AddField("Invested", CreditHelper.Format((decimal)amount), inline: true)
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
        decimal balance = _eco.GetBalance(UserId, ServerId);

        bool hasMegaBet = ShopHelper.HasActiveEffect(UserId, ServerId, "mega_bet");
        decimal effectiveCap = hasMegaBet ? decimal.MaxValue : CreditHelper.MaxBet;

        if ((decimal)bet < CreditHelper.MinBet)
        {
            await ErrorAsync($"Minimum bet is {CreditHelper.Format(CreditHelper.MinBet)}.");
            return false;
        }
        if ((decimal)bet > effectiveCap)
        {
            string capNote = hasMegaBet
                ? $"Maximum bet is {CreditHelper.Format(effectiveCap)} *(Bet Limit Booster active)*."
                : $"Maximum bet is {CreditHelper.Format(effectiveCap)}.";
            await ErrorAsync(capNote);
            return false;
        }
        if ((decimal)bet > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return false;
        }

        var lossRow = _sp.Select(Constants.Constants.discordBotConnStr, "GetDailyLoss",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        decimal dailyLost = lossRow.Rows.Count > 0
            ? decimal.Parse(lossRow.Rows[0]["TotalLost"].ToString()!)
            : 0m;

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
    private decimal ApplyGamble(decimal cost, decimal payout, string source)
    {
        bool won = payout > 0m;
        string streakKey = $"{UserId}:{ServerId}";

        // ── Chaos Card ─────────────────────────────────────────────────────────
        // Signals to the calling command via a flag — the command must check
        // ShopHelper.HasActiveEffect before its RNG roll and pass chaotic=true.
        // ApplyGamble itself just handles the economy side normally.

        // ── Comeback Chip ──────────────────────────────────────────────────────
        // Always track losses regardless of chip ownership so buying the chip
        // mid-streak works correctly.
        if (!won)
        {
            int losses = _lossStreaks.AddOrUpdate(streakKey, 1, (_, v) => v + 1);
            _winStreaks.TryRemove(streakKey, out _);

            if (losses >= 3 && ShopHelper.HasActiveEffect(UserId, ServerId, "comeback_chip"))
            {
                ShopHelper.ConsumeActiveEffect(UserId, ServerId, "comeback_chip");
                _lossStreaks.TryRemove(streakKey, out _);
                payout = cost * 1.5m;
                won = true;
            }
        }
        else
        {
            _lossStreaks.TryRemove(streakKey, out _);
        }

        // ── Hot Streak ─────────────────────────────────────────────────────────
        // Flag set here, refund applied AFTER debit/credit below.
        bool hotStreakTriggered = false;
        if (won)
        {
            int wins = _winStreaks.AddOrUpdate(streakKey, 1, (_, v) => v + 1);

            if (wins >= 3 && ShopHelper.HasActiveEffect(UserId, ServerId, "hot_streak"))
            {
                ShopHelper.ConsumeActiveEffect(UserId, ServerId, "hot_streak");
                _winStreaks.TryRemove(streakKey, out _);
                hotStreakTriggered = true;
            }
        }

        // ── Lucky Mode ─────────────────────────────────────────────────────────
        // If active for this server and this roll would be a net loss, re-roll at 60/40.
        if (payout < cost && _luckyServers.Contains(ServerId))
        {
            if (Random.Shared.NextDouble() < 0.60)
            {
                payout = cost * 1.5m;
                won = true;
            }
        }

        // ── Standard debit/credit ──────────────────────────────────────────────
        _eco.DeductCredits(UserId, ServerId, cost, source);

        // ── Golden Ticket multiplier ───────────────────────────────────────────
        if (payout > 0m)
        {
            decimal gtMult = ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket_ii") ? 3m :
                             ShopHelper.HasActiveEffect(UserId, ServerId, "golden_ticket") ? 2m : 1m;
            if (gtMult > 1m) payout *= gtMult;
        }

        if (payout > 0m)
            _eco.AddCredits(UserId, ServerId, payout, source);

        // ── Hot Streak refund — after debit so net is correct ─────────────────
        if (hotStreakTriggered)
            _eco.AddCredits(UserId, ServerId, cost, "hot_streak_refund");

        // ── Passive jackpot feed — 1% of every bet ─────────────────────────────
        // ServerId is a string ("Guild.Id.ToString()") — parse to long so ADO.NET
        // sends it as BIGINT rather than NVarChar, avoiding implicit-conversion failures.
        // feed is floored to a whole number so cast to long for the DECIMAL(20,0) column.
        if (long.TryParse(ServerId, out long feedServerId))
        {
            long feed = (long)Math.Max(1m, Math.Floor(cost * 0.01m));
            try
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "FeedPassiveJackpot",
                [
                    new SqlParameter("@ServerID", feedServerId),
                    new SqlParameter("@UserID",   UserId),
                    new SqlParameter("@Amount",   feed)
                ]);
            }
            catch (Exception ex) { Console.WriteLine($"[Jackpot] FeedPassiveJackpot failed: {ex.Message}"); }
        }

        // ── Log to GambleLog ───────────────────────────────────────────────────
        LogGamble(source, cost, payout);

        return _eco.GetBalance(UserId, ServerId);
    }

    // ── Passive jackpot claim check ────────────────────────────────────────────
    // Call after ApplyGamble on eligible commands (slots, scratch).
    // Returns (won, amount) — caller appends a note to the result embed if won.
    private const decimal PassiveJackpotOdds = 0.005m; // 0.5% chance per eligible play

    private async Task<(bool won, decimal amount)> TryClaimPassiveJackpotAsync()
    {
        if (Random.Shared.NextDouble() > (double)PassiveJackpotOdds) return (false, 0m);

        try
        {
            // Pre-check: read the current pool before attempting an atomic claim.
            // ClaimPassiveJackpot resets the pool to 0; some SP implementations
            // return the POST-reset value (0) rather than the amount claimed.
            // Fetching the pool first gives us the correct award amount as a
            // fallback and avoids calling the claim SP on an empty pool.
            if (!long.TryParse(ServerId, out long claimServerId)) return (false, 0m);

            var checkDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPassiveJackpot",
                [new SqlParameter("@ServerID", claimServerId)]);

            decimal poolBefore = checkDt.Rows.Count > 0
                ? decimal.Parse(checkDt.Rows[0]["Pool"].ToString()!)
                : 0m;

            if (poolBefore <= 0m) return (false, 0m);

            // Atomically claim the pool.
            var claimDt = _sp.Select(Constants.Constants.discordBotConnStr, "ClaimPassiveJackpot",
                [new SqlParameter("@ServerID", claimServerId)]);

            // Use SP-returned amount when available; fall back to pre-check if SP
            // returns the post-reset value (0) or no rows.
            decimal claimed = claimDt.Rows.Count > 0
                ? decimal.Parse(claimDt.Rows[0]["Pool"].ToString()!)
                : 0m;

            if (claimed <= 0m) claimed = poolBefore; // SP returned post-reset 0 — use pre-check

            _eco.AddCredits(UserId, ServerId, claimed, "passive_jackpot_win");

            // Server-wide announcement so all players see the winner.
            try
            {
                var guild = Context.Guild;

                // Resolve announcement channel: prefer the server's configured default,
                // fall back to the channel the command was run in so it always sends.
                ITextChannel? channel = null;
                var serverDetails = ServerHelper.GetServerInfo(guild.Id);
                if (serverDetails is not null
                    && ulong.TryParse(serverDetails.DefaultChannelID, out ulong defChanId)
                    && defChanId != 0)
                {
                    channel = guild.GetTextChannel(defChanId);
                }
                channel ??= Context.Channel as ITextChannel;

                if (channel is not null)
                {
                    await channel.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("🎰  PASSIVE JACKPOT WINNER!")
                        .WithColor(new Color(255, 215, 0))
                        .WithDescription(
                            $"🎉 {Context.User.Mention} just hit the **server passive jackpot** and won **{CreditHelper.Format(claimed)}**!\n\n" +
                            $"*The pool has been reset. Every gambling loss feeds it back up — good luck!*")
                        .WithCurrentTimestamp()
                        .Build());
                }
            }
            catch { /* non-fatal — don't block credit award on channel failure */ }

            return (true, claimed);
        }
        catch { return (false, 0m); }
    }

    // ── Double-or-Nothing button handlers ─────────────────────────────────────

    [ComponentInteraction(BtnDonAccept)]
    public async Task OnDonAcceptAsync()
    {
        await DeferAsync();

        string offerKey = $"{UserId}:{ServerId}";

        if (!_donOffers.TryRemove(offerKey, out var offer))
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Content = "⏰ That offer already expired or was claimed.";
                m.Components = new ComponentBuilder().Build();
            });
            return;
        }

        if (DateTime.UtcNow > offer.expiry)
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Content = "⏰ Too slow — the offer expired!";
                m.Components = new ComponentBuilder().Build();
            });
            return;
        }

        // Flip — 50/50
        bool flipWon = Random.Shared.Next(2) == 0;

        if (flipWon)
        {
            // Win: double the original net win
            decimal prize = offer.amount * 2m;
            _eco.AddCredits(UserId, ServerId, prize, "don_win");
            decimal balance = _eco.GetBalance(UserId, ServerId);

            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithTitle("⚡  Double-or-Nothing — WIN!")
                    .WithColor(ColourGold)
                    .WithDescription(
                        $"🪙 The coin landed in your favour!\n\n" +
                        $"**{CreditHelper.Format(offer.amount)}** → **{CreditHelper.Format(prize)}** 🎉")
                    .AddField("Payout", CreditHelper.Format(prize), inline: true)
                    .AddField("Balance", CreditHelper.Format(balance), inline: true)
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build();
                m.Components = new ComponentBuilder().Build();
            });
        }
        else
        {
            // Loss: claw back the original win
            _eco.DeductCredits(UserId, ServerId, offer.amount, "don_loss");
            decimal balance = _eco.GetBalance(UserId, ServerId);

            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithTitle("⚡  Double-or-Nothing — LOSS")
                    .WithColor(ColourLoss)
                    .WithDescription(
                        $"💸 The coin wasn't kind.\n\n" +
                        $"You lost **{CreditHelper.Format(offer.amount)}** back.")
                    .AddField("Lost", CreditHelper.Format(offer.amount), inline: true)
                    .AddField("Balance", CreditHelper.Format(balance), inline: true)
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build();
                m.Components = new ComponentBuilder().Build();
            });
        }
    }

    [ComponentInteraction(BtnDonDecline)]
    public async Task OnDonDeclineAsync()
    {
        await DeferAsync();

        string offerKey = $"{UserId}:{ServerId}";
        _donOffers.TryRemove(offerKey, out _);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = $"💰 Smart move — winnings kept.";
            m.Components = new ComponentBuilder().Build();
        });
    }

    /// <summary>Registers a double-or-nothing offer. Returns the button component to attach.</summary>
    private MessageComponent? OfferDon(decimal netWin)
    {
        // Only offer if there's a meaningful win and no existing offer for this user
        if (netWin <= 0m) return null;
        string key = $"{UserId}:{ServerId}";
        _donOffers[key] = (netWin, DateTime.UtcNow.Add(DonWindow));
        return DonButtons();
    }

    [SlashCommand("lucky", "Toggle lucky mode for this server (owner only).")]
    [EnabledInDm(false)]
    public async Task HandleLuckyAsync()
    {
        await DeferAsync(ephemeral: true);

        if (Context.User.Id != LuckyOwnerID)
        {
            await FollowupAsync("You don't have permission to use this command.", ephemeral: true);
            return;
        }

        if (_luckyServers.Contains(ServerId))
        {
            _luckyServers.Remove(ServerId);
            await FollowupAsync(
                embed: new EmbedBuilder()
                    .WithTitle("Lucky Mode — OFF")
                    .WithColor(ColourLoss)
                    .WithDescription($"Lucky mode **disabled** for **{Context.Guild.Name}**.\nOdds have returned to normal.")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build(),
                ephemeral: true);
        }
        else
        {
            _luckyServers.Add(ServerId);
            await FollowupAsync(
                embed: new EmbedBuilder()
                    .WithTitle("Lucky Mode — ON")
                    .WithColor(ColourGold)
                    .WithDescription($"Lucky mode **enabled** for **{Context.Guild.Name}**.\nAll gambling losses are re-rolled at **60% win / 40% loss**.")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build(),
                ephemeral: true);
        }
    }

    /// <summary>Increments challenge progress for the given game type. Non-fatal.</summary>
    private void TrackChallenge(string gameType)
    {
        try
        {
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "IncrementChallengeProgress",
            [
                new SqlParameter("@UserID",   UserId),
                new SqlParameter("@ServerID", ServerId),
                new SqlParameter("@GameType", gameType)
            ]);
        }
        catch { }
    }

    /// <summary>Write a gamble result to the log (used for draws/pushes that skip ApplyGamble).</summary>
    private void LogGamble(string game, decimal bet, decimal payout)
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