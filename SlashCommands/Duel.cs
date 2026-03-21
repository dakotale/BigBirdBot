using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Valorant-themed agent duel between two users.
/// The challenger issues a challenge; the target has 30 s to accept.
/// Winner takes a random percentage (10–100 %) of the loser's current balance.
/// </summary>
public class Duel : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp  = new();
    private readonly EmbedHelper    _embed = new();
    private readonly Economy        _eco   = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId   => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourGold  = new(255, 215, 0);
    private static readonly Color ColourRed   = new(237, 66, 69);
    private static readonly Color ColourGreen = new(87, 242, 135);
    private static readonly Color ColourGrey  = new(128, 128, 128);
    private static readonly Color ColourValo  = new(255, 70, 85);   // Valorant red

    // key = "serverId:targetUserId"
    private record DuelChallenge(string ChallengerId, string ChallengerName, string ServerId, DateTime Expiry);
    private static readonly ConcurrentDictionary<string, DuelChallenge> _pending = new();

    private static readonly TimeSpan ChallengeWindow = TimeSpan.FromSeconds(30);

    // ── Flavour pools ──────────────────────────────────────────────────────────

    private static readonly string[] Agents =
    [
        "Jett", "Reyna", "Phoenix", "Neon", "Chamber", "Raze",
        "Omen", "Brimstone", "Viper", "Astra", "Harbor",
        "Sova", "Fade", "Gekko", "Skye",
        "Killjoy", "Cypher", "Sage", "Deadlock", "Iso",
    ];

    private static readonly string[] ChallengeLines =
    [
        "has locked in and is calling you out for a 1v1.",
        "wants to settle this on the range — agent vs agent.",
        "just pinged your location and is pushing site.",
        "is activating their ult and pointing it directly at you.",
        "challenged you to a duel. Do you have the guts to accept?",
        "says your aim is trash and is ready to prove it.",
        "called you out in comms. Time to back it up.",
        "is already mid-round peeking — what are you waiting for?",
    ];

    private static readonly string[] DeclineLines =
    [
        "uninstalled Valorant and walked away.",
        "hid in spawn and let the round timer run out.",
        "switched to Deathmatch and pretended nothing happened.",
        "disconnected from the server. *Very* suspicious.",
        "claimed they had a ping spike and bailed.",
        "turned off their monitor mid-round.",
        "said \"one sec\" and never came back.",
    ];

    private static readonly string[] ExpireLines =
    [
        "The challenge timed out. One agent was clearly AFK.",
        "30 seconds passed. Looks like someone was hiding in a corner.",
        "No response. Possibly tabbed out watching pro play.",
        "Challenge expired. The enemy team is probably already B site.",
    ];

    // Multiple full build-up sequences — one is picked at random per duel
    private static readonly string[][] BuildupSequences =
    [
        [
            "🗺️  **Map loaded. Both agents step onto site…**",
            "👁️  Eye contact across the corridor. No one blinks.",
            "🎯  Crosshairs lock. Fingers hover over the mouse…",
            "💨  One agent dashes — the other pre-aims the angle…",
            "🔫  **SHOTS FIRED.**",
        ],
        [
            "🌐  **Teleporting to the range. This ends now.**",
            "⚡  Abilities charged. Ultimates ready.",
            "😤  Both players checking their sensitivity settings one last time…",
            "🎮  The cursor moves. The trigger finger twitches…",
            "💥  **FIRE!**",
        ],
        [
            "🏙️  **The server loads. Spike is live. No time to waste.**",
            "🤫  Both agents hold their angles in dead silence…",
            "📡  Sova fires a recon dart — both positions revealed.",
            "🚨  Flash goes out. Someone's going in blind…",
            "🔫  **EXECUTE!**",
        ],
        [
            "🎲  **Random agent selected. The duel begins.**",
            "🧱  One agent is holding W. The other is peeking the corner.",
            "🎯  The aim assist kicks in — just kidding, this is Valorant.",
            "📉  Someone's hands are shaking. Their crosshair drifts…",
            "💀  **HEADSHOT.**",
        ],
        [
            "🗡️  **Knife only. No guns. Honour is on the line.**",
            "👟  Running footsteps echo through the corridor…",
            "😰  One agent crouches. The other jumps. Classic.",
            "🌀  Jett dashes left — or was it Chamber? Hard to tell.",
            "🔪  **FIRST BLOOD.**",
        ],
    ];

    private static readonly string[] WinLines =
    [
        "{winner} landed the headshot. {loser} never stood a chance.",
        "{winner} smoked the angle and peeked at the perfect moment. {loser} got clapped.",
        "{winner} activated their ult and the round was never close. GG {loser}.",
        "{winner} had a 400-hour aim trainer arc pay off. {loser} is shaking.",
        "{winner} shoulder-peeked once and that was enough. {loser} is uninstalling.",
        "{winner} called the correct angle. {loser} was holding the wrong wall.",
        "{winner} flash-peeked the corner. {loser} was fully blinded.",
        "{winner} hit a no-scope flick. {loser} is already in the rant channel.",
        "{winner} stayed calm and won the aim duel. {loser} panicked and sprayed.",
        "{winner} popped their ult at the worst possible moment for {loser}.",
    ];

    // ── /duel ──────────────────────────────────────────────────────────────────

    [SlashCommand("duel", "Challenge another agent to a 1v1 for a cut of their credits!")]
    [EnabledInDm(false)]
    public async Task HandleDuelAsync(
        [Summary("user", "The agent you want to challenge")] IUser target)
    {
        await DeferAsync();

        if (target.Id == Context.User.Id)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🎮  Easy There, Agent")
                .WithColor(ColourRed)
                .WithDescription("You can't duel yourself. Queue up a real opponent.")
                .Build(), ephemeral: true);
            return;
        }

        if (target.IsBot)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🎮  Invalid Target")
                .WithColor(ColourRed)
                .WithDescription("Bots don't carry credits. Pick a real agent.")
                .Build(), ephemeral: true);
            return;
        }

        string challengeKey = $"{ServerId}:{target.Id}";

        if (_pending.ContainsKey(challengeKey))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("⚠️  Already Queued")
                .WithColor(ColourRed)
                .WithDescription($"{target.Mention} already has a pending duel. Wait for it to resolve.")
                .Build(), ephemeral: true);
            return;
        }

        _pending[challengeKey] = new DuelChallenge(UserId, Username, ServerId, DateTime.UtcNow.Add(ChallengeWindow));

        string challengerAgent = Agents[Random.Shared.Next(Agents.Length)];
        string targetAgent     = Agents[Random.Shared.Next(Agents.Length)];
        string challengeLine   = ChallengeLines[Random.Shared.Next(ChallengeLines.Length)];

        var buttons = new ComponentBuilder()
            .WithButton("Accept", $"duel:accept:{Context.User.Id}", ButtonStyle.Danger, new Emoji("🔫"))
            .WithButton("Decline", $"duel:decline:{Context.User.Id}", ButtonStyle.Secondary, new Emoji("🏳️"))
            .Build();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🔫  1v1 Challenge Incoming!")
            .WithColor(ColourValo)
            .WithDescription(
                $"**{Username}** ({challengerAgent}) {challengeLine}\n\n" +
                $"{target.Mention} ({targetAgent}), do you accept?\n\n" +
                $"The winner takes **a random cut** of the loser's credits.\n\n" +
                $"⏳ You have **30 seconds** to respond.")
            .WithThumbnailUrl(AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), components: buttons);

        // Auto-expire the challenge
        _ = Task.Run(async () =>
        {
            await Task.Delay(ChallengeWindow);
            if (_pending.TryRemove(challengeKey, out _))
            {
                try
                {
                    string expireLine = ExpireLines[Random.Shared.Next(ExpireLines.Length)];
                    var original = await Context.Interaction.GetOriginalResponseAsync();
                    await original.ModifyAsync(m =>
                    {
                        m.Embed = new EmbedBuilder()
                            .WithTitle("💤  Challenge Expired")
                            .WithColor(ColourGrey)
                            .WithDescription($"{expireLine}\n\n{target.Mention} is no longer queued.")
                            .WithCurrentTimestamp()
                            .Build();
                        m.Components = new ComponentBuilder().Build();
                    });
                }
                catch { /* message deleted or inaccessible */ }
            }
        });
    }

    // ── Button: accept ─────────────────────────────────────────────────────────

    [ComponentInteraction("duel:accept:*")]
    public async Task HandleAcceptAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out var challenge))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  No Active Duel")
                .WithColor(ColourRed)
                .WithDescription("This duel has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        if (challenge.ChallengerId != challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Wrong Agent")
                .WithColor(ColourRed)
                .WithDescription("Only the challenged player can accept.")
                .Build(), ephemeral: true);
            return;
        }

        if (Context.User.Id.ToString() == challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Wrong Agent")
                .WithColor(ColourRed)
                .WithDescription("You issued this challenge — you can't accept your own queue.")
                .Build(), ephemeral: true);
            return;
        }

        // Disable buttons immediately
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
            m.Components = new ComponentBuilder().Build());

        // ── Animated build-up ─────────────────────────────────────────────────
        string[] sequence = BuildupSequences[Random.Shared.Next(BuildupSequences.Length)];

        var buildupMsg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("⚡  Agents Locked In!")
            .WithColor(ColourValo)
            .WithDescription(sequence[0])
            .WithCurrentTimestamp()
            .Build());

        foreach (var frame in sequence[1..])
        {
            await Task.Delay(1400);
            await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("⚡  Agents Locked In!")
                .WithColor(ColourValo)
                .WithDescription(frame)
                .WithCurrentTimestamp()
                .Build());
        }

        await Task.Delay(900);

        // ── Resolve outcome ────────────────────────────────────────────────────
        string targetId     = Context.User.Id.ToString();
        string challengerId = challenge.ChallengerId;
        string srv          = challenge.ServerId;

        decimal targetBal     = _eco.GetBalance(targetId, srv);
        decimal challengerBal = _eco.GetBalance(challengerId, srv);

        bool challengerWins = Random.Shared.Next(2) == 0;

        string winnerId  = challengerWins ? challengerId : targetId;
        string loserId   = challengerWins ? targetId     : challengerId;
        decimal loserBal = challengerWins ? targetBal    : challengerBal;

        decimal pct   = 0.10m + (decimal)Random.Shared.NextDouble() * 0.90m;
        decimal prize = Math.Floor(loserBal * pct);
        if (prize < 1) prize = 1;

        _eco.DeductCredits(loserId,  srv, prize, "duel_loss");
        _eco.AddCredits(winnerId,    srv, prize, "duel_win");

        string winnerMention = $"<@{winnerId}>";
        string loserMention  = $"<@{loserId}>";
        string pctDisplay    = (pct * 100m).ToString("0.0");

        string winTemplate = WinLines[Random.Shared.Next(WinLines.Length)];
        string winLine     = winTemplate
            .Replace("{winner}", winnerMention)
            .Replace("{loser}",  loserMention);

        await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🏆  Round Over — GG!")
            .WithColor(ColourGreen)
            .WithDescription(
                $"{winLine}\n\n" +
                $"💸 {loserMention} hands over **{CreditHelper.Format(prize)}** ({pctDisplay}% of their balance).\n" +
                $"💰 {winnerMention} walks away with the bag.")
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Button: decline ────────────────────────────────────────────────────────

    [ComponentInteraction("duel:decline:*")]
    public async Task HandleDeclineAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out _))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  No Active Duel")
                .WithColor(ColourRed)
                .WithDescription("This duel has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        string declineLine = DeclineLines[Random.Shared.Next(DeclineLines.Length)];

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle("🏳️  Challenge Declined")
                .WithColor(ColourGrey)
                .WithDescription($"{Context.User.Mention} {declineLine}\n\nNo credits were exchanged.")
                .WithCurrentTimestamp()
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }
}
