using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /scramble — unscramble a word before time runs out.
/// Answer checking is handled in BotHost.OnMessageReceivedAsync.
/// DB state: AddScrambleGame / GetScrambleByChannel / DeleteScrambleGame / GetScrambleHint
/// </summary>
[Group("game", "Play a minigame.")]
public class Scramble : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username => Context.User.Username;


    private static readonly string[] Easy =
    [
        "cat", "dog", "sun", "hat", "map", "run", "log", "cup", "bed", "fox",
        "ant", "fly", "jar", "key", "mud", "net", "oak", "pan", "rat", "sky"
    ];

    private static readonly string[] Medium =
    [
        "flame", "grape", "chess", "brave", "clown", "digit", "flair", "gnome",
        "hatch", "irony", "joust", "kneel", "latch", "magic", "nerve", "ocean",
        "plumb", "query", "rivet", "slash", "torch", "ulcer", "vapor", "waltz",
        "xerox", "yield", "zones", "blast", "cramp", "dwarf"
    ];

    private static readonly string[] Hard =
    [
        "algorithm", "blueprint", "cathedral", "dimension", "eloquence",
        "franchise", "gauntlet",  "hierarchy", "inference", "juxtapose",
        "kaleidoscope", "labyrinth", "magnitude", "narrative", "obscurity",
        "parchment", "quandary",  "resonance", "syllable",  "threshold",
        "ultimatum", "variegated", "whirlpool", "xenophobe", "yardstick"
    ];

    private const int TimeoutSeconds = 45;


    [SlashCommand("scramble", "Unscramble the word before time runs out!")]
    [EnabledInDm(false)]
    public async Task HandleScrambleAsync(
        [Choice("Easy",   "easy"),
         Choice("Medium", "medium"),
         Choice("Hard",   "hard")]
        string difficulty = "medium")
    {
        await DeferAsync();

        // Check if a game is already running in this channel.
        var existing = _sp.Select(Constants.Constants.discordBotConnStr, "GetScrambleByChannel",
            [new SqlParameter("@ChannelID", Context.Channel.Id.ToString())]);

        if (existing.Rows.Count > 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Scramble",
                "A scramble is already running in this channel! Solve it first.",
                Username).Build());
            return;
        }

        string word = PickWord(difficulty);
        string scrambled = ScrambleWord(word);

        // Re-scramble if it happens to match the original (rare but possible on short words).
        int attempts = 0;
        while (scrambled == word && attempts++ < 10)
            scrambled = ScrambleWord(word);

        (Color colour, string label, string emoji) style = difficulty switch
        {
            "easy" => (Color.Green, "Easy", "🟢"),
            "hard" => (Color.Red, "Hard", "🔴"),
            _ => (Color.Orange, "Medium", "🟠")
        };

        var msg = await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🔤  Word Scramble  {style.emoji} {style.label}")
            .WithColor(style.colour)
            .WithDescription(
                $"## `{scrambled.ToUpperInvariant()}`\n\n" +
                $"Type the unscrambled word in this channel to win!\n" +
                $"⏱️ You have **{TimeoutSeconds} seconds**.")
            .WithFooter($"Started by {Username}")
            .WithCurrentTimestamp()
            .Build());

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddScrambleGame",
        [
            new SqlParameter("@ChannelID",  Context.Channel.Id.ToString()),
            new SqlParameter("@MessageID",  msg.Id.ToString()),
            new SqlParameter("@Answer",     word),
            new SqlParameter("@Difficulty", difficulty),
            new SqlParameter("@StartedBy",  Context.User.Id.ToString()),
            new SqlParameter("@ExpiresAt",  DateTime.UtcNow.AddSeconds(TimeoutSeconds))
        ]);

        // Capture these BEFORE Task.Run — Context is disposed after the interaction completes.
        var channelId = Context.Channel.Id;
        var messageId = msg.Id;
        var connStr = Constants.Constants.discordBotConnStr;
        var client = Context.Client;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds + 2));

            var check = new StoredProcedure().Select(connStr, "GetScrambleByChannel",
                [new SqlParameter("@ChannelID", channelId.ToString())]);

            // No row → already solved and deleted. Nothing to do.
            if (check.Rows.Count == 0) return;

            // A new game may have started in the same channel after this one expired.
            // Only act on the exact game we launched (match by MessageID).
            if (check.Rows[0]["MessageID"].ToString() != messageId.ToString()) return;

            new StoredProcedure().UpdateCreate(connStr, "DeleteScrambleGame",
                [new SqlParameter("@ChannelID", channelId.ToString())]);

            try
            {
                if (client.GetChannel(channelId) is IMessageChannel ch)
                {
                    await ch.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("⏰  Time's Up!")
                        .WithColor(Color.Red)
                        .WithDescription($"Nobody solved it! The word was **{word}**.")
                        .WithCurrentTimestamp()
                        .Build());

                    // Strike-through the original scramble embed to signal it's over.
                    if (await ch.GetMessageAsync(messageId) is IUserMessage original)
                        await original.ModifyAsync(m => m.Embed = new EmbedBuilder()
                            .WithTitle("🔤  Word Scramble — Expired")
                            .WithColor(Color.DarkGrey)
                            .WithDescription($"~~`{scrambled.ToUpperInvariant()}`~~\n\nNobody got it in time.")
                            .Build());
                }
            }
            catch { /* channel may be unavailable */ }
        });
    }


    private static string PickWord(string difficulty) => difficulty switch
    {
        "easy" => Easy[Random.Shared.Next(Easy.Length)],
        "hard" => Hard[Random.Shared.Next(Hard.Length)],
        _ => Medium[Random.Shared.Next(Medium.Length)]
    };

    /// <summary>Fisher-Yates shuffle on the characters of a word.</summary>
    private static string ScrambleWord(string word)
    {
        var chars = word.ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}
