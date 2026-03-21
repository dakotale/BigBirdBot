using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Old-west style duel between two users.
/// The challenger issues a challenge; the target has 30 s to accept.
/// Winner takes 10 % of the loser's current balance.
/// </summary>
public class Duel : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
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

    // key = "serverId:targetUserId"
    private record DuelChallenge(string ChallengerId, string ChallengerName, string ServerId, DateTime Expiry);
    private static readonly ConcurrentDictionary<string, DuelChallenge> _pending = new();

    private static readonly TimeSpan ChallengeWindow = TimeSpan.FromSeconds(30);
    private static readonly string[] DrawLines =
    [
        "🌵  The sun beats down on the dusty street…",
        "🤠  Both hands hover over their holsters…",
        "⏳  The tension is unbearable…",
        "🔫  **DRAW!**"
    ];

    // ── /duel ─────────────────────────────────────────────────────────────────

    [SlashCommand("duel", "Challenge another user to an old-west duel for a random percentage of their credits!")]
    [EnabledInDm(false)]
    public async Task HandleDuelAsync(
        [Summary("user", "The user you want to challenge")] IUser target)
    {
        await DeferAsync();

        if (target.Id == Context.User.Id)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  Hold On, Cowboy")
                .WithColor(ColourRed)
                .WithDescription("You can't duel yourself.")
                .Build(), ephemeral: true);
            return;
        }

        if (target.IsBot)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  Hold On, Cowboy")
                .WithColor(ColourRed)
                .WithDescription("Bots don't carry credits.")
                .Build(), ephemeral: true);
            return;
        }

        string challengeKey = $"{ServerId}:{target.Id}";

        if (_pending.ContainsKey(challengeKey))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  Already Challenged")
                .WithColor(ColourRed)
                .WithDescription($"{target.Mention} already has a pending duel challenge.")
                .Build(), ephemeral: true);
            return;
        }

        _pending[challengeKey] = new DuelChallenge(UserId, Username, ServerId, DateTime.UtcNow.Add(ChallengeWindow));

        var buttons = new ComponentBuilder()
            .WithButton("Accept", $"duel:accept:{Context.User.Id}", ButtonStyle.Danger, new Emoji("🔫"))
            .WithButton("Decline", $"duel:decline:{Context.User.Id}", ButtonStyle.Secondary, new Emoji("🏳️"))
            .Build();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🤠  Duel Challenge!")
            .WithColor(ColourGold)
            .WithDescription(
                $"{target.Mention}, **{Username}** has challenged you to a duel!\n\n" +
                $"The winner takes **a random percentage** of the loser's credits.\n\n" +
                $"⏳ You have **30 seconds** to accept or decline.")
            .WithThumbnailUrl(AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), components: buttons);

        // Auto-expire the challenge
        _ = Task.Run(async () =>
        {
            await Task.Delay(ChallengeWindow);
            if (_pending.TryRemove(challengeKey, out _))
            {
                // Challenge expired — edit the original message if still accessible
                try
                {
                    var original = await Context.Interaction.GetOriginalResponseAsync();
                    await original.ModifyAsync(m =>
                    {
                        m.Embed = new EmbedBuilder()
                            .WithTitle("🤠  Duel Expired")
                            .WithColor(ColourGrey)
                            .WithDescription($"{target.Mention} didn't respond in time. The duel is off.")
                            .WithCurrentTimestamp()
                            .Build();
                        m.Components = new ComponentBuilder().Build();
                    });
                }
                catch { /* message deleted or inaccessible */ }
            }
        });
    }

    // ── Button: accept ────────────────────────────────────────────────────────

    [ComponentInteraction("duel:accept:*")]
    public async Task HandleAcceptAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out var challenge))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  No Pending Duel")
                .WithColor(ColourRed)
                .WithDescription("This duel has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        if (challenge.ChallengerId != challengerIdStr)
        {
            _pending[challengeKey] = challenge; // put it back
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  Not Your Duel")
                .WithColor(ColourRed)
                .WithDescription("Only the challenged player can accept.")
                .Build(), ephemeral: true);
            return;
        }

        // Only the target (the one challenged) may accept
        if (Context.User.Id.ToString() == challengerIdStr)
        {
            _pending[challengeKey] = challenge; // put it back
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  Not Your Duel")
                .WithColor(ColourRed)
                .WithDescription("You issued this challenge — you can't accept it.")
                .Build(), ephemeral: true);
            return;
        }

        // Disable buttons immediately
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
            m.Components = new ComponentBuilder().Build());

        // ── Dramatic build-up ──────────────────────────────────────────────────
        var buildupMsg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🤠  The Duel Begins!")
            .WithColor(ColourGold)
            .WithDescription(DrawLines[0])
            .WithCurrentTimestamp()
            .Build());

        foreach (var line in DrawLines[1..])
        {
            await Task.Delay(1200);
            await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle("🤠  The Duel Begins!")
                .WithColor(ColourGold)
                .WithDescription(line)
                .WithCurrentTimestamp()
                .Build());
        }

        await Task.Delay(800);

        // ── Resolve outcome ────────────────────────────────────────────────────
        string targetId     = Context.User.Id.ToString();
        string challengerId = challenge.ChallengerId;
        string srv          = challenge.ServerId;

        decimal targetBal     = _eco.GetBalance(targetId, srv);
        decimal challengerBal = _eco.GetBalance(challengerId, srv);

        bool challengerWins = Random.Shared.Next(2) == 0;

        string winnerId   = challengerWins ? challengerId : targetId;
        string loserId    = challengerWins ? targetId     : challengerId;
        decimal loserBal  = challengerWins ? targetBal    : challengerBal;

        Random r = new Random();
        decimal value = 0.1m + (1.0m - 0.1m) * (decimal)r.NextDouble();

        decimal prize = Math.Floor(loserBal * value);
        if (prize < 1) prize = 1;

        _eco.DeductCredits(loserId,  srv, prize, "duel_loss");
        _eco.AddCredits(winnerId,    srv, prize, "duel_win");

        string winnerMention = $"<@{winnerId}>";
        string loserMention  = $"<@{loserId}>";

        await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle("🔫  The Smoke Clears…")
            .WithColor(ColourGreen)
            .WithDescription(
                $"{winnerMention} was **faster on the draw!** 🏆\n\n" +
                $"{loserMention} drops their iron and hands over " +
                $"{CreditHelper.Format(prize)} **({Math.Round(value * 100.0m, 1, MidpointRounding.AwayFromZero).ToString("0.0")}% of their balance)**.\n\n" +
                $"💰 {winnerMention} walks away richer.")
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Button: decline ───────────────────────────────────────────────────────

    [ComponentInteraction("duel:decline:*")]
    public async Task HandleDeclineAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out _))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("🤠  No Pending Duel")
                .WithColor(ColourRed)
                .WithDescription("This duel has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle("🏳️  Duel Declined")
                .WithColor(ColourGrey)
                .WithDescription($"{Context.User.Mention} backed down from the duel. No credits were exchanged.")
                .WithCurrentTimestamp()
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }
}
