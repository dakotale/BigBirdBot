using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Multi-theme duel between two users.
/// The challenger picks a game theme; the target has 30 s to accept.
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

    private static readonly Color ColourRed   = new(237, 66, 69);
    private static readonly Color ColourGreen = new(87, 242, 135);
    private static readonly Color ColourGrey  = new(128, 128, 128);

    // key = "serverId:targetUserId"
    private record DuelChallenge(string ChallengerId, string ChallengerName, string ServerId, DateTime Expiry, string Theme);
    private static readonly ConcurrentDictionary<string, DuelChallenge> _pending = new();
    private static readonly TimeSpan ChallengeWindow = TimeSpan.FromSeconds(30);

    // ── Theme data ─────────────────────────────────────────────────────────────

    private record ThemeData(
        Color       AccentColor,
        string      ChallengeTitle,
        string      AcceptTitle,
        string      WinTitle,
        string[]    Characters,
        string[]    ChallengeLines,
        string[][]  BuildupSequences,
        string[]    WinLines,
        string[]    DeclineLines,
        string[]    ExpireLines);

    private static readonly Dictionary<string, ThemeData> Themes = new()
    {
        ["valorant"] = new ThemeData(
            AccentColor:    new Color(255, 70, 85),
            ChallengeTitle: "🔫  1v1 Challenge Incoming!",
            AcceptTitle:    "⚡  Agents Locked In!",
            WinTitle:       "🏆  Round Over — GG!",
            Characters:
            [
                "Jett", "Reyna", "Phoenix", "Neon", "Chamber", "Raze",
                "Omen", "Brimstone", "Viper", "Astra", "Harbor",
                "Sova", "Fade", "Gekko", "Skye",
                "Killjoy", "Cypher", "Sage", "Deadlock", "Iso",
            ],
            ChallengeLines:
            [
                "has locked in and is calling you out for a 1v1.",
                "wants to settle this on the range — agent vs agent.",
                "just pinged your location and is pushing site.",
                "is activating their ult and pointing it directly at you.",
                "says your aim is trash and is ready to prove it.",
                "called you out in comms. Time to back it up.",
                "is already mid-round peeking — what are you waiting for?",
                "dropped a duelist pick and wants to run it back.",
            ],
            BuildupSequences:
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
            ],
            WinLines:
            [
                "{winner} landed the headshot. {loser} never stood a chance.",
                "{winner} smoked the angle and peeked at the perfect moment. {loser} got clapped.",
                "{winner} activated their ult and the round was never close. GG {loser}.",
                "{winner} had a 400-hour aim trainer arc pay off. {loser} is shaking.",
                "{winner} shoulder-peeked once and that was enough. {loser} is uninstalling.",
                "{winner} called the correct angle. {loser} was holding the wrong wall.",
                "{winner} flash-peeked the corner. {loser} was fully blinded.",
                "{winner} hit a no-scope flick. {loser} is already in the rant channel.",
            ],
            DeclineLines:
            [
                "uninstalled Valorant and walked away.",
                "hid in spawn and let the round timer run out.",
                "switched to Deathmatch and pretended nothing happened.",
                "disconnected from the server. *Very* suspicious.",
                "claimed they had a ping spike and bailed.",
                "said \"one sec\" and never came back.",
            ],
            ExpireLines:
            [
                "The challenge timed out. One agent was clearly AFK in spawn.",
                "30 seconds passed. Someone was hiding in a corner.",
                "No response. Possibly tabbed out watching pro play.",
                "Challenge expired. The enemy team is already B site.",
            ]
        ),

        ["overwatch"] = new ThemeData(
            AccentColor:    new Color(249, 158, 26),
            ChallengeTitle: "💥  Hero Challenge Incoming!",
            AcceptTitle:    "🦸  Heroes Assembled!",
            WinTitle:       "🏆  Victory Royale — Wait, Wrong Game. GG!",
            Characters:
            [
                "Tracer", "Reaper", "Genji", "Cassidy", "Widowmaker",
                "Pharah", "Soldier: 76", "Ashe", "Hanzo", "Bastion",
                "Roadhog", "Reinhardt", "D.Va", "Winston", "Orisa",
                "Mercy", "Ana", "Moira", "Lucio", "Zenyatta", "Kiriko",
            ],
            ChallengeLines:
            [
                "says your ult charge is sitting at 0% and they're ready to pop off.",
                "is flanking you from spawn right now.",
                "switched to a hard counter and wants to prove a point.",
                "has you in their scope. It's high noon somewhere.",
                "is grouping up and calling you out for a 1v1.",
                "popped their ult and is walking straight at you.",
                "typed GG in chat before the duel even started.",
                "says your hero choice was a throw pick — prove them wrong.",
            ],
            BuildupSequences:
            [
                [
                    "🗺️  **Both heroes drop onto the payload map…**",
                    "🕐  *It's hiiiigh noon…*",
                    "🤠  Cassidy's hand hovers. The clock ticks.",
                    "⚡  Tracer blinks left, right, forward—",
                    "💥  **DEADEYE!**",
                ],
                [
                    "🦸  **Heroes lock in. The arena goes quiet.**",
                    "🔵  Shields up. Barriers deployed.",
                    "💊  *I need healing!* — Nobody heals them.",
                    "🚀  Pharah takes to the sky. Someone shouts JUSTICE RAINS FROM ABOVE—",
                    "💀  **ELIMINATED.**",
                ],
                [
                    "🎌  **Genji draws his blade. Honour demands a clean fight.**",
                    "🌀  *Ryūjin no ken wo kurae!* — Dragon Blade is active.",
                    "🏃  One hero dashes. The other deflects.",
                    "😱  The deflect lands back on the shooter—",
                    "🗡️  **THE BLADE STRIKES TRUE.**",
                ],
                [
                    "🤖  **NERF THIS!**",
                    "🚀  D.Va ejects from her mech mid-fight.",
                    "💣  Self-destruct activated. 5… 4… 3…",
                    "😬  Someone forgot you can shoot it…",
                    "💥  **KABOOM.**",
                ],
                [
                    "👵  **Ana readies her sleep dart. One shot, one chance.**",
                    "💤  The dart flies through the air…",
                    "😴  One hero falls asleep mid-ult.",
                    "🔫  The other lines up the shot carefully…",
                    "🩸  **NAILED IT.**",
                ],
            ],
            WinLines:
            [
                "{winner} popped their ult at the perfect moment. {loser} had no answer.",
                "{winner} landed a clutch sleep dart and followed up. {loser} never woke up.",
                "{winner} flanked from spawn and {loser} didn't check their six.",
                "{winner} had the high ground and made it count. {loser} forgot to look up.",
                "{winner} one-tapped with Widowmaker. {loser} is reporting the server.",
                "{winner} deflected {loser}'s ult right back at them. Embarrassing.",
                "{winner} used Self-Destruct and {loser} forgot to shoot it. Classic.",
                "{winner} held the angle and {loser} dry-peeked into it three times.",
            ],
            DeclineLines:
            [
                "switched to a support hero and hid behind the payload.",
                "typed 'gg no re' and left the lobby.",
                "blamed lag and backed out before the duel started.",
                "swapped heroes five times and never queued.",
                "said 'this hero isn't meta' and refused to engage.",
                "respawned back at base and stayed there.",
            ],
            ExpireLines:
            [
                "Challenge timed out. Someone was too busy spamming 'I need healing.'",
                "No response. They probably got nano-boosted in the wrong direction.",
                "30 seconds up. One hero went AFK on point.",
                "Expired. Looks like they switched to a support and hid.",
            ]
        ),

        ["league"] = new ThemeData(
            AccentColor:    new Color(200, 155, 60),
            ChallengeTitle: "⚔️  All Chat: GG EZ Before It Starts",
            AcceptTitle:    "🐉  Minions Are Spawning!",
            WinTitle:       "🏆  GG — Surren at 15!",
            Characters:
            [
                "Jinx", "Lux", "Zed", "Yasuo", "Ahri", "Thresh",
                "Jhin", "Ezreal", "Darius", "Garen", "Vi", "Ekko",
                "Akali", "Lee Sin", "Katarina", "Malphite", "Teemo",
                "Caitlyn", "Vayne", "Twisted Fate", "Yone", "Viego",
            ],
            ChallengeLines:
            [
                "called you out in all chat. Everyone can see this.",
                "typed 'ez' in champion select before the game started.",
                "is perma-shoving your lane and sitting under your tower.",
                "took your jungle camp and is now staring you down.",
                "sent a duel request in /all. The whole server is watching.",
                "instalocked a carry and said 'diff' already.",
                "says your champion is iron-tier and wants to prove it.",
                "pinged your cooldowns and is walking up. Bold.",
            ],
            BuildupSequences:
            [
                [
                    "🏰  **Both summoners load onto the rift…**",
                    "🌊  Minion waves crash into each other at mid.",
                    "📍  First blood available. One wrong step…",
                    "⚡  Yasuo dashes through the wave—",
                    "💀  **FIRST BLOOD!**",
                ],
                [
                    "🐉  **Drake spawns. Both junglers arrive at the same time.**",
                    "🔴  Smite is off cooldown. The dragon is at 1HP.",
                    "😤  One player hovers their cursor over the button…",
                    "🎯  The other flashes in for the steal—",
                    "🐲  **OBJECTIVE SECURED.**",
                ],
                [
                    "🟣  **Baron Nashor awakens at 20 minutes.**",
                    "💀  Four members are dead. One carries the whole team.",
                    "🔑  Baron buff or bust. No turning back.",
                    "👆  The last player smites into the pit…",
                    "👑  **BARON STOLEN. ACE.**",
                ],
                [
                    "🌿  **Teemo places a mushroom in the river.**",
                    "😬  Someone is walking straight toward it.",
                    "👀  They're one step away…",
                    "💥  *CLICK.*",
                    "☠️  **SURPRISE! — Teemo, the Swift Scout.**",
                ],
                [
                    "🎹  **The Virtuoso loads his rifle. Four shots. No more.**",
                    "🌸  Petals fall across the rift as Jhin lines up—",
                    "🔢  One. Two. Three.",
                    "🎵  *The fourth shot is the most beautiful.*",
                    "💫  **CURTAINS.**",
                ],
            ],
            WinLines:
            [
                "{winner} solo carried the duel. {loser} is typing 'report' already.",
                "{winner} stole the baron smite and it's over. {loser} has no words.",
                "{winner} landed a five-hit Jinx passive. {loser} exploded.",
                "{winner} gapped {loser} so hard they're still loading back in.",
                "{winner} ulted through a minion wave and connected. {loser} is stunned.",
                "{winner} split pushed while {loser} grouped. Macro wins again.",
                "{winner} hit the outplay and {loser} is now typing a novel in all chat.",
                "{winner} called the right angle. {loser} is blaming their jungler.",
            ],
            DeclineLines:
            [
                "typed 'afk' and walked to fountain.",
                "said 'not my role' and refused to fight.",
                "blamed their team and logged off.",
                "surrendered at 15 before the duel even started.",
                "lost connection to the server. Convenient.",
                "picked Teemo support and went invisible.",
            ],
            ExpireLines:
            [
                "Challenge timed out. One player was stuck on the loading screen.",
                "No response. They probably went back to farming minions.",
                "Expired. Someone called for a surrender vote instead.",
                "30 seconds up. They typed 'one sec' in all chat six minutes ago.",
            ]
        ),

        ["deadlock"] = new ThemeData(
            AccentColor:    new Color(100, 200, 180),
            ChallengeTitle: "🔩  Lane Callout: 1v1 in the Streets",
            AcceptTitle:    "💀  Souls on the Line!",
            WinTitle:       "🏆  Patron Secured — GG!",
            Characters:
            [
                "Abrams", "Bebop", "Dynamo", "Grey Talon", "Haze",
                "Infernus", "Ivy", "Kelvin", "Lady Geist", "Lash",
                "McGinnis", "Mirage", "Mo & Krill", "Paradox",
                "Pocket", "Seven", "Shiv", "Vindicta", "Viscous",
                "Warden", "Wraith", "Yamato",
            ],
            ChallengeLines:
            [
                "is pushing your lane solo with a full soul stack.",
                "has your location from a troopers kill and is already walking over.",
                "bought a Warp Stone and is blinking straight at you.",
                "dropped an urn and dared you to pick it up.",
                "called a 1v1 in voice chat and everyone heard it.",
                "has maxed their ult and is looking directly at your patron.",
                "says your build is cope and your farming is worse.",
                "just denied your last-hit and won't stop staring.",
            ],
            BuildupSequences:
            [
                [
                    "🏙️  **Both heroes drop into the lane. Troopers march past.**",
                    "👁️  Soul orbs glow between them. Neither moves.",
                    "🔩  One hero slots in a new item mid-fight.",
                    "⚡  Abilities light up the street—",
                    "💀  **SOULS CLAIMED.**",
                ],
                [
                    "🪙  **The urn spawns in the middle of the map.**",
                    "🏃  Both heroes sprint toward it at the same time.",
                    "😤  One slides in first — the other opens fire.",
                    "🌀  A blink, a dodge, a point-blank shot—",
                    "☠️  **URN SECURED.**",
                ],
                [
                    "🗼  **A patron is unguarded. One hero rushes in.**",
                    "🔥  Turret fire lights up the area.",
                    "🛡️  The defender arrives just in time — or do they?",
                    "💥  An ability detonates at close range—",
                    "🔱  **PATRON DESTROYED.**",
                ],
                [
                    "🌩️  **Seven channels his storm. The air crackles.**",
                    "⚡  Lightning arcs across the lane.",
                    "😱  The enemy tries to dash out of range—",
                    "📡  Storm Cloud catches them mid-air—",
                    "🌪️  **ZAPPED.**",
                ],
                [
                    "🐙  **Viscous pops Cube on themselves.**",
                    "😐  Their opponent hits the cube. And again. And again.",
                    "🎱  The cube bounces between buildings…",
                    "🏃  They run. The cube chases.",
                    "💥  **SPLAT.**",
                ],
            ],
            WinLines:
            [
                "{winner} out-farmed {loser} and the soul lead was insurmountable.",
                "{winner} activated their ult at exactly the right moment. {loser} had nothing.",
                "{winner} hit a perfect ability combo. {loser} was at half HP before they reacted.",
                "{winner} denied {loser}'s last-hit and snowballed the rest.",
                "{winner} blinked past {loser}'s ability and one-shot them at point blank.",
                "{winner} secured the urn while {loser} was still loading their build.",
                "{winner} stacked souls all lane and {loser} had no answer late.",
                "{winner} flanked through the zip line. {loser} forgot to look up.",
            ],
            DeclineLines:
            [
                "recalled to base and pretended to buy items.",
                "ziplined away and hasn't come back.",
                "blamed server tick rate and refused to engage.",
                "switched lanes without saying anything.",
                "spent 30 seconds in the shop and missed the window.",
                "dropped the urn and walked in the other direction.",
            ],
            ExpireLines:
            [
                "Challenge timed out. One player was last-hitting under the tower.",
                "No response. They're probably still reading patch notes.",
                "30 seconds up. Someone took the zip line to the wrong lane.",
                "Expired. They bought the wrong item and needed to think.",
            ]
        ),
    };

    // ── /duel ──────────────────────────────────────────────────────────────────

    [SlashCommand("duel", "Challenge another player to a 1v1 for a cut of their credits!")]
    [EnabledInDm(false)]
    public async Task HandleDuelAsync(
        [Summary("user", "The player you want to challenge")] IUser target,
        [Summary("theme", "Pick your battle theme"),
         Choice("Valorant",          "valorant"),
         Choice("Overwatch",         "overwatch"),
         Choice("League of Legends", "league"),
         Choice("Deadlock",          "deadlock")]
        string theme = "valorant")
    {
        await DeferAsync();

        if (target.Id == Context.User.Id)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Invalid Target")
                .WithColor(ColourRed)
                .WithDescription("You can't challenge yourself.")
                .Build(), ephemeral: true);
            return;
        }

        if (target.IsBot)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Invalid Target")
                .WithColor(ColourRed)
                .WithDescription("Bots don't carry credits.")
                .Build(), ephemeral: true);
            return;
        }

        string challengeKey = $"{ServerId}:{target.Id}";

        if (_pending.ContainsKey(challengeKey))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("⚠️  Already Queued")
                .WithColor(ColourRed)
                .WithDescription($"{target.Mention} already has a pending challenge. Wait for it to resolve.")
                .Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(theme, out var t))
            t = Themes["valorant"];

        _pending[challengeKey] = new DuelChallenge(UserId, Username, ServerId, DateTime.UtcNow.Add(ChallengeWindow), theme);

        string challengerChar = t.Characters[Random.Shared.Next(t.Characters.Length)];
        string targetChar     = t.Characters[Random.Shared.Next(t.Characters.Length)];
        string challengeLine  = t.ChallengeLines[Random.Shared.Next(t.ChallengeLines.Length)];

        var buttons = new ComponentBuilder()
            .WithButton("Accept", $"duel:accept:{Context.User.Id}", ButtonStyle.Danger, new Emoji("⚔️"))
            .WithButton("Decline", $"duel:decline:{Context.User.Id}", ButtonStyle.Secondary, new Emoji("🏳️"))
            .Build();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle(t.ChallengeTitle)
            .WithColor(t.AccentColor)
            .WithDescription(
                $"**{Username}** ({challengerChar}) {challengeLine}\n\n" +
                $"{target.Mention} ({targetChar}), do you accept?\n\n" +
                $"The winner takes **a random cut** of the loser's credits.\n\n" +
                $"⏳ You have **30 seconds** to respond.")
            .WithThumbnailUrl(AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), components: buttons);

        _ = Task.Run(async () =>
        {
            await Task.Delay(ChallengeWindow);
            if (_pending.TryRemove(challengeKey, out var expired))
            {
                try
                {
                    if (!Themes.TryGetValue(expired.Theme, out var et)) et = Themes["valorant"];
                    string expireLine = et.ExpireLines[Random.Shared.Next(et.ExpireLines.Length)];
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
                .WithTitle("❌  No Active Challenge")
                .WithColor(ColourRed)
                .WithDescription("This challenge has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        if (challenge.ChallengerId != challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Not Your Challenge")
                .WithColor(ColourRed)
                .WithDescription("Only the challenged player can accept.")
                .Build(), ephemeral: true);
            return;
        }

        if (Context.User.Id.ToString() == challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  Not Your Challenge")
                .WithColor(ColourRed)
                .WithDescription("You issued this challenge — you can't accept your own.")
                .Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(challenge.Theme, out var t))
            t = Themes["valorant"];

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
            m.Components = new ComponentBuilder().Build());

        // ── Animated build-up ─────────────────────────────────────────────────
        string[] sequence = t.BuildupSequences[Random.Shared.Next(t.BuildupSequences.Length)];

        var buildupMsg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle(t.AcceptTitle)
            .WithColor(t.AccentColor)
            .WithDescription(sequence[0])
            .WithCurrentTimestamp()
            .Build());

        foreach (var frame in sequence[1..])
        {
            await Task.Delay(1400);
            await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                .WithTitle(t.AcceptTitle)
                .WithColor(t.AccentColor)
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

        _eco.DeductCredits(loserId, srv, prize, "duel_loss");
        _eco.AddCredits(winnerId,   srv, prize, "duel_win");

        string winnerMention = $"<@{winnerId}>";
        string loserMention  = $"<@{loserId}>";
        string pctDisplay    = (pct * 100m).ToString("0.0");

        string winLine = t.WinLines[Random.Shared.Next(t.WinLines.Length)]
            .Replace("{winner}", winnerMention)
            .Replace("{loser}",  loserMention);

        await buildupMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
            .WithTitle(t.WinTitle)
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

        if (!_pending.TryRemove(challengeKey, out var challenge))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("❌  No Active Challenge")
                .WithColor(ColourRed)
                .WithDescription("This challenge has already been resolved or expired.")
                .Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(challenge.Theme, out var t))
            t = Themes["valorant"];

        string declineLine = t.DeclineLines[Random.Shared.Next(t.DeclineLines.Length)];

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
