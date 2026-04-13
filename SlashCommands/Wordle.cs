using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /wordle — classic 6-attempt word guessing game.
/// The bot edits the game embed after each guess submitted via chat message.
/// Answer checking is handled in BotHost.OnMessageReceivedAsync.
/// DB state: AddWordleGame / GetWordleByChannel / UpdateWordleGame / DeleteWordleGame
/// </summary>
public partial class Games
{
    private const int WordLength = 5;
    private const int MaxGuesses = 6;


    private static readonly string[] Words =
    [
        "apple", "brave", "chess", "dizzy", "eagle", "fable", "grace", "haste",
        "igloo", "joust", "kneel", "latch", "magic", "nerve", "ocean", "plumb",
        "query", "rivet", "slash", "torch", "ulcer", "vapor", "waltz", "xerox",
        "yield", "zones", "blast", "cramp", "dwarf", "flame", "grape", "clown",
        "digit", "flair", "gnome", "hatch", "irony", "abbey", "adobe", "aging",
        "agony", "album", "alert", "algae", "alibi", "alien", "align", "alley",
        "allow", "aloft", "alone", "aloof", "aloud", "alpen", "altar", "alter",
        "angel", "anger", "angle", "angry", "anime", "annex", "anvil", "aorta",
        "arbor", "armor", "aroma", "array", "arrow", "ascot", "ashen", "askew",
        "atlas", "attic", "audio", "audit", "augur", "avail", "award", "awful",
        "axiom", "azure", "badge", "bagel", "banjo", "baron", "basic", "basil",
        "basis", "batch", "bathe", "bayou", "beach", "beard", "beast", "began",
        "beige", "belle", "belly", "bench", "bevel", "bicep", "bingo", "birch",
        "bison", "biter", "black", "blade", "bland", "blank", "blaze", "bleak",
        "bleed", "blend", "bless", "bliss", "block", "blood", "bloom", "blown",
        "blunt", "blurb", "blurt", "blush", "board", "bogus", "boost", "booth",
        "botch", "bound", "boxer", "brace", "braid", "brand", "brash", "brawn",
        "bread", "break", "breed", "breve", "brisk", "broil", "brood", "brook",
        "broth", "brown", "brunt", "brush", "brute", "buddy", "budget", "build",
        "built", "bunch", "bunny", "buoy", "butch", "cabal", "cache", "cadet",
        "camel", "cameo", "candy", "cargo", "carry", "catch", "cause", "cavern",
        "cedar", "chant", "chaos", "cheap", "check", "cheek", "cheer", "chewy",
        "chief", "chill", "chimp", "choir", "chord", "circa", "civic", "civil",
        "clamm", "clang", "clash", "clasp", "class", "clean", "clear", "clerk",
        "click", "cliff", "cling", "cloak", "clock", "clone", "close", "cloud",
        "clout", "cluck", "clued", "clump", "coach", "coast", "cobra", "comet",
        "comic", "coral", "could", "count", "coupe", "court", "cover", "covet",
        "crack", "crave", "crawl", "creed", "creek", "creep", "crest", "crimp",
        "crisp", "cross", "crowd", "crown", "crush", "crust", "crypt", "cubic",
        "curry", "cycle", "cynic", "daddy", "daisy", "dance", "datum", "daunt",
        "deals", "dealt", "decay", "decor", "delay", "delta", "depot", "depth",
        "derby", "deter", "devil", "diary", "disco", "ditty", "divot", "dodgy",
        "dogma", "donor", "donut", "dopey", "doubt", "dough", "dowdy", "dowel",
        "dowry", "dozen", "draft", "drain", "drama", "drank", "drape", "drawl",
        "dread", "drier", "drill", "drink", "drool", "drove", "drugged", "drupe",
        "dusty", "dwelt", "dying", "eclat", "edged", "eight", "elite", "emote",
        "empty", "ended", "endow", "enemy", "enjoy", "ennui", "envoy", "epoch",
        "equip", "error", "essay", "ether", "event", "every", "evict", "exact",
        "exert", "exile", "exist", "expel", "extol", "exult", "fable", "facet",
        "faith", "false", "fancy", "fanny", "farce", "fatal", "fauna", "feast",
        "feint", "fence", "ferry", "fetch", "fever", "fiber", "fidelity", "fiend",
        "fifth", "fifty", "fight", "finch", "finicky", "first", "fishy", "fixed",
        "fizzy", "flank", "flare", "flash", "flask", "flaunt", "flick", "fling",
        "flint", "flinch", "float", "flock", "flood", "floss", "flout", "flown",
        "fluke", "flunk", "foamy", "focal", "foggy", "foray", "forge", "forgo",
        "found", "fraud", "freak", "fresh", "front", "froth", "froze", "frosted",
        "fruit", "frump", "fully", "funky", "funny", "futon", "fuzzy", "gaudy",
        "ghost", "giddy", "girth", "gizmo", "glade", "glair", "gland", "glare",
        "glaze", "gleam", "glean", "glide", "glint", "gloat", "gloss", "glove",
        "glyph", "godly", "golly", "gorge", "gouge", "gourd", "governess", "graft",
        "grand", "grant", "grasp", "gravel", "graze", "greed", "greet", "grief",
        "grill", "grind", "groan", "groom", "grope", "gross", "grout", "growl",
        "gruel", "gruff", "grump", "guise", "gusto", "gypsy", "heist", "herbs",
        "hippo", "holly", "homer", "honey", "honor", "horse", "hotel", "hound",
        "house", "howdy", "human", "humid", "hurry", "hyena", "hyper", "index",
        "indie", "inert", "infer", "inlay", "input", "inter", "intro", "ionic",
        "irate", "itchy", "ivory", "jazzy", "jelly", "jewel", "jiffy", "jingo",
        "joint", "jolly", "joker", "jovial", "judge", "juice", "jumbo", "jumpy",
        "kabob", "karma", "kebab", "knave", "knife", "knock", "koala", "kudos",
        "lance", "lapel", "lathe", "layer", "leafy", "leapt", "learn", "least",
        "legal", "lemon", "level", "light", "liner", "lingo", "liner", "liver",
        "llama", "lodge", "logic", "loopy", "lousy", "lover", "loyal", "lucid",
        "lucky", "lunar", "lusty", "lying", "lyric", "macro", "mafia", "mambo",
        "manor", "maple", "march", "marry", "match", "maxim", "media", "medic",
        "melee", "melon", "mercy", "merit", "messy", "metal", "micro", "might",
        "mimic", "minor", "minty", "mirth", "miser", "mitre", "model", "mogul",
        "money", "monks", "month", "moody", "moral", "morph", "mossy", "mount",
        "mourn", "moody", "muddy", "mulch", "mummy", "murky", "musty", "myrrh",
        "naive", "nifty", "night", "ninja", "nitro", "noble", "noise", "notch",
        "novel", "nymph", "offal", "offer", "often", "onset", "optic", "orbit",
        "order", "otaku", "otter", "ought", "ounce", "outdo", "outer", "ovary",
        "ovoid", "owing", "oxide", "ozone", "paddy", "paint", "pairs", "papal",
        "paper", "parka", "parse", "party", "patch", "patsy", "pause", "payee",
        "peace", "pearl", "pedal", "penny", "perch", "peril", "perky", "pesky",
        "petal", "petty", "phase", "phone", "photo", "piano", "picot", "pilot",
        "pinch", "pixie", "pizza", "place", "plaid", "plain", "plane", "plant",
        "plaza", "plead", "pleat", "plied", "plink", "pluck", "plume", "plunk",
        "point", "poise", "poker", "polar", "polka", "polyp", "pooch", "poppy",
        "portal", "pouch", "prank", "prawn", "press", "price", "pride", "prime",
        "primp", "print", "prism", "prize", "probe", "prong", "proof", "prose",
        "prowl", "prude", "psalm", "pubic", "puffy", "pulpy", "punch", "purge",
        "pushy", "pygmy", "qualm", "queen", "quick", "quiet", "quirk", "quota",
        "quote", "rabbi", "radar", "radii", "radio", "rainy", "rally", "ranch",
        "rapid", "raven", "reach", "ready", "realm", "rebel", "reign", "relax",
        "remix", "repay", "repel", "rerun", "resin", "retch", "retro", "revel",
        "rider", "ridge", "rifle", "right", "risky", "rival", "river", "robin",
        "robot", "rocky", "rouge", "rough", "round", "rouse", "rowdy", "ruler",
        "runny", "rusty", "sadly", "saint", "salsa", "sandy", "sassy", "sauce",
        "sauna", "savvy", "scald", "scalp", "scant", "scare", "scarf", "scene",
        "scone", "scoop", "scope", "score", "scout", "scram", "scrap", "scrub",
        "seize", "sense", "serum", "sever", "shake", "shaky", "shame", "shank",
        "shape", "sharp", "shawl", "sheen", "sheer", "shelf", "shell", "shift",
        "shirt", "shock", "shoot", "shore", "short", "shout", "shove", "shown",
        "shrug", "shuck", "shunt", "siege", "sieve", "sigma", "silly", "since",
        "sinew", "skill", "skimp", "skunk", "slain", "slant", "slate", "sleek",
        "sleet", "slept", "slice", "slide", "slime", "slimy", "sling", "slink",
        "slosh", "sloth", "slump", "slunk", "slurp", "smack", "small", "smear",
        "smell", "smelt", "smile", "smirk", "smite", "smoke", "snack", "snare",
        "sneak", "sneer", "snide", "sniff", "snore", "snort", "snout", "soggy",
        "solar", "solid", "solve", "sonic", "sorry", "south", "space", "spade",
        "spare", "spark", "spawn", "speak", "speck", "speed", "spend", "spice",
        "spill", "spine", "spire", "spite", "spoof", "spook", "spoon", "sport",
        "spout", "spree", "sprig", "spunk", "squad", "squat", "squid", "stack",
        "staff", "stain", "stair", "stake", "stale", "stall", "stamp", "stand",
        "stark", "start", "stash", "state", "stave", "stead", "steal", "steam",
        "steel", "steep", "steer", "stern", "stiff", "still", "sting", "stink",
        "stoic", "stomp", "stool", "store", "storm", "story", "stout", "stove",
        "straw", "stray", "strip", "strop", "strut", "stuck", "study", "stump",
        "stung", "stunk", "stunt", "style", "suave", "sugar", "suite", "sulky",
        "sumac", "sunny", "surge", "swamp", "swarm", "swear", "sweat", "sweep",
        "sweet", "swept", "swift", "swill", "swipe", "swirl", "swoop", "sword",
        "swore", "sworn", "syrup", "table", "taboo", "taffy", "taper", "tardy",
        "taste", "taunt", "tawny", "teach", "tense", "tepid", "terse", "thank",
        "their", "theme", "there", "these", "thick", "thing", "think", "third",
        "thorn", "those", "three", "threw", "throw", "thump", "tiara", "tiger",
        "tight", "timer", "tipsy", "tired", "title", "toadstool", "today", "token",
        "tonic", "topaz", "topple", "total", "totem", "touch", "tough", "towel",
        "tower", "toxic", "train", "trait", "tramp", "trash", "trawl", "tread",
        "treat", "trend", "triad", "trial", "tribe", "trick", "trill", "tripe",
        "trite", "troth", "trout", "trove", "truce", "truck", "truly", "trump",
        "trunk", "truss", "truth", "tulip", "tummy", "tuner", "tunic", "tutor",
        "twang", "tweak", "tweed", "tweet", "twerp", "twigg", "twill", "twirl",
        "twist", "tying", "udder", "ultra", "uncle", "under", "unfit", "unify",
        "union", "unite", "unruly", "upset", "urban", "usher", "usual", "usurp",
        "utter", "vague", "valid", "value", "valve", "vaunt", "vegan", "venom",
        "verge", "verse", "vicar", "vigor", "viral", "virgo", "visor", "vital",
        "vixen", "vocal", "vodka", "voila", "vouch", "vowel", "vying", "wacky",
        "wader", "wafer", "wager", "waken", "waste", "watch", "water", "weary",
        "weave", "wedge", "weedy", "weigh", "weird", "whack", "whale", "wheat",
        "wheel", "where", "which", "while", "whiff", "whine", "whirl", "whisk",
        "white", "whole", "whose", "wield", "wimpy", "windy", "witch", "witty",
        "woken", "world", "wormy", "worry", "worse", "worst", "worth", "would",
        "wound", "wrath", "wreak", "wreck", "wring", "wrist", "wrong", "wrote",
        "yacht", "yearn", "yodel", "young", "yours", "youth", "zappy", "zesty",
        "zilch", "zippy", "zombi", "zonal"
    ];


    [SlashCommand("wordle", "Guess the 5-letter word in 6 attempts!")]
    [EnabledInDm(false)]
    public async Task HandleWordleAsync()
    {
        await DeferAsync();

        var existing = _sp.Select(Constants.Constants.discordBotConnStr, "GetWordleByChannel",
            [new SqlParameter("@ChannelID", Context.Channel.Id.ToString())]);

        if (existing.Rows.Count > 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Wordle", "A Wordle is already running in this channel! Finish it first.", Username).Build());
            return;
        }

        string answer = Words[Random.Shared.Next(Words.Length)];

        var msg = await FollowupAsync(embed: BuildWordleEmbed(answer, [], false).Build());

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddWordleGame",
        [
            new SqlParameter("@ChannelID", Context.Channel.Id.ToString()),
            new SqlParameter("@MessageID", msg.Id.ToString()),
            new SqlParameter("@Answer",    answer),
            new SqlParameter("@Guesses",   ""),
            new SqlParameter("@StartedBy", Context.User.Id.ToString())
        ]);
    }


    public static EmbedBuilder BuildWordleEmbed(
        string answer, List<string> guesses, bool gameOver)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var guess in guesses)
            sb.AppendLine(RenderGuess(guess, answer));

        // Fill remaining rows with empty tiles
        for (int i = guesses.Count; i < MaxGuesses; i++)
            sb.AppendLine("⬛⬛⬛⬛⬛");

        bool won = guesses.Count > 0 && guesses[^1].Equals(answer, StringComparison.OrdinalIgnoreCase);

        Color colour = gameOver
            ? (won ? Color.Green : Color.Red)
            : Color.Blue;

        string title = gameOver
            ? (won ? $"🎉  Wordle — Solved in {guesses.Count}/6!" : $"💀  Wordle — The word was **{answer.ToUpperInvariant()}**")
            : $"🟩  Wordle  —  {guesses.Count}/{MaxGuesses}";

        string footer = gameOver
            ? (won ? "Well done!" : "Better luck next time!")
            : "Type a 5-letter word in chat to guess!";

        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(colour)
            .WithDescription(sb.ToString())
            .AddField("Key", "🟩 Correct  🟨 Wrong position  ⬛ Not in word", inline: false)
            .WithFooter(footer)
            .WithCurrentTimestamp();
    }

    /// <summary>
    /// Renders a single guess row with coloured emoji tiles.
    /// 🟩 = correct position, 🟨 = in word but wrong position, ⬛ = not in word.
    /// Handles duplicate letters correctly using a two-pass algorithm.
    /// </summary>
    public static string RenderGuess(string guess, string answer)
    {
        var result = new char[WordLength];
        var answerPool = answer.ToCharArray();

        // Pass 1 — mark greens and consume those answer letters
        for (int i = 0; i < WordLength; i++)
        {
            if (guess[i] == answer[i])
            {
                result[i] = 'G';
                answerPool[i] = '\0'; // consumed
            }
        }

        // Pass 2 — mark yellows from remaining pool
        for (int i = 0; i < WordLength; i++)
        {
            if (result[i] == 'G') continue;

            int poolIdx = Array.IndexOf(answerPool, guess[i]);
            if (poolIdx >= 0)
            {
                result[i] = 'Y';
                answerPool[poolIdx] = '\0';
            }
            else
            {
                result[i] = 'B';
            }
        }

        return string.Concat(result.Select(r => r switch
        {
            'G' => "🟩",
            'Y' => "🟨",
            _ => "⬛"
        })) + $"  `{guess.ToUpperInvariant()}`";
    }
}
