using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Gambling commands — all require a credit bet.
/// Games: Slots, Coinflip, Dice, Roulette, Scratch Card, Horse Race, RPS,
///        High-Low, Jackpot, Transfer.
/// Stats: /gamblestats
///
/// Per-user cooldowns are tracked in memory (reset on restart — intentional).
/// Daily loss limit computation exists (ValidateBet) but the actual enforcement block is
/// commented out in source — replicated as-is, so no limit is currently enforced.
/// </summary>
public class Gambling(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourWin = EmbedColors.Green;
    private static readonly Color ColourLoss = EmbedColors.Red;
    private static readonly Color ColourPush = EmbedColors.Blue;
    private static readonly Color ColourGold = EmbedColors.Gold;
    private static readonly Color ColourInfo = EmbedColors.Blue;

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

    /// <summary>True if the user is still within this game's cooldown window; if so, sets <paramref name="remaining"/> to the time left.</summary>
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

    /// <summary>Stamps the current time as the user's last play of this game, starting its cooldown window.</summary>
    private void SetCooldown(string game) =>
        _cooldowns[$"{UserId}:{game}"] = DateTime.UtcNow;


    [SlashCommand("slots", "Spin the slot machine!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Spins the 3-reel slot machine with an animated reveal, then scores the result and checks for a passive jackpot hit.</summary>
    public async Task HandleSlotsAsync([MinValue(10)] long bet)
    {
        await DeferAsync();

        if (IsOnCooldown("slots", out var cd)) { await CooldownAsync(cd); return; }
        if (!await ValidateBet(bet, "slots")) return;

        SetCooldown("slots");

        bool slotChaos = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "chaos_card");
        if (slotChaos) await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "chaos_card");

        string r1 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();
        string r2 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();
        string r3 = slotChaos ? CreditHelper.SpinReelRandom() : CreditHelper.SpinReel();

        var (payout, result) = CreditHelper.CalculateSlotPayout(r1, r2, r3, (decimal)bet);
        if (slotChaos) result = "🃏 " + result;
        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "slots");

        // Passive jackpot — 0.5% chance on every spin
        var (pjWon, pjAmount) = await TryClaimPassiveJackpotAsync();
        if (pjWon) newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        // Challenge tracking
        if (payout > 0m)
        {
            await TrackChallengeAsync("slots");
            if (r1 == r2 && r2 == r3 && r1 == "💎") await TrackChallengeAsync("slots_jack");
        }

        EmbedBuilder SpinFrame(string a, string b, string c, string? label = null) =>
            _embed.BuildSimpleEmbed(
                "🎰  Slot Machine",
                $"╔══════════════╗\n" +
                $"║  {a}  {b}  {c}  ║\n" +
                $"╚══════════════╝\n\n" +
                (label ?? "*Spinning…*"),
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false);

        var msg = await FollowupAsync(embed: SpinFrame(
            CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom()).Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = SpinFrame(
            r1, CreditHelper.SpinReelRandom(), CreditHelper.SpinReelRandom()).Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                "🎰  Slot Machine",
                $"╔══════════════╗\n" +
                $"║  {r1}  {r2}  {r3}  ║\n" +
                $"╚══════════════╝\n\n" +
                $"**{result}**" +
                (pjWon ? $"\n\n🎰 **PASSIVE JACKPOT!** You hit the server pool for **{CreditHelper.Format(pjAmount)}**!" : ""),
                payout >= (decimal)bet ? ColourWin : payout > 0m ? ColourPush : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            decimal netWin = payout - (decimal)bet;
            if (!pjWon && netWin > 0m)
                m.Components = OfferDon(netWin);
        });
    }


    [SlashCommand("coinflip", "Flip a coin and bet on the outcome!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Flips a coin with an animated reveal and pays out if the guessed side matches.</summary>
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

        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "coinflip");
        if (won) await TrackChallengeAsync("coinflip");

        string coinEmoji = result == "heads" ? "🪙" : "⚫";
        decimal netWinCf = payout - (decimal)bet;

        EmbedBuilder CoinFrame(string display, string label) =>
            _embed.BuildSimpleEmbed(
                "🪙  Coin Flip", $"{display}  *{label}*",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false);

        var msg = await FollowupAsync(embed: CoinFrame("🪙", "Flipping…").Build());
        await Task.Delay(450);
        await msg.ModifyAsync(m => m.Embed = CoinFrame("⚫", "Spinning…").Build());
        await Task.Delay(450);
        await msg.ModifyAsync(m => m.Embed = CoinFrame("🪙", "Spinning…").Build());
        await Task.Delay(550);

        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"{coinEmoji}  Coin Flip — {char.ToUpper(result[0])}{result[1..]}",
                won
                    ? $"You called **{side}** — correct! {CreditHelper.FormatDelta(payout - (decimal)bet)}"
                    : $"You called **{side}** — it was **{result}**. {CreditHelper.FormatDelta(-(decimal)bet)}",
                won ? ColourWin : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(netWinCf);
        });
    }


    [SlashCommand("dice", "Roll two dice and bet on the total!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Rolls two dice with an animated reveal and pays out based on the chosen bet type (over/under/seven/doubles).</summary>
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

        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "dice");
        if (won) await TrackChallengeAsync("dice");

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

        static string DieFace(int n) => n switch { 1 => "⚀", 2 => "⚁", 3 => "⚂", 4 => "⚃", 5 => "⚄", _ => "⚅" };

        EmbedBuilder DiceFrame(int a, int b, string label) =>
            _embed.BuildSimpleEmbed(
                "🎲  Dice Roll", $"{DieFace(a)}  {DieFace(b)}\n*{label}*",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false);

        var msg = await FollowupAsync(embed: DiceFrame(
            Random.Shared.Next(1, 7), Random.Shared.Next(1, 7), "Rolling…").Build());
        await Task.Delay(500);
        await msg.ModifyAsync(m => m.Embed = DiceFrame(
            Random.Shared.Next(1, 7), Random.Shared.Next(1, 7), "Rolling…").Build());
        await Task.Delay(600);

        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"🎲  Dice Roll — {d1} + {d2} = **{total}**{(d1 == d2 ? " (doubles!)" : "")}",
                $"{DieFace(d1)}  {DieFace(d2)}\n\n{outcomeText}",
                won ? ColourWin : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("roulette", "Spin the roulette wheel!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Spins the roulette wheel with an animated reveal and pays out based on the chosen bet type.</summary>
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

        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "roulette");
        bool won = payout > 0m;
        if (won) await TrackChallengeAsync("roulette");

        bool isRed = CreditHelper.RedNumbers.Contains(spin.ToString());
        string spinTitle = spin == 0
            ? "🟢 0 — Green!"
            : isRed ? $"🔴 {spin} — Red" : $"⚫ {spin} — Black";

        string betDesc = betType == "number"
            ? $"Bet on **{number}** — {result}"
            : $"Bet on **{betType}** — {result}";

        var msg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🎡  Roulette — Spinning…", "🔵 *The ball is flying around the wheel…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(900);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🎡  Roulette — Slowing…", "🔵 *The ball is losing speed…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(1000);
        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"🎡  Roulette — {spinTitle}", betDesc,
                won ? ColourWin : spin == 0 ? ColourPush : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("scratchcard", "Buy and scratch a card for instant prizes!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Buys and reveals a scratch card one symbol at a time, then pays out per the matched prize tier (or checks the passive jackpot).</summary>
    public async Task HandleScratchCardAsync()
    {
        await DeferAsync();

        if (IsOnCooldown("scratchcard", out var cd)) { await CooldownAsync(cd); return; }

        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
        if (balance < CreditHelper.ScratchCardCost)
        {
            await ErrorAsync($"Scratch cards cost {CreditHelper.Format(CreditHelper.ScratchCardCost)}. You have {CreditHelper.Format(balance)}.");
            return;
        }

        SetCooldown("scratchcard");

        bool scratchChaos = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "chaos_card");
        if (scratchChaos) await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "chaos_card");

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
        var msg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🎟️  Scratch Card", Card("❓", "❓", "❓") + "\n*Scratching…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        // ── Phase 2: Reveal one symbol at a time ──────────────────────────────
        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🎟️  Scratch Card", Card(s1, "❓", "❓") + "\n*Scratching…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(700);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🎟️  Scratch Card", Card(s1, s2, "❓") + $"\n{(s1 == s2 ? "*Match so far… 👀*" : "*No match yet…*")}",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(900);

        // ── Phase 3: Final reveal ──────────────────────────────────────────────
        decimal newBalance = await ApplyGambleAsync(CreditHelper.ScratchCardCost, payout, "scratchcard");

        // Passive jackpot — 0.5% chance on every card
        var (pjWon, pjAmount) = await TryClaimPassiveJackpotAsync();
        if (pjWon) newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
        if (won) await TrackChallengeAsync("scratch");

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
            m.Embed = _embed.BuildSimpleEmbed(
                pjWon ? "🎰  PASSIVE JACKPOT!" : won ? (jackpot ? "💰  JACKPOT!" : "🎉  Winner!") : "🎟️  No Match",
                Card(s1, s2, s3) + $"\n{resultLine}{pjNote}\n\n{nextCastNote}",
                pjWon ? ColourGold : colour,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Cost", CreditHelper.Format(CreditHelper.ScratchCardCost), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won && !pjWon) m.Components = OfferDon(payout - CreditHelper.ScratchCardCost);
        });
    }


    [SlashCommand("horses", "Bet on a horse race!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Runs an animated horse race across 3 frames and pays out at the picked horse's odds if it wins.</summary>
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
        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "horses");
        if (won)
        {
            await TrackChallengeAsync("horses");
            if (horse.odds >= 7.0) await TrackChallengeAsync("horses_h");
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
        var msg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🏇  And they're off!", TrackFrame(Frame(0.1, 0.3), false) + "*The gates fly open!*",
            ColourInfo, footer: footer, footerIconUrl: AvatarUrl, timestamp: false).Build());

        // ── Frame 2 ────────────────────────────────────────────────────────────
        await Task.Delay(1200);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🏇  Rounding the bend…", TrackFrame(Frame(0.35, 0.6), false) + "*Jostling for position!*",
            ColourInfo, footer: footer, footerIconUrl: AvatarUrl, timestamp: false).Build());

        // ── Frame 3 ────────────────────────────────────────────────────────────
        await Task.Delay(1200);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🏇  Final straight!", TrackFrame(Frame(0.65, 0.88), false) + "*It's neck and neck!*",
            ColourInfo, footer: footer, footerIconUrl: AvatarUrl, timestamp: false).Build());

        // ── Final result ───────────────────────────────────────────────────────
        await Task.Delay(1200);
        var winHorse = CreditHelper.Horses[winner];
        string result = won
            ? $"🎉 **{horse.name}** wins at **{horse.odds}×**! {CreditHelper.FormatDelta(payout - (decimal)bet)}"
            : $"**{winHorse.name}** takes the win. Your horse didn't place. {CreditHelper.FormatDelta(-(decimal)bet)}";

        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"🏆  {winHorse.emoji} {winHorse.name} wins the race!",
                TrackFrame(finalProg, true) + $"\n{result}",
                won ? ColourWin : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("rps", "Play Rock Paper Scissors against the bot with a credit bet!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Plays Rock-Paper-Scissors against the bot with an animated countdown; draws refund the bet without counting as a win/loss.</summary>
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
            newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
            await LogGambleAsync("rps", (decimal)bet, (decimal)bet);
        }
        else
        {
            newBalance = await ApplyGambleAsync((decimal)bet, payout, "rps");
            if (won) await TrackChallengeAsync("rps");
        }

        string pickEmoji = pick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string botEmoji = botPick switch { "rock" => "🪨", "paper" => "📄", _ => "✂️" };
        string outcome = draw ? "🤝 Draw!" : won ? "🎉 You win!" : "😔 Bot wins!";
        Color colour = draw ? ColourPush : won ? ColourWin : ColourLoss;

        EmbedBuilder RpsFrame(string count) =>
            _embed.BuildSimpleEmbed(
                "✊  Rock Paper Scissors", $"**{count}**",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false);

        var msg = await FollowupAsync(embed: RpsFrame("3️⃣").Build());
        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = RpsFrame("2️⃣").Build());
        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = RpsFrame("1️⃣").Build());
        await Task.Delay(600);

        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"✊  Rock Paper Scissors — {outcome}",
                $"You: **{pickEmoji} {pick}** vs Bot: **{botEmoji} {botPick}**\n\n{CreditHelper.FormatDelta(net)}",
                colour,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("highlow", "Draw a card — guess if the next one is higher or lower!")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Draws two cards and pays out if the guess (higher/lower) about the second card versus the first is correct; a tie pushes the bet back.</summary>
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
            newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
            await LogGambleAsync("highlow", (decimal)bet, (decimal)bet);
        }
        else
        {
            newBalance = await ApplyGambleAsync((decimal)bet, payout, "highlow");
            if (won) await TrackChallengeAsync("highlow");
        }

        string outcomeText = tie
            ? $"🤝 **Tie!** Both cards are **{card1Display}** — push, no money changes hands."
            : won
                ? $"✅ **Correct!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}"
                : $"❌ **Wrong!** {card1Display} → {card2Display} {CreditHelper.FormatDelta(net)}";

        // Phase 1: show first card, second card hidden
        var msg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🃏  High-Low",
            $"**First card:** `{card1Display}`\n" +
            $"**Second card:** `🂠`\n\n" +
            $"You guessed **{guess}** — drawing second card…",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(1400);

        // Phase 2: reveal second card and result
        await msg.ModifyAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                $"🃏  High-Low — {(tie ? "Push" : won ? "You Win!" : "You Lose!")}",
                $"**First card:** `{card1Display}`\n" +
                $"**Second card:** `{card2Display}`\n\n" +
                $"You guessed **{guess}** — {outcomeText}",
                tie ? ColourPush : won ? ColourWin : ColourLoss,
                footer: Username, footerIconUrl: AvatarUrl,
                fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                         ("Payout", CreditHelper.Format(payout), true),
                         ("Balance", CreditHelper.Format(newBalance), true)]).Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("jackpot", "View jackpot pools or contribute to the entry jackpot.")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>With no amount, shows both jackpot pools' current totals; with an amount, contributes it to the entry jackpot's weighted hourly draw.</summary>
    public async Task HandleJackpotAsync([MinValue(10)] long? amount = null)
    {
        await DeferAsync();

        // ── Fetch both pools ───────────────────────────────────────────────────
        var entryList = await db.JackpotEntries.AsNoTracking()
            .Where(e => e.ServerId == ServerId).ToListAsync();
        decimal entryPot = entryList.Sum(e => e.Amount);
        int entries = entryList.Count;

        long.TryParse(ServerId, out long jpServerId);
        decimal passivePot = await JackpotService.GetPoolAsync(db, jpServerId);

        // ── View-only (no amount given) ────────────────────────────────────────
        if (amount is null)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "🎰  Server Jackpots",
                $"There are two jackpot pools running in this server.\n\n" +
                $"**🎟️ Entry Jackpot** — enter via `/jackpot <amount>`\n" +
                $"Weighted draw every hour. More you put in, better your odds.\n\n" +
                $"**🌊 Passive Jackpot** — earned automatically\n" +
                $"1% of every bet feeds this pool.\n" +
                $"0.5% chance to win the entire pool on slots or scratch card.",
                ColourGold,
                footer: "Use /jackpot <amount> to enter the hourly draw!", footerIconUrl: AvatarUrl,
                fields: [("🎟️ Entry Pot", CreditHelper.Format(entryPot), true),
                         ("🎟️ Entries", $"{entries}", true),
                         ("🌊 Passive Pot", CreditHelper.Format(passivePot), true)]).Build());
            return;
        }

        // ── Entry contribution ─────────────────────────────────────────────────
        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
        if ((decimal)amount > balance)
        {
            await ErrorAsync($"You don't have enough credits! Balance: {CreditHelper.Format(balance)}.");
            return;
        }

        await CreditService.DeductCreditsAsync(db, UserId, ServerId, (decimal)amount, "jackpot_entry");

        db.JackpotEntries.Add(new JackpotEntry { UserId = UserId, ServerId = ServerId, Amount = (decimal)amount });
        await db.SaveChangesAsync();

        // Refresh totals after entry
        entryList = await db.JackpotEntries.AsNoTracking()
            .Where(e => e.ServerId == ServerId).ToListAsync();
        entryPot = entryList.Sum(e => e.Amount);
        entries = entryList.Count;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🎟️  Jackpot Entry Confirmed!",
            $"{Context.User.Mention} entered **{CreditHelper.Format((decimal)amount)}** into the hourly jackpot!\n\n" +
            $"🎟️ **Entry Pot:** {CreditHelper.Format(entryPot)} across {entries} entr{(entries == 1 ? "y" : "ies")}\n" +
            $"🌊 **Passive Pot:** {CreditHelper.Format(passivePot)} *(win via slots or scratch card)*\n\n" +
            $"*Winner drawn every hour — weighted by contribution.*",
            ColourGold,
            footer: "More entries = better odds!", footerIconUrl: AvatarUrl).Build());
    }

    [SlashCommand("gamblestats", "View your gambling statistics.")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Shows aggregated gambling statistics (wagered, net P&amp;L, win rate, per-game breakdown) for yourself or another member.</summary>
    public async Task HandleGambleStatsAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        string tId = target.Id.ToString();
        bool isSelf = target.Id == Context.User.Id;

        var stats = await db.GambleLogs.AsNoTracking()
            .Where(g => g.UserId == tId && g.ServerId == ServerId)
            .GroupBy(g => g.Game)
            .Select(g => new
            {
                Game = g.Key,
                GamesPlayed = g.Count(),
                Wins = g.Count(x => x.Net > 0),
                Losses = g.Count(x => x.Net < 0),
                TotalWagered = g.Sum(x => x.Bet),
                NetTotal = g.Sum(x => x.Net),
                BiggestWin = g.Max(x => x.Net),
                BiggestLoss = g.Min(x => x.Net)
            })
            .OrderByDescending(g => g.TotalWagered)
            .ToListAsync();

        if (stats.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"📊  {target.Username}'s Gambling Stats",
                "No gambling history yet! Try `/slots` or `/coinflip`.",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
            return;
        }

        decimal totalWagered = 0m, totalNet = 0m, biggestWin = 0m, biggestLoss = 0m;
        int totalGames = 0, totalWins = 0, totalLosses = 0;
        var gameLines = new System.Text.StringBuilder();

        foreach (var row in stats)
        {
            string game = row.Game;
            int games = row.GamesPlayed;
            int wins = row.Wins;
            int losses = row.Losses;
            decimal wagered = row.TotalWagered;
            decimal net = row.NetTotal;
            decimal bWin = row.BiggestWin;
            decimal bLoss = row.BiggestLoss;

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
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Casts a line with a multi-stage animated sequence (cast/wait/bite/reel) and reveals a weighted-random catch with its credit reward.</summary>
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
        var msg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🎣  Casting…", Scene(0, false, false, false) + "\n*Winding up…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🎣  Casting…", Scene(5, false, false, false) + "\n*Line flying…*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        await Task.Delay(600);
        await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            "🎣  Casting…", Scene(10, false, false, false) + "\n*Splash! Bobber is out.*",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

        // ── Phase 2: Waiting / bobbing ─────────────────────────────────────────
        foreach (var waitLine in new[] { "*Waiting for a bite…*", "*The water is calm…*", "*Something stirs below…*" })
        {
            await Task.Delay(900);
            await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                "🎣  Waiting…", Scene(10, true, false, false) + $"\n{waitLine}",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());
        }

        // ── Phase 3: Bite / reel ───────────────────────────────────────────────
        if (credits > 0m)
        {
            await Task.Delay(500);
            await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                "❗  Bite detected!", Scene(10, false, true, false) + "\n*Something grabbed the line!*",
                new Color(255, 165, 0), footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

            await Task.Delay(700);
            await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                "🎣  Reeling in…!", Scene(6, false, false, true) + "\n*Reel it in! Reel it in!*",
                new Color(255, 165, 0), footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

            await Task.Delay(700);
            await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                "🎣  Almost there…!", Scene(2, false, false, true) + "\n*So close…!*",
                new Color(255, 165, 0), footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());

            await Task.Delay(600);
        }
        else
        {
            await Task.Delay(800);
            await msg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                "🎣  Reeling in…", Scene(6, false, false, false) + "\n*Something is on the line… feels heavy and weird.*",
                new Color(128, 128, 128), footer: Username, footerIconUrl: AvatarUrl, timestamp: false).Build());
            await Task.Delay(700);
        }

        // ── Phase 4: Final reveal ──────────────────────────────────────────────
        decimal newBalance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
        if (credits > 0m)
        {
            // Golden Ticket multiplier
            decimal gtMult = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket_ii") ? 3m :
                             await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket") ? 2m : 1m;
            if (gtMult > 1m) credits *= gtMult;

            newBalance = await CreditService.AddCreditsAsync(db, UserId, ServerId, credits, "fishing");

            // Log catch for /stats
            try
            {
                db.FishLogs.Add(new FishLog { UserId = UserId, ServerId = ServerId, FishName = name, Rarity = rarity, Credits = credits });
                await db.SaveChangesAsync();
            }
            catch { }

            // Challenge tracking
            await TrackChallengeAsync("fish");
            if (rarity is "Rare" or "Legendary") await TrackChallengeAsync("fish_rare");
            if (rarity is "Rare" or "Legendary") await TrackChallengeAsync("fish_rare3");
            if (rarity == "Legendary") await TrackChallengeAsync("fish_leg");
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
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Spins the Big Wheel with a decelerating multi-frame animation and pays out at the landed segment's multiplier.</summary>
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

        bool wheelChaos = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "chaos_card");
        if (wheelChaos) await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "chaos_card");

        int winIdx = wheelChaos ? CreditHelper.SpinWheelChaos() : CreditHelper.SpinWheel();
        var (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];

        string? shieldNote = null;
        if (wMult == 0.0 && await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "bk_shield"))
        {
            await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "bk_shield");
            winIdx = CreditHelper.SpinWheel();
            (wLabel, wMult, _, wEmoji) = CreditHelper.WheelSegments[winIdx];
            shieldNote = "🛡️ **Bankrupt Shield** blocked the BANKRUPT and re-spun!";
        }

        decimal payout = (decimal)bet * (decimal)wMult;
        decimal newBalance = await ApplyGambleAsync((decimal)bet, payout, "bigwheel");

        string? insuranceNote = null;
        if (payout < bet && await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "insurance"))
        {
            await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "insurance");
            decimal refund = (decimal)bet / 2m;
            newBalance = await CreditService.AddCreditsAsync(db, UserId, ServerId, refund, "insurance_refund");
            payout += refund;
            insuranceNote = $"📋 **Gamble Insurance** refunded {CreditHelper.Format(refund)}!";
        }

        int total = CreditHelper.WheelSegments.Length;
        bool won = payout > (decimal)bet;
        if (won)
        {
            await TrackChallengeAsync("bigwheel");
            if (wMult >= 10.0) await TrackChallengeAsync("bigwheel_h");
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
            _embed.BuildSimpleEmbed(
                "🎡  Big Wheel — Spinning!",
                CreditHelper.BuildWheelDisplay(((pos % total) + total) % total) + $"\n*{status}*",
                ColourInfo, footer: $"Bet: {CreditHelper.Format((decimal)bet)} • {Username}",
                footerIconUrl: AvatarUrl, timestamp: false);

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

        var resultEmbed = _embed.BuildSimpleEmbed(
            $"🎡  {wEmoji}  {wLabel}!", resultDesc, final,
            footer: Username, footerIconUrl: AvatarUrl,
            fields: [("Bet", CreditHelper.Format((decimal)bet), true),
                     ("Multiplier", wLabel, true),
                     ("Payout", CreditHelper.Format(payout), true),
                     ("Balance", CreditHelper.Format(newBalance), true)]);

        await Task.Delay(1100);
        await msg.ModifyAsync(m =>
        {
            m.Embed = resultEmbed.Build();
            if (won) m.Components = OfferDon(payout - (decimal)bet);
        });
    }


    [SlashCommand("invest", "Lock away credits for 24 hours — collect your return when they mature.")]
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Locks credits into a 24h investment (if none pending), or collects the matured payout if the previous investment is ready.</summary>
    public async Task HandleInvestAsync([MinValue(100)] long amount = 0)
    {
        await DeferAsync();

        var pending = await db.Investments.AsNoTracking()
            .Where(i => i.UserId == UserId && i.ServerId == ServerId && !i.Claimed)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        if (pending is not null)
        {
            int invId = pending.InvestmentId;
            decimal invAmt = pending.Amount;
            var returnsAt = pending.ReturnsAt;

            if (DateTime.UtcNow >= returnsAt)
            {
                decimal mult = pending.Multiplier;
                decimal payout = invAmt * mult;
                decimal profit = payout - invAmt;
                var (_, _, label) = CreditHelper.InvestOutcomes.First(o => o.multiplier == mult);

                await db.Investments.Where(i => i.InvestmentId == invId && i.UserId == UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Claimed, true));

                decimal newBalance = await CreditService.AddCreditsAsync(db, UserId, ServerId, payout, "invest_return");

                string outcomeEmoji = mult >= 1.5m ? "🚀" : mult >= 1.0m ? "📈" : "📉";
                Color final = mult switch
                {
                    >= 50.0m => ColourGold,
                    > 1.0m => ColourWin,
                    1.0m => ColourPush,
                    _ => ColourLoss
                };

                await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                    $"{outcomeEmoji}  Investment Matured!",
                    $"{label}\n\n" +
                    $"Your {CreditHelper.Format(invAmt)} investment returned **{mult:0.00}×**.",
                    final,
                    footer: Username, footerIconUrl: AvatarUrl,
                    fields: [("Invested", CreditHelper.Format(invAmt), true),
                             ("Return", CreditHelper.Format(payout), true),
                             ("Profit", CreditHelper.FormatDelta(profit), true),
                             ("Balance", CreditHelper.Format(newBalance), true)]).Build());
                return;
            }

            var timeLeft = returnsAt - DateTime.UtcNow;
            string tlStr = $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";

            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "💼  Investment Pending",
                $"Your investment of {CreditHelper.Format(invAmt)} is still maturing.\n\n" +
                $"⏳ Returns in **{tlStr}**\n\n" +
                $"Run `/invest` again when it's ready to collect!",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
            return;
        }

        if (amount <= 0)
        {
            await ErrorAsync("Specify an amount to invest, e.g. `/invest 1000`.");
            return;
        }

        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);
        if ((decimal)amount > balance)
        {
            await ErrorAsync($"You only have {CreditHelper.Format(balance)}.");
            return;
        }

        var (mult2, label2) = CreditHelper.RollInvestment();
        var returnsAt2 = DateTime.UtcNow.AddHours(24);

        await CreditService.DeductCreditsAsync(db, UserId, ServerId, (decimal)amount, "invest_lock");

        db.Investments.Add(new Investment
        {
            UserId = UserId, ServerId = ServerId, Amount = (decimal)amount,
            Multiplier = mult2, ReturnsAt = returnsAt2
        });
        await db.SaveChangesAsync();

        decimal remaining2 = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "💼  Investment Locked In!",
            $"You've invested {CreditHelper.Format((decimal)amount)} — the market will do its thing.\n\n" +
            $"⏳ Returns in **24 hours** — run `/invest` to collect.\n" +
            $"*(Your return is sealed but hidden until you collect.)*",
            ColourGold,
            footer: Username, footerIconUrl: AvatarUrl,
            fields: [("Invested", CreditHelper.Format((decimal)amount), true),
                     ("Matures", $"<t:{new DateTimeOffset(returnsAt2).ToUnixTimeSeconds()}:R>", true),
                     ("Balance", CreditHelper.Format(remaining2), true)]).Build());
    }


    /// <summary>
    /// Validates bet, checks daily loss limit, and returns false with an error if invalid.
    /// Respects the mega_bet active effect which temporarily raises the max bet cap to 100k.
    /// </summary>
    private async Task<bool> ValidateBet(long bet, string game)
    {
        decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

        bool hasMegaBet = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "mega_bet");
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

        var since = DateTime.UtcNow.AddHours(-24);
        decimal dailyLost = await db.GambleLogs.AsNoTracking()
            .Where(g => g.UserId == UserId && g.ServerId == ServerId && g.Net < 0 && g.CreatedAt > since)
            .SumAsync(g => -g.Net);

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
    private async Task<decimal> ApplyGambleAsync(decimal cost, decimal payout, string source)
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

            if (losses >= 3 && await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "comeback_chip"))
            {
                await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "comeback_chip");
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

            if (wins >= 3 && await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "hot_streak"))
            {
                await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "hot_streak");
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
        await CreditService.DeductCreditsAsync(db, UserId, ServerId, cost, source);

        // ── Golden Ticket multiplier ───────────────────────────────────────────
        if (payout > 0m)
        {
            decimal gtMult = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket_ii") ? 3m :
                             await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "golden_ticket") ? 2m : 1m;
            if (gtMult > 1m) payout *= gtMult;
        }

        if (payout > 0m)
            await CreditService.AddCreditsAsync(db, UserId, ServerId, payout, source);

        // ── Hot Streak refund — after debit so net is correct ─────────────────
        if (hotStreakTriggered)
            await CreditService.AddCreditsAsync(db, UserId, ServerId, cost, "hot_streak_refund");

        // ── Passive jackpot feed — 1% of every bet ─────────────────────────────
        // ServerId is a string ("Guild.Id.ToString()") — parse to long to match the
        // PassiveJackpot table's bigint ServerId column.
        // feed is floored to a whole number, matching the source DECIMAL(20,0) column.
        if (long.TryParse(ServerId, out long feedServerId))
        {
            long feed = (long)Math.Max(1m, Math.Floor(cost * 0.01m));
            try
            {
                await JackpotService.FeedAsync(db, feedServerId, feed);
            }
            catch (Exception ex) { Console.WriteLine($"[Jackpot] FeedPassiveJackpot failed: {ex.Message}"); }
        }

        // ── Log to GambleLog ───────────────────────────────────────────────────
        await LogGambleAsync(source, cost, payout);

        return await CreditService.GetBalanceAsync(db, UserId, ServerId);
    }

    // ── Passive jackpot claim check ────────────────────────────────────────────
    // Call after ApplyGamble on eligible commands (slots, scratch).
    // Returns (won, amount) — caller appends a note to the result embed if won.
    private const decimal PassiveJackpotOdds = 0.005m; // 0.5% chance per eligible play

    /// <summary>
    /// Rolls for the passive jackpot (0.5% chance) and, on a hit, atomically claims and
    /// resets the server's pool, credits the winner, and announces it in the guild's
    /// announcement channel (falling back to the current channel).
    /// </summary>
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

            decimal poolBefore = await JackpotService.GetPoolAsync(db, claimServerId);

            if (poolBefore <= 0m) return (false, 0m);

            // Atomically claim the pool.
            decimal claimed = await JackpotService.ClaimAsync(db, claimServerId);

            if (claimed <= 0m) claimed = poolBefore; // defensive fallback — mirrors source's post-reset-0 guard

            await CreditService.AddCreditsAsync(db, UserId, ServerId, claimed, "passive_jackpot_win");

            // Server-wide announcement so all players see the winner.
            try
            {
                var guild = Context.Guild;

                // Resolve announcement channel: prefer the server's configured default,
                // fall back to the channel the command was run in so it always sends.
                ITextChannel? channel = null;
                var serverDetails = await ServerHelper.GetServerInfoAsync(db, guild.Id);
                if (serverDetails is not null
                    && ulong.TryParse(serverDetails.DefaultChannelID, out ulong defChanId)
                    && defChanId != 0)
                {
                    channel = guild.GetTextChannel(defChanId);
                }
                channel ??= Context.Channel as ITextChannel;

                if (channel is not null)
                {
                    await channel.SendMessageAsync(embed: _embed.BuildSimpleEmbed(
                        "🎰  PASSIVE JACKPOT WINNER!",
                        $"🎉 {Context.User.Mention} just hit the **server passive jackpot** and won **{CreditHelper.Format(claimed)}**!\n\n" +
                        $"*The pool has been reset. Every gambling loss feeds it back up — good luck!*",
                        new Color(255, 215, 0)).Build());
                }
            }
            catch { /* non-fatal — don't block credit award on channel failure */ }

            return (true, claimed);
        }
        catch { return (false, 0m); }
    }

    // ── Double-or-Nothing button handlers ─────────────────────────────────────

    [ComponentInteraction(BtnDonAccept)]
    /// <summary>Flips a 50/50 coin for a pending double-or-nothing offer: doubles the net win, or claws it back on a loss.</summary>
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
            decimal balance = await CreditService.AddCreditsAsync(db, UserId, ServerId, prize, "don_win");

            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = _embed.BuildSimpleEmbed(
                    "⚡  Double-or-Nothing — WIN!",
                    $"🪙 The coin landed in your favour!\n\n" +
                    $"**{CreditHelper.Format(offer.amount)}** → **{CreditHelper.Format(prize)}** 🎉",
                    ColourGold,
                    footer: Username, footerIconUrl: AvatarUrl,
                    fields: [("Payout", CreditHelper.Format(prize), true),
                             ("Balance", CreditHelper.Format(balance), true)]).Build();
                m.Components = new ComponentBuilder().Build();
            });
        }
        else
        {
            // Loss: claw back the original win
            await CreditService.DeductCreditsAsync(db, UserId, ServerId, offer.amount, "don_loss");
            decimal balance = await CreditService.GetBalanceAsync(db, UserId, ServerId);

            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = _embed.BuildSimpleEmbed(
                    "⚡  Double-or-Nothing — LOSS",
                    $"💸 The coin wasn't kind.\n\n" +
                    $"You lost **{CreditHelper.Format(offer.amount)}** back.",
                    ColourLoss,
                    footer: Username, footerIconUrl: AvatarUrl,
                    fields: [("Lost", CreditHelper.Format(offer.amount), true),
                             ("Balance", CreditHelper.Format(balance), true)]).Build();
                m.Components = new ComponentBuilder().Build();
            });
        }
    }

    [ComponentInteraction(BtnDonDecline)]
    /// <summary>Declines a pending double-or-nothing offer, keeping the original winnings as-is.</summary>
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
    [CommandContextType(InteractionContextType.Guild)]
    /// <summary>Owner-only toggle for "lucky mode" on this server, which re-rolls losing gambles at 60% win / 40% loss.</summary>
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
                embed: _embed.BuildSimpleEmbed(
                    "Lucky Mode — OFF",
                    $"Lucky mode **disabled** for **{Context.Guild.Name}**.\nOdds have returned to normal.",
                    ColourLoss, footer: Username, footerIconUrl: AvatarUrl).Build(),
                ephemeral: true);
        }
        else
        {
            _luckyServers.Add(ServerId);
            await FollowupAsync(
                embed: _embed.BuildSimpleEmbed(
                    "Lucky Mode — ON",
                    $"Lucky mode **enabled** for **{Context.Guild.Name}**.\nAll gambling losses are re-rolled at **60% win / 40% loss**.",
                    ColourGold, footer: Username, footerIconUrl: AvatarUrl).Build(),
                ephemeral: true);
        }
    }

    /// <summary>Increments challenge progress for the given game type. Non-fatal.</summary>
    private async Task TrackChallengeAsync(string gameType)
    {
        try { await ChallengeService.IncrementProgressAsync(db, UserId, ServerId, gameType); }
        catch { }
    }

    /// <summary>Write a gamble result to the log (used for draws/pushes that skip ApplyGambleAsync).</summary>
    private async Task LogGambleAsync(string game, decimal bet, decimal payout)
    {
        try
        {
            db.GambleLogs.Add(new GambleLog { UserId = UserId, ServerId = ServerId, Game = game, Bet = bet, Payout = payout, Net = payout - bet });
            await db.SaveChangesAsync();
        }
        catch { /* log failure is non-fatal */ }
    }

    /// <summary>Posts a standard "you're on cooldown" error with the remaining time.</summary>
    private async Task CooldownAsync(TimeSpan remaining) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed(
            "Gambling",
            $"Slow down! Try again in **{remaining.Seconds}s**.",
            Username).Build(), ephemeral: true);

    /// <summary>Posts a standard Gambling-branded error embed.</summary>
    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Gambling", message, Username).Build());
}