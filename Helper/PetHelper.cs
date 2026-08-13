namespace DiscordBot.Helper;

/// <summary>
/// Static helpers for the Tamagotchi pet system.
/// Handles XP/level maths, stat rendering, emoji lookup, and hibernation logic.
/// </summary>
public static class PetHelper
{

    public static readonly Dictionary<string, string[]> Breeds = new()
    {
        ["cat"] =
        [
            "Abyssinian", "Bengal", "Birman", "British Shorthair", "Burmese",
            "Devon Rex", "Egyptian Mau", "Himalayan", "Maine Coon", "Manx",
            "Norwegian Forest Cat", "Ocicat", "Persian", "Ragdoll", "Russian Blue",
            "Scottish Fold", "Siamese", "Siberian", "Sphynx", "Turkish Angora"
        ],
        ["dog"] =
        [
            "Akita", "Australian Shepherd", "Beagle", "Border Collie", "Boxer",
            "Bulldog", "Chihuahua", "Chow Chow", "Dachshund", "Dalmatian",
            "Doberman", "French Bulldog", "German Shepherd", "Golden Retriever",
            "Great Dane", "Husky", "Labrador Retriever", "Pomeranian", "Poodle",
            "Rottweiler", "Samoyed", "Shih Tzu", "Shiba Inu", "Weimaraner"
        ],
        ["horse"] =
        [
            "Andalusian", "Appaloosa", "Arabian", "Clydesdale", "Friesian",
            "Haflinger", "Lipizzaner", "Lusitano", "Mongolian", "Morgan",
            "Mustang", "Oldenburg", "Paint", "Paso Fino", "Percheron",
            "Quarter Horse", "Shetland Pony", "Standardbred", "Tennessee Walker", "Thoroughbred"
        ],
        ["bird"] =
        [
            "African Grey Parrot", "Amazon Parrot", "Blue Jay", "Budgerigar",
            "Canary", "Cockatiel", "Cockatoo", "Conure", "Eclectus Parrot",
            "Finch", "Galah", "Indian Ringneck", "Lorikeet", "Lovebird",
            "Macaw", "Mynah", "Quaker Parrot", "Robin", "Sun Conure", "Toucan"
        ],
        ["dinosaur"] =
        [
            "Ankylosaurus", "Brachiosaurus", "Carnotaurus", "Compsognathus",
            "Dilophosaurus", "Diplodocus", "Iguanodon", "Pachycephalosaurus",
            "Parasaurolophus", "Pterodactyl", "Spinosaurus", "Stegosaurus",
            "Styracosaurus", "Triceratops", "Troodon", "T-Rex",
            "Utahraptor", "Velociraptor"
        ],
        ["bunny"] =
        [
            "American", "Angora", "Belgian Hare", "Californian", "Checkered Giant",
            "Chinchilla", "Dutch", "Dwarf Hotot", "English Lop", "Flemish Giant",
            "French Lop", "Harlequin", "Holland Lop", "Jersey Wooly", "Lionhead",
            "Mini Rex", "Netherland Dwarf", "New Zealand", "Polish", "Rex"
        ],
        ["fish"] =
        [
            "Betta", "Clownfish", "Angelfish", "Goldfish", "Guppy",
            "Koi", "Discus", "Oscar", "Neon Tetra", "Axolotl",
            "Pufferfish", "Lionfish", "Moorish Idol", "Mandarin Fish", "Arowana",
            "Rainbow Fish", "Cichlid", "Zebrafish", "Pleco", "Parrotfish"
        ],
        ["shark"] =
        [
            "Great White", "Hammerhead", "Bull Shark", "Tiger Shark", "Whale Shark",
            "Nurse Shark", "Blacktip Reef", "Whitetip Reef", "Lemon Shark", "Blue Shark",
            "Mako Shark", "Goblin Shark", "Thresher Shark", "Zebra Shark", "Wobbegong",
            "Epaulette Shark", "Angel Shark", "Horn Shark", "Port Jackson", "Bamboo Shark"
        ],
        ["wolf"] =
        [
            "Arctic Wolf", "Black Wolf", "Eastern Timber", "Ethiopian Wolf", "Eurasian Wolf",
            "Gray Wolf", "Great Plains Wolf", "Himalayan Wolf", "Iberian Wolf", "Indian Wolf",
            "Iranian Wolf", "Italian Wolf", "Mackenzie Valley", "Mexican Wolf", "Northwestern Wolf",
            "Red Wolf", "Steppe Wolf", "Tundra Wolf", "Arabian Wolf", "Chinese Wolf"
        ],
        ["lizard"] =
        [
            "Ackie Monitor", "Bearded Dragon", "Blue-Tongued Skink", "Chameleon", "Crested Gecko",
            "Day Gecko", "Frilled Dragon", "Giant Tegu", "Green Iguana", "Jackson's Chameleon",
            "Komodo Dragon", "Leopard Gecko", "Monitor Lizard", "Panther Chameleon", "Savannah Monitor",
            "Spiny-Tailed Iguana", "Uromastyx", "Veiled Chameleon", "Water Dragon", "Tokay Gecko"
        ],
        ["otter"] =
        [
            "African Clawless Otter", "Asian Small-Clawed Otter", "Cape Clawless Otter", "Congo Clawless Otter",
            "Eurasian Otter", "Giant Otter", "Hairy-Nosed Otter", "Indian Smooth-Coated Otter",
            "Japanese River Otter", "Marine Otter", "Neotropical Otter", "North American River Otter",
            "River Otter", "Sea Otter", "Smooth-Coated Otter", "Southern River Otter",
            "Spotted-Necked Otter", "Brazilian Giant Otter", "Luzon Otter", "Sumatran Otter"
        ],
        ["bear"] =
        [
            "American Black Bear", "Asiatic Black Bear", "Atlas Bear", "Brown Bear", "Cave Bear",
            "Eurasian Brown Bear", "Florida Black Bear", "Giant Panda", "Grizzly Bear",
            "Himalayan Brown Bear", "Kermode Bear", "Kodiak Bear", "Polar Bear", "Sloth Bear",
            "Spectacled Bear", "Spirit Bear", "Sun Bear", "Syrian Brown Bear",
            "Ussuri Brown Bear", "Malayan Sun Bear"
        ],
        ["insect"] =
        [
            "Atlas Moth", "Blue Morpho Butterfly", "Bumblebee", "Dragonfly", "Emperor Dragonfly",
            "Firefly", "Giant Swallowtail", "Glasswing Butterfly", "Goliath Beetle", "Hercules Beetle",
            "Hummingbird Hawk-Moth", "Jewel Beetle", "Leafcutter Ant", "Luna Moth", "Monarch Butterfly",
            "Orchid Mantis", "Praying Mantis", "Rainbow Scarab", "Stick Insect", "Walking Stick"
        ],
        ["ocean_invertebrate"] =
        [
            "Blue-Ringed Octopus", "Box Jellyfish", "Christmas Tree Worm", "Coconut Crab",
            "Cuttlefish", "Decorator Crab", "Dumbo Octopus", "Fiddler Crab", "Giant Clam",
            "Giant Pacific Octopus", "Giant Squid", "Horseshoe Crab", "Japanese Spider Crab",
            "Mantis Shrimp", "Mimic Octopus", "Moon Jellyfish", "Nautilus", "Peacock Mantis Shrimp",
            "Portuguese Man O' War", "Sea Slug", "Starfish", "Vampire Squid"
        ],
        ["land_invertebrate"] =
        [
            "Atlas Beetle", "Black Widow Spider", "Centipede", "Death Stalker Scorpion",
            "Emperor Scorpion", "Garden Snail", "Giant African Millipede", "Giant Land Snail",
            "Giant Vinegaroon", "Goliath Bird-Eating Spider", "Land Hermit Crab", "Malaysian Jewel Tarantula",
            "Peacock Tarantula", "Pill Bug", "Pink-Toed Tarantula", "Purple Pincher Hermit Crab",
            "Red-Knee Tarantula", "Rose Hair Tarantula", "Rusty-Patched Bumblebee", "Tailless Whip Scorpion"
        ]
    };

    /// <summary>True if <paramref name="breed"/> is a recognized breed for the given species.</summary>
    public static bool IsValidBreed(string species, string breed) =>
        Breeds.TryGetValue(species.ToLower(), out var breeds) &&
        breeds.Contains(breed, StringComparer.OrdinalIgnoreCase);


    /// <summary>XP required to reach a given level. Curve: 50 * level^2</summary>
    public static int XpForLevel(int level) => 50 * level * level;

    /// <summary>Derives the current level from a raw XP value.</summary>
    public static int LevelFromXp(int xp)
    {
        int level = 1;
        while (XpForLevel(level + 1) <= xp)
            level++;
        return level;
    }

    /// <summary>Returns XP progress within the current level as a 0–1 float.</summary>
    public static float LevelProgress(int xp)
    {
        int level = LevelFromXp(xp);
        int current = XpForLevel(level);
        int next = XpForLevel(level + 1);
        return (float)(xp - current) / (next - current);
    }


    public const int HibernationThreshold = 15;

    /// <summary>
    /// Triggers hibernation when 2 or more stats fall below the threshold.
    /// Requiring all 3 was almost never hit in practice.
    /// </summary>
    public static bool ShouldHibernate(int hunger, int happiness, int energy)
    {
        int below = (hunger < HibernationThreshold ? 1 : 0)
                  + (happiness < HibernationThreshold ? 1 : 0)
                  + (energy < HibernationThreshold ? 1 : 0);
        return below >= 2;
    }


    /// <summary>
    /// Picks the display emoji for a pet based on its current state: hibernating and hungry
    /// take priority over mood, and evolved (level 100) pets get a distinct emoji when happy.
    /// </summary>
    public static string PetEmoji(string species, int happiness, int hunger,
                                   bool hibernating, bool evolved)
    {
        if (hibernating) return species.ToLower() switch
        {
            "cat" => "😴🐱",
            "dog" => "😴🐶",
            "horse" => "😴🐴",
            "bird" => "😴🐦",
            "dinosaur" => "😴🦕",
            "bunny" => "😴🐰",
            "fish" => "😴🐟",
            "shark" => "😴🦈",
            "wolf" => "😴🐺",
            "lizard" => "😴🦎",
            "otter" => "😴🦦",
            "bear" => "😴🐻",
            "insect" => "😴🐛",
            "ocean_invertebrate" => "😴🐙",
            "land_invertebrate" => "😴🦂",
            _ => "😴"
        };

        if (hunger < 20) return species.ToLower() switch
        {
            "cat" => "😾",
            "dog" => "🐕",
            "horse" => "🐎",
            "bird" => "🐧",
            "dinosaur" => "🦖",
            "bunny" => "🐇",
            "fish" => "🐡",
            "shark" => "🦷",
            "wolf" => "🐺",
            "lizard" => "🦎",
            "otter" => "🦦",
            "bear" => "🐻",
            "insect" => "🐜",
            "ocean_invertebrate" => "🦑",
            "land_invertebrate" => "🦂",
            _ => "😟"
        };

        if (happiness >= 75) return evolved ? EvolvedEmoji(species) : HappyEmoji(species);

        return NormalEmoji(species, evolved);
    }

    /// <summary>Emoji for a species at high happiness, not evolved.</summary>
    private static string HappyEmoji(string species) => species.ToLower() switch
    {
        "cat" => "😺",
        "dog" => "🐶",
        "horse" => "🐎",
        "bird" => "🦜",
        "dinosaur" => "🦕",
        "bunny" => "🐰",
        "fish" => "🐠",
        "shark" => "🦈",
        "wolf" => "🐺",
        "lizard" => "🦎",
        "otter" => "🦦",
        "bear" => "🐻",
        "insect" => "🦋",
        "ocean_invertebrate" => "🐙",
        "land_invertebrate" => "🕷️",
        _ => "🐾"
    };

    /// <summary>Emoji for a species at ordinary mood/hunger — the default look, evolved or not.</summary>
    private static string NormalEmoji(string species, bool evolved) => species.ToLower() switch
    {
        "cat" => evolved ? "🦁" : "🐱",
        "dog" => evolved ? "🐺" : "🐶",
        "horse" => evolved ? "🦄" : "🐴",
        "bird" => evolved ? "🦅" : "🐦",
        "dinosaur" => evolved ? "🐉" : "🦕",
        "bunny" => evolved ? "🐇" : "🐰",
        "fish" => evolved ? "🐋" : "🐟",
        "shark" => evolved ? "🌊" : "🦈",
        "wolf" => evolved ? "🌕" : "🐺",
        "lizard" => evolved ? "🐲" : "🦎",
        "otter" => evolved ? "🌊" : "🦦",
        "bear" => evolved ? "🏔️" : "🐻",
        "insect" => evolved ? "🐝" : "🐛",
        "ocean_invertebrate" => evolved ? "🦑" : "🐙",
        "land_invertebrate" => evolved ? "🦂" : "🕷️",
        _ => "🐾"
    };

    /// <summary>Emoji for a species at high happiness and evolved (level 100) — the "final form" look.</summary>
    private static string EvolvedEmoji(string species) => species.ToLower() switch
    {
        "cat" => "🦁",
        "dog" => "🐺",
        "horse" => "🦄",
        "bird" => "🦅",
        "dinosaur" => "🐉",
        "bunny" => "🐇",
        "fish" => "🐋",
        "shark" => "🌊",
        "wolf" => "🌕",
        "lizard" => "🐲",
        "otter" => "🌊",
        "bear" => "🏔️",
        "insect" => "🐝",
        "ocean_invertebrate" => "🦑",
        "land_invertebrate" => "🦂",
        _ => "🌟"
    };


    /// <summary>Returns the flavour display name a species takes on once evolved (level 100), e.g. "cat" → "Maine Coon".</summary>
    public static string EvolvedName(string species) => species.ToLower() switch
    {
        "cat" => "Maine Coon",
        "dog" => "Golden Retriever",
        "horse" => "Unicorn",
        "bird" => "Eagle",
        "dinosaur" => "Dragon",
        "bunny" => "Shadow Rabbit",
        "fish" => "Leviathan",
        "shark" => "Megalodon",
        "wolf" => "Dire Wolf",
        "lizard" => "Komodo Dragon",
        "otter" => "Sea Emperor",
        "bear" => "Spirit Bear",
        "insect" => "Metamorph",
        "ocean_invertebrate" => "Kraken",
        "land_invertebrate" => "Emperor Scorpion",
        _ => species
    };


    /// <summary>Renders a 10-block filled/empty bar for a 0-100 stat value.</summary>
    public static string StatBar(int value)
    {
        int filled = Math.Clamp(value, 0, 100) / 10;
        return string.Create(10, filled, static (span, f) =>
        {
            span.Fill('░');
            span[..f].Fill('█');
        });
    }

    /// <summary>Renders a stat bar with a colour-coded emoji indicator (green/yellow/orange/red by value range).</summary>
    public static string StatDisplay(string label, int value)
    {
        string bar = StatBar(value);
        string colour = value switch
        {
            >= 70 => "🟢",
            >= 40 => "🟡",
            >= 20 => "🟠",
            _ => "🔴"
        };
        return $"{colour} {bar} **{value}/100**";
    }


    public const int XpMessage = 1;
    public const int XpAttachment = 3;
    public const int XpLink = 2;
    public const int XpActivity = 5;
    public const int XpWordPuzzle = 15;
    public const int XpPet = 5;
    public const int XpFeed = 3;
    public const int XpGroom = 3;
    public const int XpPlay = 8;


    public const int FeedCooldownMinutes = 30;
    public const int PetCooldownMinutes = 5;
    public const int GroomCooldownMinutes = 60;
    public const int PlayCooldownMinutes = 15;


    /// <summary>Returns the unlock announcement text for a level milestone, or null if this level doesn't unlock anything.</summary>
    public static string? LevelUpUnlock(int level) => level switch
    {
        5 => "🎪 **Unlocked:** `/trick` slot 1 — your pet can now show off!",
        10 => "🎩 **Unlocked:** Accessory Slot 1 — equip a hat with `/accessory`",
        15 => "👗 **Unlocked:** Accessory Slot 2 — equip a collar or outfit",
        20 => "✨ **Unlocked:** Veteran border + `/trick` slot 2!",
        25 => "🍖 **Unlocked:** Rare food items in `/feed`",
        50 => "🌟 **Evolved!** Your pet has reached its final form! + `/trick` slot 3 unlocked!",
        75 => "🎭 **Unlocked:** `/trick` slot 4 — your legendary pet's ultimate move!",
        100 => "👑 **Hall of Fame!** Your pet is now legendary!",
        _ => null
    };


    /// <summary>Returns the flavour-text description for a species performing the trick unlocked at the given slot (1-4, gated by level).</summary>
    public static string PerformTrick(string species, int slot) =>
        (species.ToLower(), slot) switch
        {
            // Slot 1 — level 5
            ("cat", 1) => "*rolls over with enormous confidence, ignores you for ten seconds, then chirps once as if to say 'I did it, you're welcome'* 😺",
            ("dog", 1) => "*plants themselves directly in front of you, deploys full puppy eyes, adds a tiny whine, and oscillates between sit and almost-sit with maximum emotional manipulation* 🐶",
            ("horse", 1) => "*rears to full height with theatrical precision, hooves pawing the air, mane flowing — holds it for three full seconds before landing with a perfectly dignified thud* 🐎",
            ("bird", 1) => "*tilts their head, listens for a moment, then whistles back your favourite tune completely correctly — and then adds their own personal flourish at the end* 🎵",
            ("dinosaur", 1) => "*thunders in a wide circle, halts with surprising precision, sits — which takes a moment — and stares at you with obvious expectation* 🦕",
            ("bunny", 1) => "*launches into a binky of such magnificence that they briefly become airborne, land, binky again immediately, and then freeze as if nothing happened* 🐰",
            ("fish", 1) => "*rises to the surface, blows one perfectly spherical bubble, watches it drift to the top, then looks at you with both eyes simultaneously as if awaiting a score* 🐟",
            ("shark", 1) => "*circles with increasing speed and menace, builds tension for a full thirty seconds, then comes in close and bumps your hand with their nose like a golden retriever* 🦈",
            ("wolf", 1) => "*sits with the posture of someone who invented sitting, locks those amber eyes directly onto yours, and communicates a full emotional thesis without blinking once* 🐺",
            ("lizard", 1) => "*drops to the ground, executes three precise push-ups with a pause between each one, and then looks up with an expression that can only be described as self-satisfied* 🦎",
            ("otter", 1) => "*rolls onto their back with breathtaking ease, balances a pebble on their chest, and gazes at it with the reverence usually reserved for sacred objects* 🦦",
            ("bear", 1) => "*rises to full hind-leg height with the unhurried confidence of someone who invented standing up, extends one enormous paw, and waves it twice — royally* 🐻",
            ("insect", 1) => "*goes very still, vibrates once, and then emits a soft steady glow for five full seconds before dimming back down as though nothing unusual occurred* 🦋",
            ("ocean_invertebrate", 1) => "*extends all eight arms simultaneously in a perfectly symmetrical radial display, holds it for a count of three, then curls them all back with poise* 🐙",
            ("land_invertebrate", 1) => "*slowly raises both front legs to full extension, holds the stance with unreadable stillness, and waits for your reaction with a patience you cannot match* 🕷️",
            // Slot 2 — level 20
            ("cat", 2) => "*selects the item on the highest available surface with great deliberation, maintains absolute direct eye contact, and nudges it off the edge with one precise paw. Sits. Blinks.* 😸",
            ("dog", 2) => "*spins in accelerating circles, barks twice at peak velocity, and then crashes onto the floor with total commitment — tail still wagging from the ground* 🐶",
            ("horse", 2) => "*executes a flawless lateral dressage step that would impress at any grand prix, pivots sharply, and folds one leg under for a bow that lands right on cue* 🐎",
            ("bird", 2) => "*listens carefully, processes for two full seconds, then delivers your exact voice saying something you said once months ago that you'd entirely forgotten about* 🦜",
            ("dinosaur", 2) => "*rears to full height, draws a massive breath, and produces a sound not unlike a very large pigeon. Stares at you. Tries again. Same result. Stares harder.* 🐉",
            ("bunny", 2) => "*stands on hind legs, reaches maximum extension, and gazes at you with eyes of such profound and calculated sweetness that refusing them would be physically impossible* 🐇",
            ("fish", 2) => "*coils into a tight corkscrew, unwinds at speed, and comes out of the spin facing you dead-on at eye level — sticks the landing perfectly and holds it* 🐟",
            ("shark", 2) => "*rises from the depths with theatrical slowness, breaches the surface, jaw opening with ceremony — and closes gently around the treat with astonishing precision* 🦈",
            ("wolf", 2) => "*draws breath slowly, tilts their head to the exact right angle, and releases a single held note that fills the entire space and somehow stays exactly on pitch throughout* 🐺",
            ("lizard", 2) => "*flickers through amber, then teal, then burgundy, then pale gold in rapid sequence, bows their head once at the end like an artist acknowledging a performance* 🦎",
            ("otter", 2) => "*rolls onto their back in open water, selects three pebbles, and tosses them one at a time — catches each on the way down without looking. Holds the last one up.* 🦦",
            ("bear", 2) => "*lumbers a full deliberate circuit, settles down with the force of a small avalanche, and looks at you with an expression that says the next move is yours* 🐻",
            ("insect", 2) => "*takes flight in tight formation with themselves, executes four precise arcs, and traces shapes in the air that take a moment to resolve into something recognisable* 🦋",
            ("ocean_invertebrate", 2) => "*locates the narrowest gap available, pours their entire body through it with no apparent effort, emerges on the other side, and bows with genuine flourish* 🐙",
            ("land_invertebrate", 2) => "*anchors eight silk threads in a radial pattern, weaves them into a small hammock in under three minutes, settles into it briefly, then dismantles it cleanly* 🕷️",
            // Slot 3 — level 50
            ("cat", 3) => "*initiates a slow-blink sequence of such calculated precision that you feel genuinely honoured — then walks toward you, bumps your hand with their head, and actually stays* 😺",
            ("dog", 3) => "*rushes out of the room, returns at top speed carrying something you absolutely did not throw, drops it at your feet, and vibrates with tail-wagging pride* 🐶",
            ("horse", 3) => "*launches into a full figure-eight gallop at collection, transitions through every gait in sequence, and stops on the precise mark — not a single extra step* 🐎",
            ("bird", 3) => "*takes a breath, assumes a posture, and delivers a full thirty-second monologue in your voice including the pauses, the intonation, and one joke you told last week* 🦜",
            ("dinosaur", 3) => "*executes the full prehistoric victory ceremony: ground-stomp, tail-sweep, sky-bellow, and an unexpectedly dignified bow. The ground genuinely shook.* 🦖",
            ("bunny", 3) => "*launches into a full binky-and-zoom sequence that covers every available surface in under forty seconds, then collapses into a perfect loaf as if nothing happened* 🐰",
            ("fish", 3) => "*breaches the surface in a clean arc, passes through the suspended hoop without touching the rim, and re-enters the water in a single quiet splash* 🐟",
            ("shark", 3) => "*builds speed from the bottom of the tank, hits the surface at full force, clears the water by a full body-length, hangs there for two full seconds, and lands clean* 🦈",
            ("wolf", 3) => "*throws back their head, produces a long opening note, and somehow the acoustics shift — three other animal voices in the distance join in whether they intended to or not* 🐺",
            ("lizard", 3) => "*detaches their tail with quiet ceremony, lets it perform a distracting wriggle routine, and then simply grows it back in front of you — start to finish* 🦎",
            ("otter", 3) => "*lays back in the water, selects seven pebbles from a pile, and assembles them on their chest into a freestanding tower without using their eyes once* 🦦",
            ("bear", 3) => "*rises to hind legs, and with a composure that defies every expectation, performs a full slow waltz — four measured steps, one turn, repeat — for sixty seconds exactly* 🐻",
            ("insect", 3) => "*goes very still, shimmers at the wing edges, and then emerges from the shimmer noticeably, undeniably shinier — the metamorphosis was brief but clearly real* 🦋",
            ("ocean_invertebrate", 3) => "*shifts colour, then pattern, then texture in sequence, cycling through three distinct configurations before settling on a perfect replica of the surface behind them* 🐙",
            ("land_invertebrate", 3) => "*retreats into stillness, splits their exoskeleton cleanly down the back, steps forward out of it in gleaming new armour, and holds a five-second pose* 🕷️",
            // Slot 4 — level 75
            ("cat", 4) => "*fixes their gaze on a specific point in the middle distance, holds it with complete stillness for ten minutes, then blinks once, stretches, and resumes normal life as though you didn't see anything* 😺",
            ("dog", 4) => "*trots purposefully to the refrigerator, paws the handle down, retrieves something appropriate, brings it to you in a completely unasked-for act of service, and closes the fridge with their snout* 🐶",
            ("horse", 4) => "*executes a capriole — full controlled rear, hind legs kicking backward at peak height — and lands with measured precision on exactly the same mark they started from* 🐎",
            ("bird", 4) => "*clears their throat, takes a breath, and performs a five-minute opera entirely about their own daily life, in perfect pitch, with recognisable callbacks to earlier verses* 🦜",
            ("dinosaur", 4) => "*draws breath for a full ten seconds, then releases a roar that registers on seismographs, shatters three nearby windows, and briefly silences every other animal for miles. Bows.* 🌋",
            ("bunny", 4) => "*begins vibrating at a frequency of such joy that their edges blur slightly, goes briefly translucent for two full seconds, then rematerialises perfectly intact and binky-sprints away* 🐰",
            ("fish", 4) => "*swims directly at the glass, passes through it without disturbing a single molecule, completes one full circuit of the room at head height, returns, and re-enters without a ripple* 🐋",
            ("shark", 4) => "*descends to the deepest point, accelerates upward at impossible speed, clears the water entirely, circles overhead for one full rotation, and descends back in without disturbing the surface* 🌊",
            ("wolf", 4) => "*tilts their head back and releases a single sustained note that climbs and climbs — and from somewhere distant, something answers. They nod once. You don't ask further.* 🌕",
            ("lizard", 4) => "*fades, patches first, then entirely, and remains invisible for a precise ten minutes — you only know they're there by the faint warmth on the carpet — then reappears wearing a tiny, unexplained hat* 🦎",
            ("otter", 4) => "*retrieves a locked wooden box, examines it for thirty seconds, selects three specific pebbles, applies them in sequence, and opens it. Removes the snack. Relocks the box. Eats the snack.* 🦦",
            ("bear", 4) => "*sits heavily, takes one long breath, closes both eyes, and enters a state of absolute stillness for exactly thirty seconds — then opens one eye, stands up, and proceeds as though nothing at all happened* 🐻",
            ("insect", 4) => "*wraps themselves in a shimmer that lasts four full seconds, expands briefly into something magnificent, wing-span at maximum, every colour simultaneously — then collapses back, winks, and buzzes off* 🐝",
            ("ocean_invertebrate", 4) => "*locates the sealed jar, examines it once from the outside, slips inside through an imperceptible gap, opens it from the interior, removes the snack, replaces the lid, and exits. Looks smug.* 🦑",
            ("land_invertebrate", 4) => "*spends forty-five minutes constructing a perfect scaled web reproduction of an iconic structure, holds perfectly still while you take it in, then eats the entire thing in thirty seconds* 🦂",
            _ => "*does something impressively cute*"
        };


    public static readonly (string name, string emoji, int hungerRestore, int happyBonus, int minLevel)[] Foods =
    [
        ("Kibble",          "🥣", 20,  5,  1),
        ("Fresh Meat",      "🥩", 35, 10,  1),
        ("Vegetables",      "🥦", 15,  3,  1),
        ("Fish",            "🐟", 30, 15,  1),
        ("Bread",           "🍞", 12,  2,  1),
        ("Apple",           "🍎", 14,  6,  1),
        ("Carrot",          "🥕", 16,  7,  1),
        ("Egg",             "🥚", 18,  5,  1),
        ("Cheese",          "🧀", 20,  8,  1),
        ("Milk",            "🥛", 10,  6,  1),
        ("Berries",         "🫐", 12, 10,  1),
        ("Banana",          "🍌", 14,  8,  1),
        ("Corn",            "🌽", 15,  4,  1),
        ("Pumpkin",         "🎃", 18,  5,  1),
        ("Chicken",         "🍗", 32, 10,  1),
        ("Rice Bowl",       "🍚", 22,  4,  1),
        ("Bone Broth",      "🫙", 25,  8,  1),
        ("Hay",             "🌾", 20,  3,  1),   // horses/dinosaurs love this
        ("Seeds",           "🌱", 10,  5,  1),   // birds love this
        ("Pellets",         "⚪", 18,  4,  1),

        ("Salmon Fillet",   "🍣", 38, 18, 10),
        ("Grilled Steak",   "🥓", 45, 15, 10),
        ("Fruit Salad",     "🍓", 28, 20, 10),
        ("Honey",           "🍯", 20, 22, 10),
        ("Smoothie",        "🥤", 25, 18, 10),
        ("Sweet Potato",    "🍠", 30, 12, 10),
        ("Pasta",           "🍝", 35, 10, 10),
        ("Sandwich",        "🥪", 32, 12, 10),
        ("Soup",            "🍜", 30, 14, 10),
        ("Pancakes",        "🥞", 28, 16, 10),

        ("Birthday Cake",   "🎂", 25, 30, 25),
        ("Gourmet Meal",    "🍱", 50, 25, 25),
        ("Sushi Platter",   "🍱", 45, 28, 25),
        ("Lobster",         "🦞", 50, 30, 25),
        ("Truffle",         "🍄", 35, 35, 25),
        ("Ice Cream",       "🍦", 20, 35, 25),
        ("Chocolate",       "🍫", 22, 32, 25),
        ("Croissant",       "🥐", 30, 25, 25),
        ("Ramen",           "🍜", 40, 22, 25),
        ("Taco",            "🌮", 38, 24, 25),

        ("Magic Treat",     "✨", 40, 40, 50),
        ("Dragon Fruit",    "🐉", 50, 45, 50),
        ("Golden Apple",    "🌟", 55, 50, 50),
        ("Cosmic Candy",    "🍬", 35, 55, 50),
        ("Elixir",          "🧪", 60, 40, 50),
        ("Stardust Cake",   "🎂", 45, 60, 50),
        ("Phoenix Feather Tea","🪶",50, 55, 50),
    ];

    /// <summary>Lists every food item unlocked at or below the pet's current level, one per line.</summary>
    public static string ListFoods(int petLevel) => string.Join("\n", Foods.Where(f => f.minLevel <= petLevel).Select(f => $"{f.emoji} **{f.name}** — +{f.hungerRestore} hunger, +{f.happyBonus} happiness"));


    public static readonly (string key, string emoji, string description, int xp, int happyBonus, int hungerCost, int energyCost, int minLevel)[] ExploreRewards =
    [
        ("common_bone",    "🦴", "Found an old bone!",                          10, 5,  10, 15, 1),
        ("common_flower",  "🌸", "Brought back a pretty flower!",               10, 10, 5,  10, 1),
        ("common_stick",   "🪵", "Dragged home a massive stick",                10, 8,  8,  12, 1),
        ("common_rock",    "🪨", "Proudly presented a shiny rock",              10, 5,  5,  10, 1),
        ("uncommon_coin",  "🪙", "Discovered a shiny coin in the grass!",       20, 10, 10, 20, 1),
        ("uncommon_berry", "🫐", "Snacked on wild berries along the way!",      20, 15, 0,  15, 1),
        ("uncommon_feather","🪶","Found a rare feather from an unknown bird!",  20, 12, 8,  18, 1),
        ("rare_gem",       "💎", "Unearthed a sparkling gemstone!",             40, 20, 15, 25, 10),
        ("rare_map",       "🗺️", "Found a piece of an ancient treasure map!",  40, 25, 12, 25, 10),
        ("rare_crown",     "👑", "Somehow came home wearing a tiny crown",      40, 30, 15, 30, 10),
        ("epic_treasure",  "💰", "Found an entire treasure chest!",             75, 35, 20, 35, 25),
        ("epic_artifact",  "🏺", "Discovered a mysterious ancient artifact!",   75, 40, 18, 35, 25),
        ("legendary_star", "⭐", "Caught a falling star and brought it back!",  120, 50, 25, 40, 50),
    ];

    /// <summary>
    /// Picks a random reward weighted by rarity and gated by pet level.
    /// Higher level pets have a better chance at rare/epic/legendary drops.
    /// </summary>
    public static (string key, string emoji, string description, int xp, int happyBonus, int hungerCost, int energyCost, int minLevel) PickExploreReward(int level)
    {
        var available = ExploreRewards.Where(r => r.minLevel <= level).ToArray();

        // Weighted roll: common = 50%, uncommon = 30%, rare = 15%, epic = 4%, legendary = 1%
        // Approximated by weighting each tier's entries
        int roll = Random.Shared.Next(100);

        IEnumerable<(string key, string emoji, string description,
                     int xp, int happyBonus, int hungerCost, int energyCost,
                     int minLevel)> pool;

        if (roll < 50)
            pool = available.Where(r => r.key.StartsWith("common"));
        else if (roll < 80)
            pool = available.Where(r => r.key.StartsWith("uncommon"));
        else if (roll < 95)
            pool = available.Where(r => r.key.StartsWith("rare"));
        else if (roll < 99)
            pool = available.Where(r => r.key.StartsWith("epic"));
        else
            pool = available.Where(r => r.key.StartsWith("legendary"));

        var filtered = pool.ToArray();

        // Fallback to common if tier is locked
        if (filtered.Length == 0)
            filtered = available.Where(r => r.key.StartsWith("common")).ToArray();

        return filtered[Random.Shared.Next(filtered.Length)];
    }

    /// <summary>
    /// Boosted version of <see cref="PickExploreReward"/> — guarantees Rare+ tier.
    /// Used when the explore_boost shop item is active.
    /// Weights: Rare = 60%, Epic = 30%, Legendary = 10%.
    /// </summary>
    public static (string key, string emoji, string description, int xp, int happyBonus, int hungerCost, int energyCost, int minLevel) PickExploreRewardBoosted(int level)
    {
        var available = ExploreRewards.Where(r => r.minLevel <= level).ToArray();
        int roll = Random.Shared.Next(100);

        IEnumerable<(string key, string emoji, string description,
                     int xp, int happyBonus, int hungerCost, int energyCost,
                     int minLevel)> pool;

        if (roll < 60)
            pool = available.Where(r => r.key.StartsWith("rare"));
        else if (roll < 90)
            pool = available.Where(r => r.key.StartsWith("epic"));
        else
            pool = available.Where(r => r.key.StartsWith("legendary"));

        var filtered = pool.ToArray();

        // Fallback chain if tier is completely locked for this level
        if (filtered.Length == 0)
            filtered = available.Where(r => r.key.StartsWith("rare")).ToArray();
        if (filtered.Length == 0)
            filtered = available.Where(r => r.key.StartsWith("uncommon")).ToArray();
        if (filtered.Length == 0)
            filtered = available.Where(r => r.key.StartsWith("common")).ToArray();

        return filtered[Random.Shared.Next(filtered.Length)];
    }


    /// <summary>Returns a species-flavoured departure line shown when a pet sets off on /explore.</summary>
    public static string ExploreDeparture(string species) => species.ToLower() switch
    {
        "cat" => "🐱 *fixes you with one long, evaluating look, then slips through a gap in the door that was definitely not big enough for them, and is simply gone*",
        "dog" => "🐶 *bolts out the front door at maximum velocity, tail achieving helicopter rotation, leaping the fence without breaking stride, and disappearing over the hill mid-bark*",
        "horse" => "🐴 *breaks into a canter before they're fully out the gate, transitions to full gallop within three strides, and vanishes over the ridge without so much as a backward glance*",
        "bird" => "🐦 *launches off the windowsill, catches an updraft with practiced ease, spirals upward until they're a dark speck against the sky, and then tilts toward the horizon*",
        "dinosaur" => "🦕 *lumbers toward the horizon with absolute purpose, each footstep a minor geological event, until they vanish beyond the treeline*",
        "bunny" => "🐰 *bolts from a standing start to full sprint in a single blink, ears pinned flat, covering the garden in approximately four bounds before disappearing into the hedge*",
        "fish" => "🐟 *slips through a gap you're nearly certain wasn't there, navigates three impossible surfaces, and is in open water before you've finished looking for them*",
        "shark" => "🦈 *cuts through the shallows with a single unhurried fin-sweep, accelerates without apparent effort, and is gone beneath the surface before you can blink*",
        "wolf" => "🐺 *steps into the shadows at the treeline, and between one heartbeat and the next is simply absent — only two amber points of light lingering for a moment before those, too, disappear*",
        "lizard" => "🦎 *skitters up the doorframe in three rapid movements, across the ceiling with the casual ease of someone who finds gravity optional, and out the window without once touching the floor*",
        "otter" => "🦦 *executes a running belly-slide down the bank with enormous commitment, hits the water at perfect angle, and submerges in a single clean splash that dissipates almost immediately*",
        "bear" => "🐻 *lumbers into motion with deceptive speed, snout already working the air ahead of them, growing smaller with each unhurried-looking stride until the trees close around them entirely*",
        "insect" => "🐛 *makes slow, purposeful progress toward the door for two full minutes, then abruptly takes flight and is through the gap before you register they've left the ground*",
        "ocean_invertebrate" => "🐙 *flows under the door like water finding the path of least resistance, every arm accounted for, nothing left behind — gone before you've fully processed what you just watched*",
        "land_invertebrate" => "🕷️ *anchors a single silk thread, rappels down the exterior wall with controlled precision, and vanishes into the undergrowth as though they were never there at all*",
        _ => "*heads off on an adventure*"
    };

    /// <summary>Returns a random species-flavoured narrative line describing what the pet got up to while exploring.</summary>
    public static string ExploreNarrative(string species, string rewardKey) =>
        species.ToLower() switch
        {
            "cat" => Random.Shared.Next(10) switch
            {
                0 => "🐱 Wandered into three different gardens, judged each one, and came back.",
                1 => "🐱 Spent most of the time sitting on a stranger's porch being adored.",
                2 => "🐱 Explored everywhere, accepted belly rubs from nobody.",
                3 => "🐱 Strolled through the neighbourhood with the energy of someone who owns all of it.",
                4 => "🐱 Knocked something off a very high shelf, observed the aftermath, and left.",
                5 => "🐱 Spent an hour watching a bird through a window, then pretended not to care.",
                6 => "🐱 Found an extremely comfortable sunny spot, napped in it for most of the adventure, and considers this a success.",
                7 => "🐱 Infiltrated four separate households, received treats at three of them, and left the fourth a strongly-worded impression.",
                8 => "🐱 Sat in a cardboard box someone had left out, refused to leave for forty minutes, eventually departed without explanation.",
                _ => "🐱 Got caught in the rain, found shelter anyway, and acted like it was the plan all along."
            },
            "dog" => Random.Shared.Next(10) switch
            {
                0 => "🐶 Ran through the park, made six new best friends, and investigated every bin.",
                1 => "🐶 Followed an interesting smell for two miles and ended up at a bakery.",
                2 => "🐶 Sprinted the entire way there and the entire way back. Maximum effort.",
                3 => "🐶 Discovered a puddle of suspicious size and dove in without hesitation.",
                4 => "🐶 Tracked a squirrel across four gardens, lost it on a fence, and declared a moral victory.",
                5 => "🐶 Greeted every single person they passed, got pets from most of them.",
                6 => "🐶 Discovered a second dog on the adventure. They were best friends. They may never meet again. They are fine with this.",
                7 => "🐶 Found a stick of such remarkable quality that the rest of the adventure became secondary.",
                8 => "🐶 Rolled in something. Refuses to elaborate. Seems very pleased about it.",
                _ => "🐶 Found a hill, ran up it, barked at the sky, ran back down. Mission accomplished."
            },
            "horse" => Random.Shared.Next(10) switch
            {
                0 => "🐴 Galloped through open fields and scattered several pigeons.",
                1 => "🐴 Trotted through a village and was photographed by three tourists.",
                2 => "🐴 Jumped every fence they could find just for fun.",
                3 => "🐴 Cantered along a coastal cliffpath with dramatic flair.",
                4 => "🐴 Stood magnificently on a hillside while the wind did the rest of the work.",
                5 => "🐴 Explored a forest trail at full gallop and felt genuinely alive.",
                6 => "🐴 Found a wide open beach at low tide and ran the entire length of it twice, for no reason beyond the fact that they could.",
                7 => "🐴 Encountered another horse across a fence, exchanged a long and meaningful look, and moved on with a new sense of perspective.",
                8 => "🐴 Discovered a puddle they could not in good conscience not splash through, and didn't.",
                _ => "🐴 Discovered an apple orchard. Stayed there for a while. No regrets."
            },
            "bird" => Random.Shared.Next(10) switch
            {
                0 => "🐦 Soared high above the clouds and saw things you wouldn't believe.",
                1 => "🐦 Flew to a distant tree and eavesdropped on several conversations.",
                2 => "🐦 Rode a thermal updraft all the way to the hills and back.",
                3 => "🐦 Dive-bombed a scarecrow on principle and felt much better afterwards.",
                4 => "🐦 Found the most acoustically perfect canyon in the region and sang into it.",
                5 => "🐦 Perched on a weather vane and surveyed the whole town like a general.",
                6 => "🐦 Discovered a window with a very good reflection, gave it a thorough talking-to, and departed satisfied.",
                7 => "🐦 Flew further than intended, realised it was scenic, kept going, and returned with a story they'll never fully explain.",
                8 => "🐦 Found an outdoor concert, perched above the speakers, and contributed several unrequested solos.",
                _ => "🐦 Raced the wind across three counties. The wind lost."
            },
            "dinosaur" => Random.Shared.Next(10) switch
            {
                0 => "🦕 Stomped through the forest and caused a minor local news story. The headline was respectful.",
                1 => "🦕 Waded through a river and frightened some ducks — and the fish, and a heron, and three kayakers.",
                2 => "🦕 Explored a canyon and left footprints so deep they'll perplex geologists for centuries.",
                3 => "🦕 Emerged from the treeline briefly, causing several hikers to rethink their life choices and career paths.",
                4 => "🦕 Investigated a mountain. Found it acceptable. Scratched a review into the rockface. Left.",
                5 => "🦕 Bellowed at a cliff face to test the echo. Was deeply satisfied. Did it four more times.",
                6 => "🦕 Discovered a prehistoric tar pit, sniffed it with genuine scientific interest, and wisely moved on.",
                7 => "🦕 Stumbled upon an active archaeological excavation site. The researchers will be talking about this for years.",
                8 => "🦕 Took an extended swim in a lake and briefly convinced a boater they'd discovered a genuine sea monster.",
                _ => "🦕 Crossed a swamp, found an ancient ruin, claimed it as territory, and took up a considerable amount of space doing all of it."
            },
            "bunny" => Random.Shared.Next(10) switch
            {
                0 => "🐰 Dug seventeen tunnels, explored five, and deemed the rest unnecessary.",
                1 => "🐰 Binkied through a meadow at top speed for reasons unknown.",
                2 => "🐰 Discovered a clover patch and had the best hour of their life.",
                3 => "🐰 Thumped at a shadow, decided the shadow deserved it, moved on.",
                4 => "🐰 Explored an entire hedgerow system with terrifying efficiency.",
                5 => "🐰 Investigated a dandelion for ten minutes, then ate it, then found a better one.",
                6 => "🐰 Sat perfectly still in a field for twenty-five minutes and then binkied away at full speed. The sitting was clearly necessary.",
                7 => "🐰 Located the softest grass in a three-kilometre radius through a process that remains entirely mysterious.",
                8 => "🐰 Encountered another bunny, they exchanged a long nose-to-nose greeting, and both went their separate ways with new information.",
                _ => "🐰 Made a complete circuit of the meadow, found nothing threatening, logged it anyway."
            },
            "fish" => Random.Shared.Next(10) switch
            {
                0 => "🐟 Navigated a labyrinth of coral, befriended a crab, and returned with treasure.",
                1 => "🐟 Slipped through the deepest currents, saw things no fish should see.",
                2 => "🐟 Explored a sunken wreck and emerged carrying something shiny.",
                3 => "🐟 Descended to a pressure zone that would crush lesser creatures and felt fine.",
                4 => "🐟 Rode the Gulf Stream for a bit just to see where it went.",
                5 => "🐟 Found a thermal vent colony, made some connections, left before it got complicated.",
                6 => "🐟 Discovered a cavern system so deep and dark that the only light was themselves, and kept going anyway.",
                7 => "🐟 Joined a shoal briefly, led it for twenty minutes, declined to explain their qualifications, and departed.",
                8 => "🐟 Found a mirror-calm section of water at dawn and spent a meaningful amount of time considering their reflection.",
                _ => "🐟 Slipstreamed through a kelp forest at speed and spooked an entire shoal of herrings."
            },
            "shark" => Random.Shared.Next(10) switch
            {
                0 => "🦈 Cleared an entire section of ocean with a single fin breach.",
                1 => "🦈 Investigated a submersible, decided it was unworthy, and moved on.",
                2 => "🦈 Patrolled a ten-mile radius and returned with something interesting.",
                3 => "🦈 Circled a shipping lane three times. The crew never saw them. That was the point.",
                4 => "🦈 Found the wreck of an old galleon, explored it thoroughly, and left a tooth behind.",
                5 => "🦈 Dove to a depth where it's completely dark and felt perfectly at home.",
                6 => "🦈 Spent forty minutes investigating an underwater canyon so deep and still that even the pressure was respectful of them.",
                7 => "🦈 Encountered a pod of dolphins, maintained a ten-metre detente for thirty minutes, and departed with mutual respect established.",
                8 => "🦈 Performed a full breach directly alongside a whale shark, sized them up, nodded once in professional acknowledgement, and continued.",
                _ => "🦈 Emerged briefly near a surf beach, caused a mass exodus from the water, disappeared."
            },
            "wolf" => Random.Shared.Next(10) switch
            {
                0 => "🐺 Stalked through the forest like a shadow and returned without explaining themselves.",
                1 => "🐺 Howled at the moon, received a howl back, and decided the errand was complete.",
                2 => "🐺 Ranged across three hills, marked their territory extensively, and came home satisfied.",
                3 => "🐺 Tracked something through two valleys just to see if they could. They could.",
                4 => "🐺 Sat at the peak of a ridge in the rain for twenty minutes, communing with something.",
                5 => "🐺 Moved through the forest without snapping a single twig. Unnecessary, but satisfying.",
                6 => "🐺 Found the exact centre of a vast old-growth forest, sat down, and spent considerable time deciding whether to stay. Came back. For now.",
                7 => "🐺 Spent the night at the edge of the wild, watching the boundary between settled land and forest with philosophical attention.",
                8 => "🐺 Ran flat out for an hour in no particular direction, stopped at a peak, surveyed everything below, and came home without offering context.",
                _ => "🐺 Found a frozen lake, tested every inch of the edge, and crossed it anyway."
            },
            "lizard" => Random.Shared.Next(10) switch
            {
                0 => "🦎 Basked on a warm rock for an indeterminate amount of time, then got to business.",
                1 => "🦎 Climbed every vertical surface in the area just to see if they could.",
                2 => "🦎 Changed colour seventeen times and confused a photographer.",
                3 => "🦎 Found a sun-baked wall and pressed their whole body against it with visible satisfaction.",
                4 => "🦎 Scurried through a ruined building and claimed it as their territory.",
                5 => "🦎 Stalked an insect across a garden for eleven minutes and then let it go. Power move.",
                6 => "🦎 Discovered a greenhouse, tested every pane of glass for temperature, and ranked them by quality in an internal list.",
                7 => "🦎 Found a basking rock of such specific geometry and solar orientation that they appear to have located the ideal object in the known world.",
                8 => "🦎 Navigated a complex rooftop system via routes no other creature could use and found something excellent on the far side.",
                _ => "🦎 Discovered a rock formation that perfectly concentrated heat and spent most of the trip there."
            },
            "otter" => Random.Shared.Next(10) switch
            {
                0 => "🦦 Floated downstream on their back, holding the reward the entire way.",
                1 => "🦦 Found a new rock, tested it thoroughly, and deemed it acceptable.",
                2 => "🦦 Slid down a muddy bank repeatedly before remembering the actual errand.",
                3 => "🦦 Wove through river reeds at high speed and startled a heron twice.",
                4 => "🦦 Dived to the bottom of a lake, found something interesting, dived back down to check it again.",
                5 => "🦦 Built a temporary floating raft from sticks, used it once, abandoned it without ceremony.",
                6 => "🦦 Located a section of river with exactly the right current for effortless floating, and floated it three times back-to-back.",
                7 => "🦦 Found a beaver dam, examined the construction with genuine professional interest, and made several silent criticisms.",
                8 => "🦦 Spent an hour in a tidal pool arranging and rearranging rocks, ate something, and concluded the expedition on their own terms.",
                _ => "🦦 Found a waterfall, swam up it, looked around, swam back down. Said nothing about it."
            },
            "bear" => Random.Shared.Next(10) switch
            {
                0 => "🐻 Investigated every log, overturned three boulders, and smelled a lot of interesting things.",
                1 => "🐻 Wandered considerably further than intended and had to be coaxed back with snacks.",
                2 => "🐻 Found a beehive, negotiated diplomatically, and left with both the treasure and their dignity.",
                3 => "🐻 Climbed a tree that was definitely not rated for their weight. Climbed back down. Fine.",
                4 => "🐻 Located a river with a strong salmon run and spent the best afternoon of the month there.",
                5 => "🐻 Found a cave, investigated it extensively, decided against moving in, but thought about it.",
                6 => "🐻 Sat beside a waterfall for a very long time doing nothing in particular, and returned noticeably more at peace.",
                7 => "🐻 Overturned a log of truly exceptional size, found it full of interest, and sat down to appreciate it properly.",
                8 => "🐻 Followed a creek upstream until they found where it came from, decided this was satisfying enough, and turned back.",
                _ => "🐻 Sat in a berry patch for an undisclosed amount of time. No further questions."
            },
            "insect" => Random.Shared.Next(10) switch
            {
                0 => "🐛 Navigated a complex obstacle course of grass blades and emerged victorious.",
                1 => "🐛 Flew fourteen hundred feet straight up just to see what was up there.",
                2 => "🐛 Explored a flower patch so thoroughly they came back dusted in pollen.",
                3 => "🐛 Discovered an anthill, introduced themselves, departed on good terms.",
                4 => "🐛 Climbed a sunflower to the very top and surveyed their domain.",
                5 => "🐛 Navigated three puddles and a compost heap without losing a single antenna.",
                6 => "🐛 Found a spider web of exquisite construction, studied it for an embarrassingly long time, and resolved to be better.",
                7 => "🐛 Toured seventeen flowers in methodical sequence and returned with scientific data that only they can interpret.",
                8 => "🐛 Discovered a patch of bioluminescent moss and spent the best forty minutes of their recent memory simply glowing near it.",
                _ => "🐛 Located a rotting log of remarkable complexity and spent most of the trip inside it."
            },
            "ocean_invertebrate" => Random.Shared.Next(10) switch
            {
                0 => "🐙 Squeezed into three places they definitely shouldn't fit and explored all of them.",
                1 => "🐙 Camouflaged as a rock for forty minutes, then got bored and went exploring.",
                2 => "🐙 Opened every container they encountered and left them all slightly ajar.",
                3 => "🐙 Descended into a thermal vent field and came back smelling unusual.",
                4 => "🐙 Disassembled a small crab trap purely out of curiosity, then reassembled it wrong.",
                5 => "🐙 Pursued eight separate interesting things simultaneously and finished all of them.",
                6 => "🐙 Found a submarine canyon, rappelled the entire depth using their own arms as anchors, and returned with a strong opinion about it.",
                7 => "🐙 Located a garden of sea anemones, spent thirty minutes changing colour to match each one, and moved on without comment.",
                8 => "🐙 Discovered a particularly fine piece of seafloor real estate, decorated it with twelve carefully selected objects, and then abandoned it for something better.",
                _ => "🐙 Found a shipwreck, entered through eight different access points, and ranked them by quality."
            },
            "land_invertebrate" => Random.Shared.Next(10) switch
            {
                0 => "🕷️ Scaled every vertical surface in the area and mapped them all with silk markers.",
                1 => "🕷️ Vanished into a log pile and emerged three hours later looking very pleased.",
                2 => "🕷️ Investigated the entire garden with methodical precision, leaving no stone unturned.",
                3 => "🕷️ Built a web in three separate locations, stress-tested each one, and kept the best.",
                4 => "🕷️ Found a dark cellar, catalogued its contents, and approved of the humidity.",
                5 => "🕷️ Stalked through tall grass for an hour with the energy of someone very much on a mission.",
                6 => "🕷️ Discovered a crevice of such perfect dimensions and darkness that they sat inside it for a long time simply appreciating the architecture.",
                7 => "🕷️ Built a web of unprecedented scale and complexity, took a look at it, and then built a completely different one beside it for comparison.",
                8 => "🕷️ Navigated an entire ecosystem in miniature — leaf litter, moss, soil, bark — and returned having thoroughly catalogued every corner of it.",
                _ => "🕷️ Rappelled off a cliff face eight times. The first seven were practice."
            },
            _ => "Set off and returned with something interesting."
        };

    /// <summary>
    /// Returns a randomised one-liner that appears before the adventure narrative
    /// in the return embed, adding variety to how the pet's homecoming is announced.
    /// </summary>
    public static string ExploreReturnOpener(string petName) =>
        Random.Shared.Next(14) switch
        {
            0 => $"After a long journey, **{petName}** has finally made it home.",
            1 => $"**{petName}** strolls back in like they were never gone.",
            2 => $"The door creaks open — **{petName}** is back.",
            3 => $"**{petName}** returns, slightly dirty, and clearly pleased with themselves.",
            4 => $"Word travels fast: **{petName}** has returned from the wild.",
            5 => $"**{petName}** drops something at your feet and looks up expectantly.",
            6 => $"Against all odds, **{petName}** made it back in one piece.",
            7 => $"**{petName}** appears at the threshold, carrying something interesting.",
            8 => $"The adventure is over — **{petName}** is home safe.",
            9 => $"**{petName}** bursts through the door with an unmistakable air of accomplishment.",
            10 => $"You weren't worried. **{petName}** was never not going to be fine.",
            11 => $"**{petName}** saunters back in and acts like the entire thing was routine.",
            12 => $"Tired but triumphant, **{petName}** has returned.",
            _ => $"**{petName}** is back — and they brought something with them."
        };


    public static readonly string[] PuzzleWords =
    [
      "gallop", "control", "ambush", "escape", "pace", "weather", "allied", "mind", "blue", "afield",
      "text", "stolen", "swan", "road", "bishop", "fear", "message", "earn", "caught", "soft",
      "people", "beaten", "farm", "defense", "bone", "glider", "central", "life", "shot", "agility",
      "grieve", "emit", "kettle", "global", "post", "drop", "hunting", "beacon", "maze", "gravel",
      "wild", "button", "access", "concern", "flag", "rant", "general", "genuine", "room", "name",
      "toggle", "gain", "effort", "link", "form", "view", "vessel", "absence", "disable", "convert",
      "emotion", "race", "parent", "clay", "hamlet", "cash", "reduce", "duress", "bridge", "sister",
      "tell", "artist", "gutter", "harbor", "rack", "absent", "call", "fern", "alleged", "backed",
      "side", "desert", "losing", "corn", "lawn", "knob", "robust", "diamond", "debt", "swim",
      "turf", "spindle", "news", "fickle", "compete", "fell", "cinder", "amongst", "monthly", "tiny",
      "cluster", "explore", "motion", "extreme", "before", "poem", "lack", "toss", "comb", "lend",
      "mirror", "site", "shop", "tonight", "forest", "soak", "encode", "handle", "quickly", "fusion",
      "outline", "tire", "finance", "icon", "castle", "maximum", "sock", "rush", "mosaic", "appear",
      "content", "gamble", "park", "protect", "mall", "couple", "falcon", "promise", "gust", "income",
      "menace", "lean", "worthy", "afflict", "warrior", "ruckus", "tusk", "emerge", "attempt", "lace",
      "kinship", "purpose", "lamp", "persist", "actual", "amended", "dazzle", "show", "belief", "search",
      "spiral", "append", "spoken", "hostage", "foam", "include", "able", "such", "damages", "revenue",
      "corner", "best", "herb", "lift", "deck", "trifle", "potter", "primary", "remain", "legacy",
      "clam", "lose", "outside", "aged", "dust", "mask", "code", "deal", "lion", "remove",
      "care", "charge", "flight", "commit", "earnest", "late", "fate", "either", "counsel", "reel",
      "symbol", "shin", "ship", "host", "wand", "provide", "speaker", "divest", "ability", "sour",
      "roar", "snow", "dusk", "pour", "sign", "vein", "village", "timbre", "strive", "market",
      "pretty", "reflect", "case", "tourism", "foster", "assign", "chapel", "gate", "mate", "isle",
      "chemist", "gale", "zeal", "letter", "tear", "edition", "assent", "avocado", "connect", "meal",
      "suffer", "fist", "saddle", "nose", "passage", "policy", "fort", "zipper", "blow", "dawdle",
      "full", "move", "design", "pack", "limited", "anchor", "town", "stay", "fashion", "luster",
      "simple", "strong", "mesh", "quarter", "rice", "soul", "church", "kidnap", "soap", "nested",
      "confer", "active", "imagine", "keen", "wolf", "worm", "revive", "throat", "finish", "mortal",
      "propose", "tall", "part", "careful", "plague", "spread", "reckon", "forbid", "dash", "healthy",
      "vest", "reed", "warm", "preview", "awkward", "type", "annual", "submit", "whip", "fish",
      "cereal", "through", "crab", "salmon", "figure", "decent", "bird", "latter", "decree", "compel",
      "cave", "horn", "loud", "logical", "slug", "cool", "hope", "popular", "leather", "thatch",
      "silk", "arrange", "bitter", "assault", "sprout", "hide", "cost", "lure", "unique", "afford",
      "barrel", "clue", "inject", "choose", "aerial", "tissue", "giggle", "adjourn", "fuse", "golden",
      "smooth", "needle", "basket", "wrench", "earned", "science", "consent", "edge", "western", "silence",
      "cell", "double", "calm", "normal", "seal", "fast", "mine", "define", "replace", "neutral",
      "hollow", "agency", "obtain", "mention", "middle", "hunger", "hardly", "advisor", "head", "denial",
      "capsule", "plunder", "vacuum", "repeat", "heat", "pass", "release", "mistake", "goat", "behave",
      "airways", "fondle", "catalog", "working", "trance", "lava", "disk", "nation", "restore", "density",
      "casual", "hawk", "villain", "cipher", "counter", "thread", "ginger", "cotton", "however", "teacher",
      "amount", "lucent", "discuss", "virtue", "ensure", "pirate", "help", "launch", "nothing", "sustain",
      "fantasy", "network", "lamb", "dimple", "narrow", "pick", "ascend", "dose", "single", "garlic",
      "pebble", "invest", "pure", "similar", "plot", "beckon", "attire", "beneath", "heal", "costly",
      "luxury", "hull", "profit", "goal", "expose", "tide", "hint", "shoe", "strain", "flinch",
      "request", "just", "goblin", "jester", "tussle", "zero", "rake", "wade", "crisis", "plan",
      "candle", "belong", "wide", "soil", "adrift", "dialect", "mild", "prefer", "sane", "common",
      "margin", "willing", "bother", "fragile", "last", "create", "lake", "fathom", "balance", "moth",
      "film", "tail", "epic", "august", "breach", "lively", "bond", "slip", "prison", "cobble",
      "ambient", "hole", "differ", "yawn", "century", "jagged", "permit", "path", "wake", "tube",
      "cobalt", "badger", "behind", "keep", "gram", "artery", "mingle", "stride", "told", "club",
      "cutlet", "embark", "whim", "gear", "yell", "assume", "present", "velvet", "exit", "climate",
      "without", "reason", "lumber", "uphold", "look", "slim", "wall", "planet", "street", "sunset",
      "rain", "formal", "fire", "step", "achieve", "author", "against", "outlet", "open", "league",
      "nest", "harden", "tension", "pose", "respect", "secure", "obvious", "massive", "loosen", "base",
      "once", "leap", "hill", "context", "forward", "invoke", "sore", "meet", "jitter", "mint",
      "hose", "savage", "intense", "gunner", "scholar", "safe", "melt", "admire", "mystic", "inform",
      "tone", "finger", "enhance", "glue", "ritual", "lament", "recover", "antler", "monitor", "hard",
      "fumble", "notable", "poll", "cavern", "tragedy", "acid", "dirt", "tame", "feline", "tremor",
      "reserve", "assess", "bounce", "jolt", "enable", "doodle", "dark", "command", "violet", "behalf",
      "unknown", "supply", "ordeal", "glow", "address", "feudal", "agreed", "pile", "jargon", "archer",
      "program", "hazard", "remote", "account", "adamant", "intent", "durable", "duster", "center", "aspire",
      "alight", "rocket", "rate", "budget", "peer", "ardent", "nowhere", "convey", "dangle", "abdomen",
      "boat", "kind", "pore", "blanket", "land", "cattle", "year", "curl", "history", "defeat",
      "patient", "perform", "attain", "spring", "blithe", "combat", "except", "cockpit", "feel", "sell",
      "retreat", "subtle", "complex", "cord", "gentle", "nimble", "gesture", "roam", "sole", "list",
      "tune", "haul", "struck", "insist", "fizzle", "affect", "wren", "discard", "fellow", "pier",
      "ramp", "rent", "adjust", "digital", "coat", "vine", "reap", "revolve", "collect", "soothe",
      "wither", "somehow", "oyster", "ring", "lime", "draw", "trim", "pipe", "comment", "blazer",
      "survive", "darkens", "student", "twin", "subject", "fact", "roof", "fade", "cuddle", "font",
      "fail", "crop", "reform", "node", "asylum", "captain", "game", "shelter", "burden", "bumble",
      "burn", "pardon", "reveal", "beyond", "export", "verb", "defend", "anxious", "sink", "wave",
      "poverty", "vain", "oath", "deploy", "sunrise", "spin", "banter", "hurt", "treaty", "yellow",
      "optimal", "canopy", "agenda", "outlaw", "discord", "robe", "chat", "duty", "wallet", "segment",
      "bedrock", "bundle", "heap", "left", "observe", "hunter", "loft", "another", "cook", "vary",
      "roll", "fame", "measure", "accent", "soar", "clip", "thunder", "dilemma", "team", "reverse",
      "chrome", "mail", "random", "crow", "pine", "much", "refine", "play", "lone", "throne",
      "foreign", "hearing", "worship", "rock", "current", "bath", "rattle", "direct", "offset", "diffuse",
      "monkey", "tape", "citrus", "jangle", "prey", "section", "equity", "meat", "sheriff", "filter",
      "eldest", "wine", "save", "service", "terror", "expand", "behold", "dynasty", "lander", "body",
      "effect", "repeal", "moment", "deposit", "pursue", "mission", "stop", "palm", "depart", "trek",
      "potent", "raid", "decline", "hail", "station", "toad", "fund", "sail", "lead", "assure",
      "jostle", "sickle", "dart", "darken", "assail", "harm", "receive", "star", "flow", "choice",
      "plunge", "know", "bull", "summit", "sway", "hero", "element", "hall", "quarry", "jump",
      "missing", "nail", "port", "moon", "machine", "vagrant", "empire", "target", "ride", "time",
      "tank", "born", "sand", "bear", "flip", "moss", "note", "wooden", "humble", "string",
      "endless", "sample", "alpine", "stem", "attach", "anthem", "memo", "peak", "overdo", "beat",
      "ticket", "devoted", "scandal", "urge", "vast", "strike", "mitten", "threat", "island", "camp",
      "milk", "advent", "ward", "ravine", "invent", "debris", "hunt", "fold", "tunnel", "trap",
      "spur", "entire", "abrupt", "sale", "extent", "rule", "fuel", "bell", "jungle", "victor",
      "almost", "trophy", "nook", "import", "rise", "bagpipe", "task", "offense", "relief", "skin",
      "trouble", "contain", "involve", "detect", "capture", "deep", "poison", "loop", "lock", "coal",
      "file", "accused", "sing", "debate", "altered", "size", "rest", "rank", "perhaps", "meadow",
      "push", "join", "food", "aligned", "passion", "scratch", "rust", "manner", "hasten", "weld",
      "plum", "further", "coin", "someone", "devote", "exceed", "quiver", "cactus", "compare", "caption",
      "emperor", "average", "serious", "sick", "soup", "credit", "slot", "vale", "mood", "anyway",
      "stroke", "crutch", "pattern", "rely", "fleece", "ruin", "dune", "tool", "defined", "daring",
      "wind", "welfare", "mill", "impact", "pair", "line", "public", "website", "patron", "ethnic",
      "find", "insult", "mane", "cartoon", "pendant", "fall", "adhere", "wish", "battle", "zealot",
      "tender", "curtain", "pocket", "brother", "gift", "luggage", "indeed", "dabble", "busy", "academy",
      "risk", "unearth", "barley", "courage", "tree", "unlock", "soldier", "anymore", "will", "alchemy",
      "desire", "matter", "reality", "gore", "seek", "vent", "tend", "dead", "tangle", "serene",
      "rugged", "wraith", "assert", "deadly", "mold", "root", "mineral", "king", "pond", "jumble",
      "hook", "drum", "loom", "item", "warp", "clothe", "fair", "book", "girder", "triple",
      "bank", "agitate", "allure", "always", "donkey", "twitch", "stadium", "detail", "already", "furnish",
      "tavern", "bestow", "gotten", "fiscal", "cutting", "dollar", "page", "circle", "beauty", "seed",
      "modest", "embrace", "dove", "woolen", "load", "hidden", "absolve", "adverse", "west", "portion",
      "ball", "expect", "chip", "output", "stir", "dock", "chance", "regard", "enforce", "hornet",
      "glad", "suit", "clarity", "process", "catnap", "distant", "lick", "concur", "wise", "warden",
      "lineage", "guitar", "donate", "select", "engage", "winning", "ensign", "till", "second", "rabbit",
      "acquire", "barrier", "exam", "relate", "conduct", "fender", "pump", "navy", "halt", "vendor",
      "read", "package", "rustle", "hive", "veil", "palace", "kilter", "iron", "moor", "drip",
      "rail", "curious", "ailment", "animal", "dismiss", "archway", "cute", "harvest", "theater", "require",
      "cabinet", "notice", "envy", "future", "hustle", "deer", "amazing", "unfold", "salt", "levy",
      "enough", "whisper", "toll", "burrow", "justice", "face", "hash", "snap", "absorb", "sector",
      "avenue", "make", "economy", "private", "fallow", "feed", "picture", "catchy", "agonize", "gang",
      "vote", "hold", "ford", "bottle", "frog", "assist", "skip", "pain", "bright", "hack",
      "harmony", "firm", "opinion", "shut", "wash", "wing", "coastal", "nominal", "grip", "fill",
      "accuse", "gown", "kick", "quality", "lessen", "zoom", "spot", "mangle", "gaze", "dragon",
      "evolve", "article", "unclear", "freedom", "strict", "leaf", "decode", "tuck", "gulf", "tilt",
      "possess", "surf", "answer", "easily", "role", "wonder", "gold", "trip", "ripple", "charity",
      "rash", "cousin", "branch", "misery", "extract", "ceiling", "unit", "issued", "card", "proven",
      "awaken", "hand", "helm", "broken", "accord", "extend", "stream", "gadget", "tour", "word",
      "coupon", "well", "vanish", "tile", "lizard", "feet", "nature", "honest", "update", "back",
      "worried", "course", "famine", "fierce", "kernel", "affable", "advance", "enacted", "slam", "wire",
      "within", "barter", "finding", "hang", "happen", "mass", "reject", "fork", "junk", "tent",
      "contest", "data", "vision", "unusual", "attend", "haze", "mixture", "yeoman", "grow", "settle",
      "species", "object", "swap", "tray", "send", "desk", "mist", "motive", "wander", "bouquet",
      "sort", "rescue", "blossom", "nettle", "urgency", "zone", "herald", "fortune", "pierce", "torment",
      "jail", "glance", "abstain", "bailey", "rage", "lane", "freckle", "span", "lantern", "riot",
      "slow", "pool", "autumn", "afresh", "proper", "cavalry", "society", "crucial", "strange", "marvel",
      "band", "switch", "void", "temple", "loss", "archive", "puzzle", "habitat", "crystal", "pull",
      "oblige", "tumble", "hire", "banquet", "screen", "invade", "portal", "modern", "project", "negate",
      "bronze", "riddle", "mash", "peel", "bandage", "fabric", "journey", "comedy", "should", "plow",
      "endure", "mutter", "decade", "change", "surface", "cold", "joke", "induce", "blight", "frozen",
      "knot", "resolve", "walk", "chapter", "sketch", "wrap", "idle", "abandon", "neck", "wage",
      "armored", "ablaze", "failure", "misuse", "horizon", "abused", "tale", "custom", "signal", "prop",
      "turtle", "dawn", "song", "ancient", "demand", "racial", "rope", "dish", "anyone", "fool",
      "bargain", "mark", "gather", "hurdle", "decide", "comply", "rose", "chosen", "dive", "lovable",
      "engine", "mile", "twig"
    ];


    /// <summary>Maps a pet journal event type (feed, hug, explore, etc.) to its display emoji.</summary>
    public static string JournalEventEmoji(string eventType) => eventType.ToLower() switch
    {
        "feed" => "🍽️",
        "wake" => "🌅",
        "hug" => "🤗",
        "pet" => "🖐️",
        "groom" => "🛁",
        "play" => "🎮",
        "sleep" => "💤",
        "explore" => "🗺️",
        "battle" => "⚔️",
        "levelup" => "🎉",
        "adopt" => "🐾",
        "trick" => "🎪",
        _ => "📌"
    };


    /// <summary>
    /// Calculates a pet's battle power from level, stats, and a luck roll.
    /// Level contributes the most, stats tune it, and luck keeps lower-level
    /// pets competitive so battles aren't always deterministic.
    /// </summary>
    public static int BattlePower(int level, int hunger, int happiness, int energy)
    {
        int basePower = level * 10;
        int statBonus = (hunger + happiness + energy) / 10;
        int luck = Random.Shared.Next(1, 26); // ±25 luck roll
        return basePower + statBonus + luck;
    }

    /// <summary>
    /// Returns 3 individual round strings for animated battle display.
    /// </summary>
    public static string[] GenerateBattleRounds(string attackerName, string attackerSpecies, int attackerPower, string defenderName, string defenderSpecies, int defenderPower, bool draw)
    {
        var moves = BattleMoves(attackerSpecies);
        var counterMoves = BattleMoves(defenderSpecies);

        string r3 = draw
            ? "💥 **Round 3:** Both pets clash and neither gives an inch!"
            : attackerPower > defenderPower
                ? $"💥 **Round 3:** {attackerName} lands the finishing move!"
                : $"💥 **Round 3:** {defenderName} turns the tide and lands the final blow!";

        return
        [
            $"⚔️ **Round 1:** {attackerName} uses *{moves[Random.Shared.Next(moves.Length)]}*!",
            $"🛡️ **Round 2:** {defenderName} counters with *{counterMoves[Random.Shared.Next(counterMoves.Length)]}*!",
            r3
        ];
    }

    /// <summary>Generates a short flavour battle log across 3 rounds (legacy single-string version).</summary>
    public static string GenerateBattleLog(
        string attackerName, string attackerSpecies, int attackerPower,
        string defenderName, string defenderSpecies, int defenderPower,
        bool draw)
    {
        var rounds = GenerateBattleRounds(
            attackerName, attackerSpecies, attackerPower,
            defenderName, defenderSpecies, defenderPower, draw);
        return string.Join("\n", rounds);
    }

    /// <summary>
    /// Returns a species-contextual flavour description for an explore reward.
    /// Falls back to the generic description if no override exists.
    /// </summary>
    public static string ExploreRewardDescription(string rewardKey, string species, string genericDescription) =>
        (rewardKey, species.ToLower()) switch
        {
            // Bones
            ("common_bone", "dinosaur") => "Unearthed an ancient fossil fragment!",
            ("common_bone", "dog") => "Found a perfectly aged bone buried in a park!",
            ("common_bone", "cat") => "Dragged home a mysterious bone from somewhere",
            ("common_bone", "wolf") => "Returned with a bone of impressive provenance and will not explain further",
            ("common_bone", "bear") => "Excavated a tremendous old bone from beneath a boulder — took some effort",
            ("common_bone", "lizard") => "Dragged back a sun-bleached bone nearly as long as themselves",
            // Flowers
            ("common_flower", "bunny") => "Nibbled on a fresh wildflower and brought the rest back!",
            ("common_flower", "horse") => "Pranced through a meadow and returned with flowers in their mane!",
            ("common_flower", "insect") => "Carefully carried back a bloom still fully loaded with pollen",
            ("common_flower", "otter") => "Found a water lily, tasted it, decided it was a gift, brought it home",
            ("common_flower", "bird") => "Wove a found flower into their feathers and arrived home looking excellent",
            // Sticks
            ("common_stick", "dog") => "Found the ultimate stick and refuses to let go of it",
            ("common_stick", "bird") => "Carried back a perfect nesting twig!",
            ("common_stick", "otter") => "Selected a stick of very precise length and carried it all the way home with purpose",
            ("common_stick", "wolf") => "Returned carrying a branch that is, objectively, more of a small tree",
            // Rocks
            ("common_rock", "dinosaur") => "Found a petrified rock that looks suspiciously old",
            ("common_rock", "otter") => "Sourced a pebble of extraordinary smoothness that now lives in the special pile",
            ("common_rock", "lizard") => "Returned with a flat stone of ideal basking geometry",
            ("common_rock", "ocean_invertebrate") => "Carried home a rock of apparently perfect size, weight, and placement potential",
            ("common_rock", "land_invertebrate") => "Spent considerable time selecting exactly the right rock from what must have been a very large sample",
            // Coins
            ("uncommon_coin", "bird") => "Spotted a shiny coin from the sky and dove for it!",
            ("uncommon_coin", "cat") => "Batted a coin out of a fountain and brought it home",
            ("uncommon_coin", "dinosaur") => "Unearthed a coin so old it predates the mint that made it — probably.",
            ("uncommon_coin", "otter") => "Spotted a glint at the riverbed, dived, retrieved a coin of interesting vintage",
            ("uncommon_coin", "shark") => "Located a coin on the ocean floor with the precision of something that does this professionally",
            ("uncommon_coin", "fish") => "Found a coin among the pebbles at the bed of a very clear stream",
            // Berries
            ("uncommon_berry", "bunny") => "Discovered a wild berry patch and ate an alarming quantity",
            ("uncommon_berry", "bird") => "Found a berry-covered bush and had a feast!",
            ("uncommon_berry", "dinosaur") => "Trampled through an entire berry patch, ate most of it, and returned the rest as tribute.",
            ("uncommon_berry", "bear") => "Located a wild berry patch and sat in it for a meaningful stretch of time",
            ("uncommon_berry", "horse") => "Grazed through a section of wild berry hedge and arrived home with a purple-stained nose",
            // Feathers
            ("uncommon_feather", "bird") => "Found a feather from a species they've never encountered!",
            ("uncommon_feather", "cat") => "Stalked and observed a mystery bird, returned with proof",
            ("uncommon_feather", "dinosaur") => "Returned with a pterodactyl feather. From *where*, exactly, is unclear and perhaps best not examined.",
            ("uncommon_feather", "wolf") => "Found a large, striking feather and carried it home with unexpected gentleness",
            ("uncommon_feather", "lizard") => "Retrieved a bright feather from somewhere very high up — no further information available",
            // Gems
            ("rare_gem", "dinosaur") => "Dug up a prehistoric gemstone embedded in ancient rock!",
            ("rare_gem", "bird") => "Spotted a glittering gem from high altitude and retrieved it!",
            ("rare_gem", "otter") => "Dove to the riverbed and emerged with something that was definitely not just a pebble",
            ("rare_gem", "cat") => "Located a glittering gem through methods that suggest better eyesight than previously disclosed",
            ("rare_gem", "shark") => "Retrieved a gemstone from a crevice at significant depth without apparent difficulty",
            ("rare_gem", "ocean_invertebrate") => "Found a gemstone on the seafloor and brought it back arranged with three complementary rocks",
            // Maps
            ("rare_map", "dinosaur") => "Dug up a territorial map scratched into ancient stone. The territory marked is enormous.",
            ("rare_map", "wolf") => "Unearthed a hand-drawn map, studied it at length, and returned appearing to know things",
            ("rare_map", "cat") => "Found a rolled map, batted it around for a bit, and brought home the piece that interested them",
            ("rare_map", "fish") => "Recovered a waterlogged map from a shipwreck that somehow remained entirely legible",
            ("rare_map", "shark") => "Found an old nautical chart on the seafloor — accurate by their own assessment, though not by any human one",
            // Crowns
            ("rare_crown", "horse") => "Somehow returned wearing a tiny crown — regal as always",
            ("rare_crown", "cat") => "Found a crown, tried it on, decided it was beneath them, brought it back anyway",
            ("rare_crown", "dinosaur") => "Returned with a tiny crown balanced on their snout. Refused to explain. Refused to remove it.",
            ("rare_crown", "bear") => "Arrived home wearing a small crown at an angle that communicated genuine ownership",
            ("rare_crown", "wolf") => "Returned wearing a circlet of twisted wire and bark that somehow looked intentional",
            // Treasure
            ("epic_treasure", "dinosaur") => "Uncovered a chest buried since prehistoric times!",
            ("epic_treasure", "dog") => "Followed their nose to an entire buried treasure chest!",
            ("epic_treasure", "shark") => "Brought up a sealed chest from the bottom of a shipwreck they apparently knew the location of",
            ("epic_treasure", "otter") => "Assembled a small personal treasure from riverbed finds, then found an actual chest, and brought both",
            ("epic_treasure", "ocean_invertebrate") => "Recovered a chest from a wreck and arrived home with it tucked under three arms",
            // Artifacts
            ("epic_artifact", "dinosaur") => "Found a fossilised artefact predating recorded history!",
            ("epic_artifact", "bird") => "Spotted an ancient artefact from above and retrieved it!",
            ("epic_artifact", "lizard") => "Emerged from under an old ruin carrying something the archaeologists will want to know about",
            ("epic_artifact", "cat") => "Knocked an ancient artefact out of a display case somewhere and brought it home as a gift",
            ("epic_artifact", "wolf") => "Dug up something ancient from a hillside that has absolutely no business being there",
            ("epic_artifact", "ocean_invertebrate") => "Retrieved an artefact of unknown origin from an unmarked section of seafloor",
            // Legendary star
            ("legendary_star", "bunny") => "Binky'd so high they accidentally caught a falling star!",
            ("legendary_star", "horse") => "Galloped so fast they outran the night and caught a star!",
            ("legendary_star", "dinosaur") => "Roared at the sky with such force that a star fell. Returned extremely pleased with themselves.",
            ("legendary_star", "bird") => "Flew high enough to catch a falling star on the way down — arrived home glowing faintly",
            ("legendary_star", "wolf") => "Howled at the right frequency and something fell from the sky toward them — they caught it",
            ("legendary_star", "cat") => "Batted a falling star out of the air as it passed — seemed to find this ordinary",
            ("legendary_star", "shark") => "Breached at precisely the right moment and the star landed in their path — they took it",
            ("legendary_star", "otter") => "Was floating on their back at exactly the right time and place, and simply caught it",
            _ => genericDescription
        };

    /// <summary>Returns the pool of flavour move names a species can draw from in a pet battle round.</summary>
    private static string[] BattleMoves(string species) => species.ToLower() switch
    {
        "cat" =>
        [
            "Paw Swipe", "Hiss Blast", "Furball Throw", "Scratch Flurry", "Disappearing Act",
            "Midnight Zoomies", "Knock It Off (sends opponent's items flying)", "Void Stare",
            "Chaos Pounce", "Smug Dodge"
        ],
        "dog" =>
        [
            "Bark Shock", "Pounce", "Zoomie Tackle", "Fetch Frenzy", "Puppy Eyes (confuses opponent)",
            "Slobber Barrage", "Sock Steal", "Spin-Out Collision", "Good Boy Aura (buffs self)",
            "Tennis Ball Volley"
        ],
        "horse" =>
        [
            "Hoof Stomp", "Gallop Charge", "Mane Whip", "Rear Kick", "Whinny Shockwave",
            "Dressage Flourish (distracts opponent)", "Full Gallop Rampage", "Fence Jump (evades)",
            "Thunder Canter", "Stallion's Roar"
        ],
        "bird" =>
        [
            "Wing Gust", "Talon Strike", "Sonic Chirp", "Dive Bomb", "Feather Flurry",
            "Thermal Ascent (gains height advantage)", "Aerial Mockery (debuffs opponent confidence)",
            "Beak Peck Rapid Fire", "Flock Signal (calls for backup)", "Shriek of the Heavens"
        ],
        "dinosaur" => ["Stomp Quake", "Tail Slam", "Prehistoric Roar", "Chomp", "Meteor Crash", "Fossil Barrage", "Extinction Event", "Thunder Rex", "Saurian Fury", "Bone Crush"],
        "bunny" =>
        [
            "Binky Blitz", "Thump Wave", "Ear Slap", "Speed Dash", "Adorable Stare (stuns)",
            "Clover Blitz", "Tunnel Escape (evades)", "Flop (confuses opponent into concern)",
            "Rapid Rear Kick", "Nose Twitch (unsettles opponent)"
        ],
        "fish" =>
        [
            "Bubble Burst", "Fin Slash", "Current Surge", "Ink Cloud", "Depth Charge",
            "Pressure Wave", "Spiral Rush", "Blind Splash", "Electric Shock (for certain breeds)",
            "School Formation Slam"
        ],
        "shark" =>
        [
            "Jaw Snap", "Death Roll", "Breach Slam", "Pressure Wave", "Feeding Frenzy",
            "Torpedo Rush", "Depth Charge", "Fin Blade Sweep", "Apex Predator Aura (intimidates)",
            "Circling Terror (stalls opponent)"
        ],
        "wolf" =>
        [
            "Pack Howl", "Lunge Bite", "Shadow Pounce", "Feral Snarl", "Moonlit Frenzy",
            "Phantom Stalk (opponent loses track)", "Throat Bite", "Winter's Sprint",
            "Pack Tactics (combo move)", "Dire Warning (reduces opponent's power)"
        ],
        "lizard" =>
        [
            "Tail Whip", "Tongue Lash", "Scale Spike", "Venom Spit", "Camouflage Strike",
            "Frill Flare (startles opponent)", "Rock Dash", "Dewlap Intimidation",
            "Shed Tail Distraction", "Solar Beam (charged from basking)"
        ],
        "otter" =>
        [
            "Rock Throw", "Belly Flop", "Slippery Dodge", "River Rush", "Pebble Barrage",
            "Spinning Splash Attack", "Current Redirect", "Tail Slap",
            "Raft Launch", "Cheerful Chaos (confuses opponent with unpredictability)"
        ],
        "bear" =>
        [
            "Bear Hug", "Swipe Claw", "Ground Pound", "Roar Blast", "Hibernate Charge",
            "Boulder Toss", "Salmon Slam", "Honey Trap (slows opponent)", "Mountain Lunge",
            "Apex Presence (opponent hesitates)"
        ],
        "insect" =>
        [
            "Sting Rush", "Wing Slash", "Swarm Call", "Venom Jab", "Metamorphosis Surge",
            "Pheromone Cloud (disorients opponent)", "Carapace Deflect", "Mandible Lock",
            "Bioluminescent Flash (blinds briefly)", "Rapid Molt (sheds damage)"
        ],
        "ocean_invertebrate" =>
        [
            "Ink Blast", "Tentacle Wrap", "Jet Propulsion", "Camouflage Strike", "Kraken's Grasp",
            "Colour Confusion (disorients opponent)", "Eight-Arm Slam", "Pressure Jet",
            "Mimic Form (copies opponent's last move)", "Deep Sea Surge"
        ],
        "land_invertebrate" =>
        [
            "Venom Strike", "Web Snare", "Pincer Crush", "Carapace Guard", "Scorpion Sting",
            "Silk Bind", "Leg Vibration (unsettles opponent)", "Exoskeleton Slam",
            "Ambush from Above", "Venomous Presence (passive damage)"
        ],
        _ => ["Tackle", "Scratch", "Growl"]
    };
}
