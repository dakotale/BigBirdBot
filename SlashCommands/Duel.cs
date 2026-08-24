using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using System.Collections.Concurrent;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Multi-theme duel between two users.
/// The challenger picks a game theme; the target has 30 s to accept.
/// Winner takes a random percentage (10–100 %) of the loser's current balance.
/// </summary>
public class Duel(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username  => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId    => Context.User.Id.ToString();
    private string ServerId  => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourRed   = EmbedColors.Red;
    private static readonly Color ColourGreen = EmbedColors.Green;
    private static readonly Color ColourGrey  = EmbedColors.Grey;

    private record DuelChallenge(string ChallengerId, string ChallengerName, string ServerId, DateTime Expiry, string Theme);
    private static readonly ConcurrentDictionary<string, DuelChallenge> _pending = new();
    private static readonly TimeSpan ChallengeWindow = TimeSpan.FromSeconds(30);

    // ── Theme registry ─────────────────────────────────────────────────────────

    private record ThemeData(
        Color      AccentColor,
        string     ChallengeTitle,
        string     AcceptTitle,
        string     WinTitle,
        string[]   Characters,
        string[]   ChallengeLines,
        string[][] BuildupSequences,
        string[]   WinLines,
        string[]   DeclineLines,
        string[]   ExpireLines);

    private static readonly Dictionary<string, ThemeData> Themes = new()
    {
        // ── VALORANT ──────────────────────────────────────────────────────────
        ["valorant"] = new ThemeData(
            AccentColor:    new Color(255, 70, 85),
            ChallengeTitle: "🔫  1v1 Challenge Incoming!",
            AcceptTitle:    "⚡  Agents Locked In!",
            WinTitle:       "🏆  Round Over — GG!",
            Characters:
            [
                // Duelists
                "Jett", "Reyna", "Phoenix", "Neon", "Raze", "Iso", "Yoru",
                // Initiators
                "Sova", "Breach", "Skye", "KAY/O", "Fade", "Gekko",
                // Controllers
                "Brimstone", "Viper", "Omen", "Astra", "Harbor", "Clove",
                // Sentinels
                "Cypher", "Killjoy", "Sage", "Chamber", "Deadlock", "Vyse",
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
                "said 'diff' in all chat and now needs to back it up.",
                "challenged you to a knife fight at mid. No abilities allowed.",
                "just hit a one-tap through smoke and thinks they're invincible.",
                "has been holding this angle for 30 seconds waiting for you.",
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
                    "😤  Both players triple-checking their sensitivity settings…",
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
                "{winner} had 400 hours in aim trainers and it showed. {loser} is shaking.",
                "{winner} shoulder-peeked once and that was enough. {loser} is uninstalling.",
                "{winner} called the correct angle. {loser} was holding the wrong wall.",
                "{winner} flash-peeked the corner. {loser} was fully blinded.",
                "{winner} hit a no-scope flick. {loser} is already typing in the rant channel.",
                "{winner} one-tapped through the smoke. {loser} didn't even see it coming.",
                "{winner} jiggle-peeked and won the duel in a single frame. {loser} is stunned.",
                "{winner} naded the corner and prefired the exit. {loser} walked right into it.",
                "{winner} stayed calm on the 1v1 eco round. {loser} panic-sprayed the wall.",
                "{winner} bated the ability and punished the recovery. Textbook. {loser} is silent.",
                "{winner} planted, watched the corner, and took the free kill. {loser} ego-peeked.",
                "{winner} clutched on a 1v1 in the most stressful situation imaginable. {loser} applauds.",
            ],
            DeclineLines:
            [
                "uninstalled Valorant and walked away.",
                "hid in spawn and let the round timer run out.",
                "switched to Deathmatch and pretended nothing happened.",
                "disconnected from the server. *Very* suspicious.",
                "claimed they had a ping spike and bailed.",
                "said \"one sec\" and never came back.",
                "pulled up their crosshair settings and got lost in the menus.",
                "queued for a different map and hope nobody noticed.",
                "switched to Sentinel and started placing traps instead of fighting.",
                "opened the agent store and spent 30 seconds looking at skins.",
                "blamed their mouse and closed the game.",
                "reported the challenge as a bug and filed a support ticket.",
            ],
            ExpireLines:
            [
                "The challenge timed out. One agent was clearly AFK in spawn.",
                "30 seconds passed. Someone was hiding in a corner.",
                "No response. Possibly tabbed out watching pro play.",
                "Challenge expired. The enemy team is already B site.",
                "Timer ran out. They were probably adjusting their crosshair colour.",
                "No one answered. The round ended and they were still in buy phase.",
            ]
        ),

        // ── OVERWATCH 2 ───────────────────────────────────────────────────────
        ["overwatch"] = new ThemeData(
            AccentColor:    new Color(249, 158, 26),
            ChallengeTitle: "💥  Hero Challenge Incoming!",
            AcceptTitle:    "🦸  Heroes Assembled!",
            WinTitle:       "🏆  Enemy Team Eliminated — GG!",
            Characters:
            [
                // Tank
                "D.Va", "Doomfist", "Junker Queen", "Mauga", "Orisa",
                "Ramattra", "Reinhardt", "Roadhog", "Sigma", "Winston",
                "Wrecking Ball", "Zarya",
                // Damage
                "Ashe", "Bastion", "Cassidy", "Echo", "Genji", "Hanzo",
                "Junkrat", "Mei", "Pharah", "Reaper", "Sojourn",
                "Soldier: 76", "Sombra", "Symmetra", "Torbjörn", "Tracer",
                "Venture", "Widowmaker",
                // Support
                "Ana", "Baptiste", "Brigitte", "Illari", "Juno", "Kiriko",
                "Lifeweaver", "Lúcio", "Mercy", "Moira", "Zenyatta",
            ],
            ChallengeLines:
            [
                "says your ult charge is at 0% and they're ready to pop off.",
                "is flanking you from spawn right now.",
                "switched to a hard counter and wants to prove a point.",
                "has you in their scope. It's high noon somewhere.",
                "popped their ult and is walking straight at you.",
                "typed GG in chat before the duel even started.",
                "says your hero choice was a throw pick — prove them wrong.",
                "has you pinned on the payload and isn't moving.",
                "nano-boosted themselves and is charging your position.",
                "said 'tank diff' in voice and is about to back it up.",
                "switched off-meta just to beat you and make it embarrassing.",
                "stood on point, looked at the camera, and pointed at you.",
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
                    "🚀  Pharah takes to the sky. *JUSTICE RAINS FROM ABOVE!*",
                    "💀  **ELIMINATED.**",
                ],
                [
                    "🎌  **Genji draws his blade. Honour demands a clean fight.**",
                    "🌀  *Ryūjin no ken wo kurae!* — Dragon Blade is active.",
                    "🏃  One hero dashes. The other deflects.",
                    "😱  The deflect connects — it's going back—",
                    "🗡️  **THE BLADE STRIKES TRUE.**",
                ],
                [
                    "🤖  **NERF THIS!**",
                    "🚀  D.Va ejects from her mech mid-fight.",
                    "💣  Self-Destruct activated. 5… 4… 3…",
                    "😬  Someone forgot you can shoot it…",
                    "💥  **KABOOM.**",
                ],
                [
                    "👵  **Ana readies her sleep dart. One shot, one chance.**",
                    "💤  The dart flies through the air…",
                    "😴  One hero falls asleep mid-ult.",
                    "🔫  The other lines up a careful headshot…",
                    "🩸  **NAILED IT.**",
                ],
            ],
            WinLines:
            [
                "{winner} popped their ult at the perfect moment. {loser} had no answer.",
                "{winner} landed a clutch sleep dart and followed up immediately. {loser} never woke up.",
                "{winner} flanked from spawn and {loser} didn't check their six.",
                "{winner} had the high ground and made it count. {loser} forgot to look up.",
                "{winner} one-tapped with Widowmaker. {loser} is reporting the server.",
                "{winner} deflected {loser}'s ult right back at them. Embarrassing.",
                "{winner} popped Self-Destruct and {loser} forgot to shoot it. Classic.",
                "{winner} held the angle and {loser} dry-peeked into it three times.",
                "{winner} nano-boosted at the right second. {loser} couldn't keep up.",
                "{winner} landed a Rein Earthshatter and {loser} never got back up.",
                "{winner} hook-shotted around the corner and {loser} had no idea where they went.",
                "{winner} used Sound Barrier at the clutch moment. {loser} got nothing off.",
                "{winner} gravved {loser} into the pit. Environmental. That counts.",
                "{winner} hacked {loser} mid-ult and the whole thing was wasted.",
                "{winner} out-positioned {loser} and won through sheer map awareness alone.",
                "{winner} booped {loser} off the edge. No abilities, just good timing.",
            ],
            DeclineLines:
            [
                "switched to a support and hid behind the payload.",
                "typed 'gg no re' and left the lobby.",
                "blamed lag and backed out before the duel started.",
                "swapped heroes five times and never locked in.",
                "said 'this hero isn't meta' and refused to engage.",
                "respawned at base and sat there.",
                "asked for a group up and then walked in the opposite direction.",
                "popped Transcendence and became immune to fun.",
                "called a tank diff in voice and then went Support.",
                "started charging a Graviton Surge and panicked mid-charge.",
                "teleported away with Sombra and vanished from the conversation.",
                "placed a shield, stood behind it, and said nothing.",
            ],
            ExpireLines:
            [
                "Challenge timed out. Someone was too busy spamming 'I need healing.'",
                "No response. They probably got nano-boosted in the wrong direction.",
                "30 seconds up. One hero went AFK on point.",
                "Expired. Looks like they swapped to support and hid.",
                "Timer ran out. They were tabbed out complaining about role queue.",
                "No one answered. The payload was moving and they had priorities.",
            ]
        ),

        // ── LEAGUE OF LEGENDS ─────────────────────────────────────────────────
        ["league"] = new ThemeData(
            AccentColor:    new Color(200, 155, 60),
            ChallengeTitle: "⚔️  All Chat: GG EZ Before It Starts",
            AcceptTitle:    "🐉  Minions Are Spawning!",
            WinTitle:       "🏆  GG — Surrender at 15!",
            Characters:
            [
                // Top
                "Darius", "Garen", "Fiora", "Camille", "Jax", "Malphite",
                "Sett", "Irelia", "Aatrox", "Renekton", "Nasus", "Teemo",
                "Tryndamere", "Vladimir", "Cho'Gath", "Gangplank", "Kennen",
                // Jungle
                "Lee Sin", "Vi", "Warwick", "Hecarim", "Graves", "Kindred",
                "Volibear", "Kha'Zix", "Rengar", "Evelynn", "Master Yi",
                "Nocturne", "Shaco", "Amumu", "Elise", "Nidalee", "Zac",
                // Mid
                "Yasuo", "Ahri", "Lux", "Zed", "Syndra", "Viktor", "Orianna",
                "Ekko", "Veigar", "Annie", "Katarina", "Twisted Fate", "Akali",
                "Yone", "Fizz", "LeBlanc", "Cassiopeia", "Lissandra", "Ryze",
                // Bot
                "Jinx", "Caitlyn", "Ezreal", "Vayne", "Jhin", "Tristana",
                "Ashe", "Xayah", "Draven", "Miss Fortune", "Lucian",
                "Kai'Sa", "Samira", "Zeri", "Nilah", "Sivir",
                // Support
                "Thresh", "Soraka", "Leona", "Blitzcrank", "Lulu", "Nami",
                "Morgana", "Pyke", "Nautilus", "Zilean", "Yuumi", "Senna",
            ],
            ChallengeLines:
            [
                "called you out in all chat. Everyone can see this.",
                "typed 'ez' in champion select before the game started.",
                "is perma-shoving your lane and sitting under your tower.",
                "took your jungle camp and is now staring you down.",
                "sent a duel request in /all. The whole server is watching.",
                "instalocked a carry and said 'diff' already.",
                "says your champion is Iron-tier and wants to prove it.",
                "pinged your cooldowns and is walking up. Bold.",
                "drew a crowd at the baron pit and called you out by name.",
                "set up a 1v1 custom lobby and is waiting at mid.",
                "bought a stopwatch just to make this more dramatic.",
                "said 'this champ is broken' and picked it to prove their point.",
            ],
            BuildupSequences:
            [
                [
                    "🏰  **Both summoners load onto the Rift…**",
                    "🌊  Minion waves crash into each other at mid.",
                    "📍  First blood available. One wrong step…",
                    "⚡  Yasuo dashes through the minion wave—",
                    "💀  **FIRST BLOOD!**",
                ],
                [
                    "🐉  **Drake spawns. Both junglers converge simultaneously.**",
                    "🔴  Smite is off cooldown. The dragon is at 1 HP.",
                    "😤  One player hovers their cursor over the button…",
                    "🎯  The other flashes in for the steal—",
                    "🐲  **OBJECTIVE SECURED.**",
                ],
                [
                    "🟣  **Baron Nashor awakens at 20 minutes.**",
                    "💀  Four members are dead. One carries the whole team in.",
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
                    "🌸  Petals fall across the Rift as Jhin lines up—",
                    "🔢  One. Two. Three.",
                    "🎵  *The fourth shot is the most beautiful.*",
                    "💫  **CURTAINS.**",
                ],
            ],
            WinLines:
            [
                "{winner} solo carried the duel. {loser} is typing 'report' in all chat.",
                "{winner} stole the Baron smite and it's over. {loser} has no words.",
                "{winner} landed a five-hit Jinx passive. {loser} exploded.",
                "{winner} gapped {loser} so hard they're still loading back in.",
                "{winner} ulted through the minion wave and connected. {loser} is stunned.",
                "{winner} split pushed while {loser} grouped. Macro wins again.",
                "{winner} hit the outplay and {loser} is now typing a novel in all chat.",
                "{winner} called the right angle. {loser} is blaming their jungler.",
                "{winner} flash-ulted and {loser} burned both Summoner spells for nothing.",
                "{winner} oneshot {loser} from fog of war. They never knew what happened.",
                "{winner} built the perfect counter item and {loser} refused to adapt.",
                "{winner} freeze-farmed all laning phase and won the late-game cleanly.",
                "{winner} set up a kill with vision control. {loser} walked into the trap.",
                "{winner} hit a skill shot that had no right connecting. {loser} is speechless.",
                "{winner} dove {loser} under turret at level 6 and walked out alive.",
                "{winner} hard engaged at the perfect moment. {loser}'s cooldowns were all down.",
            ],
            DeclineLines:
            [
                "typed 'afk' and walked to fountain.",
                "said 'not my role' and refused to fight.",
                "blamed their team and logged off.",
                "surrendered at 15 before the duel even started.",
                "lost connection to the server. Convenient.",
                "picked Teemo support and went invisible.",
                "backed to base to 'fix' their build and never returned.",
                "pinged 'on my way' in the wrong direction.",
                "hid under turret and waited out the timer.",
                "requested a remake and refused to explain why.",
                "swapped to a scaling champion and said 'come back at 40 minutes.'",
                "called the challenge 'a cheese strat' and refused to engage on principle.",
            ],
            ExpireLines:
            [
                "Challenge timed out. One player was stuck on the loading screen.",
                "No response. They probably went back to farming minions.",
                "Expired. Someone called for a surrender vote instead.",
                "30 seconds up. They typed 'one sec' in all chat six minutes ago.",
                "Timer ran out. They were checking patch notes mid-game.",
                "No answer. Their jungler didn't gank and they lost motivation.",
            ]
        ),

        // ── DEADLOCK ──────────────────────────────────────────────────────────
        ["deadlock"] = new ThemeData(
            AccentColor:    new Color(100, 200, 180),
            ChallengeTitle: "🔩  Lane Callout: 1v1 in the Streets",
            AcceptTitle:    "💀  Souls on the Line!",
            WinTitle:       "🏆  Patron Secured — GG!",
            Characters:
            [
                "Abrams", "Bebop", "Calico", "Dynamo", "Grey Talon",
                "Haze", "Infernus", "Ivy", "Kelvin", "Lady Geist",
                "Lash", "McGinnis", "Mirage", "Mo & Krill", "Paradox",
                "Pocket", "Seven", "Shiv", "Vindicta", "Viscous",
                "Warden", "Wraith", "Yamato",
            ],
            ChallengeLines:
            [
                "is pushing your lane solo with a full soul stack.",
                "has your location from a trooper kill and is already walking over.",
                "bought a Warp Stone and is blinking straight at you.",
                "dropped an urn and dared you to pick it up.",
                "called a 1v1 in voice chat and everyone heard it.",
                "has maxed their ult and is staring at your patron.",
                "says your build is cope and your CS is worse.",
                "just denied your last-hit and won't stop staring.",
                "zip-lined across the map just to get in your face.",
                "parried your last shot and immediately pinged you.",
                "has a 1,000 soul advantage and wants to remind you every second.",
                "waltzed into your jungle, farmed your camps, and is now challenging you to a duel.",
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
                "{winner} denied {loser}'s last-hit and snowballed the rest of the lane.",
                "{winner} blinked past {loser}'s ability and one-shot them at point blank.",
                "{winner} secured the urn while {loser} was still loading their build.",
                "{winner} stacked souls all lane and {loser} had no answer late.",
                "{winner} flanked through the zip line. {loser} forgot to look up.",
                "{winner} parried the key ability and punished the cooldown. Clean read.",
                "{winner} used map control to force a bad fight. {loser} had no escape.",
                "{winner} landed a Bebop hook from across the lane. {loser} was pulled to their death.",
                "{winner} held the angle on the rooftop. {loser} peeked anyway. Twice.",
                "{winner} burned {loser}'s cooldowns with a bait and went in clean.",
                "{winner} carried the urn the entire map with {loser} chasing and never catching up.",
                "{winner} wall-jumped over the ability and came back down with the finishing blow.",
                "{winner} denied the retreat path and cleaned up the kill calmly. {loser} is quiet.",
            ],
            DeclineLines:
            [
                "recalled to base and pretended to buy items.",
                "zip-lined away and hasn't come back.",
                "blamed the server tick rate and refused to engage.",
                "switched lanes without saying anything.",
                "spent 30 seconds in the item shop and missed the window.",
                "dropped the urn and walked in the other direction.",
                "climbed a building and is now just watching from above.",
                "pinged 'enemy missing' and used that as their excuse to back off.",
                "blamed their spirit build and said the matchup is unfavourable.",
                "Paradox time-walked backwards out of the situation.",
                "Haze smoked the lane and disappeared into it without a word.",
                "said 'my patron is being hit' and ran away without looking back.",
            ],
            ExpireLines:
            [
                "Challenge timed out. One player was last-hitting under the tower.",
                "No response. They're probably still reading patch notes.",
                "30 seconds up. Someone took the zip line to the wrong lane.",
                "Expired. They bought the wrong item and needed a moment.",
                "Timer ran out. They were farming neutrals and lost track of time.",
                "No answer. Their patron was getting hit and they had bigger problems.",
            ]
        ),
    };

    // ── /duel ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a themed duel challenge with Accept/Decline buttons. Registers a 30-second
    /// pending challenge keyed by server+target, and auto-expires it (editing the message
    /// to show an expiry line) if nobody responds in time.
    /// </summary>
    [SlashCommand("duel", "Challenge another player to a 1v1 for a cut of their credits!")]
    [CommandContextType(InteractionContextType.Guild)]
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
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  Invalid Target", "You can't challenge yourself.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (target.IsBot)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  Invalid Target", "Bots don't carry credits.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        string challengeKey = $"{ServerId}:{target.Id}";

        if (_pending.ContainsKey(challengeKey))
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "⚠️  Already Queued", $"{target.Mention} already has a pending challenge. Wait for it to resolve.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(theme, out var t))
            t = Themes["valorant"];

        _pending[challengeKey] = new DuelChallenge(UserId, Username, ServerId, DateTime.UtcNow.Add(ChallengeWindow), theme);

        string challengerChar = t.Characters[Random.Shared.Next(t.Characters.Length)];
        string targetChar     = t.Characters[Random.Shared.Next(t.Characters.Length)];
        string challengeLine  = t.ChallengeLines[Random.Shared.Next(t.ChallengeLines.Length)];

        var buttons = new ComponentBuilder()
            .WithButton("Accept", $"duel:accept:{Context.User.Id}", ButtonStyle.Danger,    new Emoji("⚔️"))
            .WithButton("Decline", $"duel:decline:{Context.User.Id}", ButtonStyle.Secondary, new Emoji("🏳️"))
            .Build();

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            t.ChallengeTitle,
            $"**{Username}** ({challengerChar}) {challengeLine}\n\n" +
            $"{target.Mention} ({targetChar}), do you accept?\n\n" +
            $"The winner takes **a random cut** of the loser's credits.\n\n" +
            $"⏳ You have **30 seconds** to respond.",
            t.AccentColor).WithThumbnailUrl(AvatarUrl).Build(), components: buttons);

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
                        m.Embed = _embed.BuildSimpleEmbed(
                            "💤  Challenge Expired", $"{expireLine}\n\n{target.Mention} is no longer queued.",
                            ColourGrey).Build();
                        m.Components = new ComponentBuilder().Build();
                    });
                }
                catch { /* message deleted or inaccessible */ }
            }
        });
    }

    // ── Button: accept ─────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts a pending challenge: plays the theme's animated buildup sequence, then rolls
    /// a winner and transfers a random percentage of the loser's balance to the winner.
    /// </summary>
    [ComponentInteraction("duel:accept:*")]
    public async Task HandleAcceptAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out var challenge))
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  No Active Challenge", "This challenge has already been resolved or expired.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (challenge.ChallengerId != challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  Not Your Challenge", "Only the challenged player can accept.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (Context.User.Id.ToString() == challengerIdStr)
        {
            _pending[challengeKey] = challenge;
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  Not Your Challenge", "You issued this challenge — you can't accept your own.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(challenge.Theme, out var t))
            t = Themes["valorant"];

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
            m.Components = new ComponentBuilder().Build());

        // ── Animated build-up ─────────────────────────────────────────────────
        string[] sequence = t.BuildupSequences[Random.Shared.Next(t.BuildupSequences.Length)];

        var buildupMsg = await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            t.AcceptTitle, sequence[0], t.AccentColor).Build());

        foreach (var frame in sequence[1..])
        {
            await Task.Delay(1400);
            await buildupMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
                t.AcceptTitle, frame, t.AccentColor).Build());
        }

        await Task.Delay(900);

        // ── Resolve outcome ────────────────────────────────────────────────────
        string targetId     = Context.User.Id.ToString();
        string challengerId = challenge.ChallengerId;
        string srv          = challenge.ServerId;

        decimal targetBal     = await CreditService.GetBalanceAsync(db, targetId, srv);
        decimal challengerBal = await CreditService.GetBalanceAsync(db, challengerId, srv);

        bool challengerWins = Random.Shared.Next(2) == 0;

        string winnerId  = challengerWins ? challengerId : targetId;
        string loserId   = challengerWins ? targetId     : challengerId;
        decimal loserBal = challengerWins ? targetBal    : challengerBal;

        decimal pct   = 0.10m + (decimal)Random.Shared.NextDouble() * 0.90m;
        decimal prize = Math.Floor(loserBal * pct);
        if (prize < 1) prize = 1;

        await CreditService.DeductCreditsAsync(db, loserId, srv, prize, "duel_loss");
        await CreditService.AddCreditsAsync(db, winnerId, srv, prize, "duel_win");

        string winnerMention = $"<@{winnerId}>";
        string loserMention  = $"<@{loserId}>";
        string pctDisplay    = (pct * 100m).ToString("0.0");

        string winLine = t.WinLines[Random.Shared.Next(t.WinLines.Length)]
            .Replace("{winner}", winnerMention)
            .Replace("{loser}",  loserMention);

        await buildupMsg.ModifyAsync(m => m.Embed = _embed.BuildSimpleEmbed(
            t.WinTitle,
            $"{winLine}\n\n" +
            $"💸 {loserMention} hands over **{CreditHelper.Format(prize)}** ({pctDisplay}% of their balance).\n" +
            $"💰 {winnerMention} walks away with the bag.",
            ColourGreen).Build());
    }

    // ── Button: decline ────────────────────────────────────────────────────────

    /// <summary>Declines a pending challenge — no credits change hands.</summary>
    [ComponentInteraction("duel:decline:*")]
    public async Task HandleDeclineAsync(string challengerIdStr)
    {
        await DeferAsync();

        string challengeKey = $"{ServerId}:{Context.User.Id}";

        if (!_pending.TryRemove(challengeKey, out var challenge))
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "❌  No Active Challenge", "This challenge has already been resolved or expired.",
                ColourRed, timestamp: false).Build(), ephemeral: true);
            return;
        }

        if (!Themes.TryGetValue(challenge.Theme, out var t))
            t = Themes["valorant"];

        string declineLine = t.DeclineLines[Random.Shared.Next(t.DeclineLines.Length)];

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                "🏳️  Challenge Declined", $"{Context.User.Mention} {declineLine}\n\nNo credits were exchanged.",
                ColourGrey).Build();
            m.Components = new ComponentBuilder().Build();
        });
    }
}
