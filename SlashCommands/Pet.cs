using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using PetEntity = DiscordBot.Models.Generated.Pet;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Tamagotchi-inspired pet system.
/// Players adopt pets, care for them, earn XP through server activity,
/// and level them up to unlock new abilities and forms.
/// Up to 5 pets per user — one is "active" at a time.
/// </summary>
[Group("pet", "Pet commands")]
public class Pet(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourPet = EmbedColors.Peach;
    private static readonly Color ColourSuccess = EmbedColors.Green;
    private static readonly Color ColourError = EmbedColors.Red;
    private static readonly Color ColourInfo = EmbedColors.Blue;
    private static readonly Color ColourVeteran = EmbedColors.Gold;

    // Per-user battle cooldown (5 minutes)
    private static readonly ConcurrentDictionary<string, DateTime> _battleCooldowns = new();
    private const int BattleCooldownSeconds = 300;


    /// <summary>Adopts a new pet of the given species/breed with a chosen name (up to 100 per user), auto-marking it active if it's the user's first.</summary>
    [SlashCommand("adopt", "Adopt a new pet and give it a name!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleAdoptAsync(
        [Choice("Bear",         "bear"),
         Choice("Bird",         "bird"),
         Choice("Bunny",        "bunny"),
         Choice("Cat",          "cat"),
         Choice("Dinosaur",     "dinosaur"),
         Choice("Dog",          "dog"),
         Choice("Fish",         "fish"),
         Choice("Horse",        "horse"),
         Choice("Insect",       "insect"),
         Choice("Invertebrate (Land)",  "land_invertebrate"),
         Choice("Invertebrate (Ocean)", "ocean_invertebrate"),
         Choice("Lizard",       "lizard"),
         Choice("Otter",        "otter"),
         Choice("Shark",        "shark"),
         Choice("Wolf",         "wolf")]
        string species,
        [MinLength(1), MaxLength(32)] string name,
        [Autocomplete(typeof(BreedAutocompleteHandler))][MinLength(1), MaxLength(64)] string breed)
    {
        await DeferAsync();

        name = name.Trim();
        breed = breed.Trim();

        // Validate breed belongs to species
        if (!PetHelper.IsValidBreed(species, breed))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Adopt", $"**{breed}** isn't a valid breed for a {species}. Use `/breedlist {species}` to see options.", Username).Build());
            return;
        }

        int existingCount = await db.Pets.CountAsync(p => p.UserId == UserId);

        if (existingCount >= 100)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Adopt", "You already have 100 pets! Use `/release` to make room.", Username).Build());
            return;
        }

        bool makeActive = existingCount == 0;
        // Source (AddPet) deactivates all of the user's other pets first when the new one is
        // active, preserving the "exactly one active pet" invariant.
        if (makeActive)
            await db.Pets.Where(p => p.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        db.Pets.Add(new PetEntity
        {
            UserId = UserId, ServerId = ServerId, Name = name, Species = species, Breed = breed,
            IsActive = makeActive
        });
        await db.SaveChangesAsync();

        string emoji = PetHelper.PetEmoji(species, 100, 100, false, false);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{emoji}  Welcome, {name}!",
            $"You adopted a **{breed}** named **{name}**! 🎉\n\n" +
            $"Take good care of them — feed them, play with them, and keep them happy.\n\n" +
            $"Use `/pet card` to see their stats, and `/pet feed` when they get hungry!",
            ColourSuccess, footer: $"Adopted by {Username}", footerIconUrl: AvatarUrl).Build());
    }


    internal const int PetsPerPage = 5;
    internal static readonly Color PetAccentColor = EmbedColors.Peach;

    /// <summary>Lists all of the user's pets as a paginated embed with Prev/Next buttons.</summary>
    [SlashCommand("list", "List all your pets.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetsAsync()
    {
        await DeferAsync();

        var pets = await db.Pets.AsNoTracking().Where(p => p.UserId == UserId)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.BirthDate).ToListAsync();

        if (pets.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Pets", "You don't have any pets yet! Use `/adopt` to get one.", Username).Build());
            return;
        }

        await FollowupAsync(
            embed: PetPageHelper.BuildPetsPageEmbed(pets, 0, Username).Build(),
            components: PetPageHelper.BuildPetsPageButtons(UserId, 0, pets.Count));
    }


    /// <summary>Shows the active pet's full detailed stat card, including its last journal activity and any equipped title/aura cosmetics.</summary>
    [SlashCommand("card", "Show your active pet's full stat card.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetCardAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = row.PetId;

        // Fetch last journal entry to show in the card (source's GetPetJournal returns TOP 20
        // ORDER BY JournalID DESC, but the caller only ever used row[0] — just fetch that one).
        var lastJournal = await db.PetJournals.AsNoTracking()
            .Where(j => j.PetId == petId).OrderByDescending(j => j.JournalId).FirstOrDefaultAsync();

        string? lastActivity = null;
        if (lastJournal is not null)
        {
            string emoji = PetHelper.JournalEventEmoji(lastJournal.Event);
            string relTime = $"<t:{new DateTimeOffset(lastJournal.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()}:R>";
            lastActivity = $"{emoji} {lastJournal.Details} ({relTime})";
        }

        // Fetch cosmetics applied to this pet
        var cosmetics = await db.PetCosmetics.AsNoTracking().Where(c => c.PetId == petId).ToListAsync();

        string? titleKey = null, auraKey = null;
        foreach (var cr in cosmetics)
        {
            if (cr.CosmeticType == "title") titleKey = cr.CosmeticKey;
            if (cr.CosmeticType == "aura") auraKey = cr.CosmeticKey;
        }

        var (_, embed) = BuildPetEmbed(row, detailed: true, lastActivity: lastActivity,
            titleKey: titleKey, auraKey: auraKey);
        await FollowupAsync(embed: embed.Build());
    }


    /// <summary>Feeds the active pet a chosen food item (subject to level lock and cooldown), restoring hunger/happiness, waking it from hibernation if needed, and granting XP.</summary>
    [SlashCommand("feed", "Feed your active pet.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleFeedAsync(
        [Autocomplete(typeof(FoodAutocompleteHandler))][MinLength(1), MaxLength(64)] string food = "Kibble")
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(row.Xp);
        var foodItem = PetHelper.Foods.FirstOrDefault(f => f.name == food);

        if (foodItem == default)
        {
            await ErrorAsync($"**{food}** isn't a valid food item. Use `/foodlist` to see what's available.");
            return;
        }

        if (foodItem.minLevel > level)
        {
            await ErrorAsync($"**{food}** is locked until level **{foodItem.minLevel}**. Your pet is level {level}.");
            return;
        }

        if (row.LastFed is { } lastFed)
        {
            var remaining = lastFed.AddMinutes(PetHelper.FeedCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet isn't hungry yet! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int petId = row.PetId;
        int hunger = Math.Min(100, row.Hunger + foodItem.hungerRestore);
        int happiness = Math.Min(100, row.Happiness + foodItem.happyBonus);
        int energy = row.Energy;
        int hygiene = row.Hygiene;
        int oldXp = row.Xp;
        int newXp = oldXp + PetHelper.XpFeed;
        bool wasHibernating = row.IsHibernating;
        string petName = row.Name;

        ApplyPetStats(row, hunger, happiness, energy, hygiene, newXp, isHibernating: false, lastFed: DateTime.UtcNow);
        if (wasHibernating) row.HibernatedAt = null; // WakePet also clears this
        await db.SaveChangesAsync();

        await AddJournalEntryAsync(db, petId, wasHibernating ? "wake" : "feed", wasHibernating
            ? $"{Username} fed {petName} {foodItem.emoji} {food} and woke them from hibernation!"
            : $"{Username} fed {petName} {foodItem.emoji} {food}.");

        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{foodItem.emoji}  Fed {petName}!",
            $"You fed **{petName}** some **{food}**! {foodItem.emoji}\n\n" +
            (wasHibernating ? "🌅 **Your pet woke up from hibernation!**\n\n" : "") +
            $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**\n" +
            $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**" +
            (levelUp is not null ? $"\n\n{levelUp}" : ""),
            ColourSuccess, footer: $"{Username} • +{PetHelper.XpFeed} XP", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Pets the active pet for a happiness boost and XP, replying with a species-specific flavor reaction.</summary>
    [SlashCommand("pat", "Pet your active pet to boost their happiness!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetPetAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        if (row.LastPetted is { } lastPetted)
        {
            var remaining = lastPetted.AddMinutes(PetHelper.PetCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet needs a moment! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int happiness = Math.Min(100, row.Happiness + 15);
        int hunger = row.Hunger;
        int energy = row.Energy;
        int hygiene = row.Hygiene;
        int oldXp = row.Xp;
        int newXp = oldXp + PetHelper.XpPet;
        string petName = row.Name;
        string species = row.Species;

        ApplyPetStats(row, hunger, happiness, energy, hygiene, newXp,
            isHibernating: PetHelper.ShouldHibernate(hunger, happiness, energy), lastPetted: DateTime.UtcNow);
        await db.SaveChangesAsync();

        string[] reactions = species.ToLower() switch
        {
            "cat" =>
            [
                "*purrs contentedly* 😺",
                "*slow blinks at you* 😸",
                "*headbutts your hand* 🐱",
                "*kneads the air with absolute conviction, then pretends they weren't* 😺",
                "*tolerates exactly five seconds of petting, then bites you affectionately* 🐱",
                "*chirps a tiny trill and looks immediately embarrassed about it* 😸",
                "*accepts the attention as tribute, closes eyes regally, and begins purring at 40hz* 🐱",
            ],
            "dog" =>
            [
                "*tail wagging intensifies* 🐶",
                "*licks your face* 🐕",
                "*rolls over for belly rubs* 🐾",
                "*entire back half wiggles independently of the front half* 🐶",
                "*brings you a toy as a gift, then immediately wants it back* 🐕",
                "*makes direct eye contact and wags so hard they nearly spin out* 🐾",
                "*lets out a long, satisfied groan and melts completely onto the floor* 🐶",
            ],
            "horse" =>
            [
                "*nuzzles you gently* 🐴",
                "*whinnies happily* 🐎",
                "*tosses their mane* 🐴",
                "*blows warm air through their nose directly into your hair* 🐎",
                "*leans into your hand with the full weight of their enormous face* 🐴",
                "*lets out a low, rumbly nicker and stamps one hoof contentedly* 🐎",
                "*rests their chin on your shoulder and exhales a sigh of absolute peace* 🐴",
            ],
            "bird" =>
            [
                "*chirps excitedly* 🐦",
                "*flaps their wings happily* 🦜",
                "*whistles a little tune* 🎵",
                "*tilts their head sideways and blinks one eye very slowly* 🦜",
                "*fluffs up to twice their normal size and vibrates with joy* 🐦",
                "*does a little foot-to-foot happy dance along the perch* 🎵",
                "*opens their beak and produces a sound suspiciously close to 'thank you'* 🦜",
            ],
            "dinosaur" =>
            [
                "*lowers their enormous snout for scritches and produces a low rumble you feel through the floor* 🦕",
                "*lets out a series of rapid chirps and trills — apparently the scientifically accurate sound — which is somehow completely adorable* 🦖",
                "*wags their tail with such enthusiasm it rearranges nearby furniture. Totally worth it.* 🐉",
                "*bumps you with their forehead so gently it barely registers. For their size, this restraint is heroic.* 🦕",
                "*goes completely still as you scratch behind their frill, then tilts the entire enormous head sideways, demanding more* 🦖",
                "*closes both eyes with unmistakable satisfaction. The rumbling continues for several minutes.* 🐉",
                "*briefly chases their own tail, catches it, releases it with dignity, then stares off into the distance as though it never happened* 🦕",
            ],
            "bunny" =>
            [
                "*thumps happily* 🐰",
                "*licks your hand* 🐇",
                "*flops onto their side (the highest bunny compliment)* 😊",
                "*binkies in a tiny circle and then freezes as if it never happened* 🐰",
                "*grooms your finger very seriously before accepting further pats* 🐇",
                "*does a full body shiver of joy that starts at their nose and ripples to their cottontail* 🐰",
                "*nudges your hand back into position when you stop. You do not stop again.* 🐇",
            ],
            "fish" =>
            [
                "*does an excited lap around the tank* 🐟",
                "*bobs to the surface and blows an approving bubble* 🐠",
                "*flares their fins in a dazzling display of happiness* 🐡",
                "*swims figure-eights against your finger through the glass* 🐟",
                "*presses their tiny face to the tank wall and studies you with both eyes* 🐠",
                "*performs a rapid barrel roll and then freezes, looking extremely pleased* 🐡",
                "*darts to the bottom, spirals back up, and fans their tail in what can only be described as a bow* 🐟",
            ],
            "shark" =>
            [
                "*nudges your hand with their snout surprisingly gently* 🦈",
                "*glides slowly past and leans into your hand for a brief moment* 🦈",
                "*opens their mouth slightly and lets you scratch underneath their jaw* 🦈",
                "*bumps you with their nose so hard you nearly topple over. It's affection. Probably.* 🦈",
                "*rolls slightly sideways, exposing their belly — this is trust, not a threat* 🦈",
                "*circles back three times, each pass a little slower, finally resting against your hand* 🦈",
                "*makes direct, unblinking eye contact for a full five seconds, then leans in for scritches* 🦈",
            ],
            "wolf" =>
            [
                "*huffs softly and leans against your leg* 🐺",
                "*allows exactly one ear scratch, then sits up with dignity* 🐺",
                "*holds eye contact for a long moment, then briefly closes both eyes — highest wolf honour* 🐺",
                "*presses their forehead firmly against your hand and exhales very slowly* 🐺",
                "*lets out a low, soft rumble that vibrates right through your hand* 🐺",
                "*looks pointedly away while very clearly enjoying every second of this* 🐺",
                "*flicks an ear, turns their whole head to look at you, and then shoves into your palm* 🐺",
            ],
            "lizard" =>
            [
                "*tilts their head toward your hand and closes both eyes* 🦎",
                "*does exactly two push-ups to acknowledge you and holds the second one* 🦎",
                "*puffs their throat dramatically and turns a slightly warmer colour* 🦎",
                "*climbs onto your hand and sits very still, absorbing warmth with obvious satisfaction* 🦎",
                "*flicks their tongue rapidly — tasting the air, approving of what they find* 🦎",
                "*bobs their head in a slow, deliberate nod that means something important to them* 🦎",
                "*raises one front foot and holds it mid-air for a long moment, then presses it against your thumb* 🦎",
            ],
            "otter" =>
            [
                "*grabs your hand with both paws and examines it thoroughly before accepting the pat* 🦦",
                "*rolls upside-down and waves all four paws in the air* 🦦",
                "*produces a series of rapid, chittering squeaks that definitely mean 'more'* 🦦",
                "*wraps both arms around your wrist and refuses to let go for a very comfortable amount of time* 🦦",
                "*does a full body wiggle and then presents you with their favourite pebble as thanks* 🦦",
                "*spins in a tight circle, flops flat onto their back, and gazes at you upside-down* 🦦",
                "*makes a sound like a tiny squeaky door and immediately looks proud of it* 🦦",
            ],
            "bear" =>
            [
                "*huffs a warm breath and leans into your hand like a boulder slowly shifting* 🐻",
                "*sits up straight, places one huge paw on your shoulder, and regards you warmly* 🐻",
                "*makes a slow, resonant woof sound somewhere between a sigh and approval* 🐻",
                "*closes their eyes and lets out the longest exhale you've ever heard* 🐻",
                "*tilts their entire enormous head into your hand and goes very still* 🐻",
                "*chuffs softly and bobs their head — this is a bear 'thank you'* 🐻",
                "*rubs the side of their face against your palm so firmly they nearly knock you over* 🐻",
            ],
            "insect" =>
            [
                "*glows softly for a moment and then dims back down with a contented flutter* 🐛",
                "*vibrates their wings at a frequency that is somehow deeply soothing* 🦋",
                "*does a tiny antennae-wiggle that means more than it seems* 🐛",
                "*walks very carefully across your palm like it's the most important terrain they've crossed* 🦋",
                "*fans their wings open to full display for exactly two seconds, then closes them again* 🐛",
                "*clicks their mandibles twice in rapid succession — this is apparently applause* 🦋",
                "*does three slow circles on your hand, settles in the exact centre, and goes perfectly still* 🐛",
            ],
            "ocean_invertebrate" =>
            [
                "*extends two tentacles toward your hand and pats you back* 🐙",
                "*changes colour to a warm amber — their happy colour* 🐙",
                "*curls one arm into a neat spiral, which is their version of a wave* 🐙",
                "*rises slightly in the water and hovers at eye level, studying you with great interest* 🐙",
                "*wraps three arms loosely around your wrist and pulses gently* 🐙",
                "*briefly inks a tiny cloud, then fans it away immediately, embarrassed* 🐙",
                "*arranges their arms into a rosette shape and holds it for a suspiciously long time* 🐙",
            ],
            "land_invertebrate" =>
            [
                "*raises both front legs slowly and holds them there — high five, maybe* 🕷️",
                "*walks very deliberately onto your outstretched hand and sits perfectly still* 🦂",
                "*taps the ground three times, which you choose to interpret as affection* 🕷️",
                "*produces a small silk thread, anchors it to your finger, and swings from it briefly* 🕷️",
                "*vibrates all eight legs in a rapid shiver that reads as enthusiasm* 🦂",
                "*rotates slowly to face you directly and holds that posture with quiet intensity* 🕷️",
                "*extends one leg, touches your hand, withdraws it, then extends it again — clearly a greeting* 🦂",
            ],
            _ => ["*enjoys the attention* 🐾"]
        };

        string reaction = reactions[Random.Shared.Next(reactions.Length)];
        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🤗  Petted {petName}!",
            $"**{petName}** {reaction}\n\n" +
            $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**" +
            (levelUp is not null ? $"\n\n{levelUp}" : ""),
            ColourPet, footer: $"{Username} • +{PetHelper.XpPet} XP", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Grooms the active pet to restore hygiene (and a little happiness), with a species-specific flavor verb and journal entry.</summary>
    [SlashCommand("groom", "Groom your active pet to boost their hygiene!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleGroomAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        if (row.LastGroomed is { } lastGroomed)
        {
            var remaining = lastGroomed.AddMinutes(PetHelper.GroomCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet is already clean! Come back in **{(int)remaining.TotalMinutes}m**.");
                return;
            }
        }

        int petId = row.PetId;
        int hygiene = Math.Min(100, row.Hygiene + 40);
        int happy = Math.Min(100, row.Happiness + 10);
        int hunger = row.Hunger;
        int energy = row.Energy;
        int oldXp = row.Xp;
        int newXp = oldXp + PetHelper.XpGroom;
        string petName = row.Name;
        string species = row.Species;

        ApplyPetStats(row, hunger, happy, energy, hygiene, newXp,
            isHibernating: PetHelper.ShouldHibernate(hunger, happy, energy), lastGroomed: DateTime.UtcNow);
        await db.SaveChangesAsync();

        string groomVerb = species.ToLower() switch
        {
            "cat" => "brushed",
            "dog" => "bathed",
            "horse" => "groomed",
            "bird" => "preened",
            "dinosaur" => "hosed down",
            "bunny" => "gently brushed",
            "fish" => "cleaned up after",
            "shark" => "scrubbed down",
            "wolf" => "brushed out",
            "lizard" => "carefully wiped down",
            "otter" => "dried and fluffed up",
            "bear" => "thoroughly brushed",
            "insect" => "gently dusted off",
            "ocean_invertebrate" => "carefully rinsed",
            "land_invertebrate" => "delicately tidied",
            _ => "cleaned"
        };

        await AddJournalEntryAsync(db, petId, "groom", $"{Username} {groomVerb} {petName}. Squeaky clean! 🛁");

        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🛁  Groomed {petName}!",
            $"You {groomVerb} **{petName}**! They're squeaky clean! ✨\n\n" +
            $"🧼 Hygiene: {PetHelper.StatBar(hygiene)} **{hygiene}/100**\n" +
            $"😊 Happiness: {PetHelper.StatBar(happy)} **{happy}/100**" +
            (levelUp is not null ? $"\n\n{levelUp}" : ""),
            ColourSuccess, footer: $"{Username} • +{PetHelper.XpGroom} XP", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Plays with the active pet (unless hibernating) to boost happiness at the cost of energy/hunger, replying with a species-specific activity and journal entry.</summary>
    [SlashCommand("play", "Play with your active pet!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePlayWithAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        if (row.IsHibernating)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first to wake them up.");
            return;
        }

        if (row.LastPlayed is { } lastPlayed)
        {
            var remaining = lastPlayed.AddMinutes(PetHelper.PlayCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet is tired! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int petId = row.PetId;
        int happiness = Math.Min(100, row.Happiness + 25);
        int energy = Math.Max(0, row.Energy - 15);
        int hunger = Math.Max(0, row.Hunger - 10);
        int hygiene = row.Hygiene;
        int oldXp = row.Xp;
        int newXp = oldXp + PetHelper.XpPlay;
        string petName = row.Name;
        string species = row.Species;

        ApplyPetStats(row, hunger, happiness, energy, hygiene, newXp,
            isHibernating: PetHelper.ShouldHibernate(hunger, happiness, energy), lastPlayed: DateTime.UtcNow);
        await db.SaveChangesAsync();

        string[] activities = species.ToLower() switch
        {
            "cat" =>
            [
                "chased a laser pointer",
                "played with a ball of yarn",
                "pounced on a toy mouse",
                "knocked every single item off the shelf one by one while maintaining direct eye contact",
                "attacked a paper bag for six minutes and then sat inside it",
                "ambushed their own tail from behind a cushion and acted surprised when they caught it",
                "sprinted from room to room at 3am for no reason, then sat down and pretended it was normal",
            ],
            "dog" =>
            [
                "fetched the ball 12 times",
                "zoomed around the yard",
                "played tug-of-war",
                "launched into full zoomies mode, lapped the garden eight times, then collapsed with complete satisfaction",
                "played a very intense game of 'find the hidden treat' and found all six in record time",
                "carried their favourite toy around the house to show everyone how great it is",
                "invented a new game involving a sock and three pillows that had no rules but tremendous energy",
            ],
            "horse" =>
            [
                "galloped through a field",
                "jumped a fence gracefully",
                "trotted around the paddock",
                "cleared every jump in the course and looked absolutely majestic doing it",
                "played chase with a very confused dog who had no idea what they'd started",
                "cantered side-by-side with the wind in the most cinematic way possible",
                "invented a game involving rolling a ball with their nose and clearly won",
            ],
            "bird" =>
            [
                "learned a new song",
                "played with a mirror",
                "flew acrobatic loops",
                "mastered a new phrase and deployed it immediately at an inappropriate moment",
                "raided the toy basket and rearranged everything to their exact specifications",
                "performed a full aerial display through every room of the house",
                "played 'drop the toy and make you pick it up' for twenty-five consecutive minutes",
            ],
            "dinosaur" =>
            [
                "stomped around the yard looking magnificently prehistoric while every bird within a mile radius fled",
                "chased a ball, caught it, sat on it triumphantly, looked confused, and declared victory anyway",
                "roared at a squirrel until it moved to a different postcode",
                "played fetch — brought back the ball, plus a full branch, three rocks, and somehow a garden bench",
                "dug an archaeology-worthy pit in the garden, discovered a bone, and presented it with enormous ceremony",
                "attempted hide-and-seek behind a tree roughly one-fifth their width. Played four rounds. Loved every second.",
                "splashed through every puddle in a two-mile radius with maximum prehistoric enthusiasm",
            ],
            "bunny" =>
            [
                "binkied non-stop for five minutes",
                "zoomed laps around the living room",
                "tossed their toy in the air repeatedly",
                "executed the zoomies at such speed they briefly became a blur in three separate rooms",
                "dug an elaborate network through their bedding, declared it finished, and remodelled it immediately",
                "stole a sock, ran eight circuits of the living room with it, and deposited it somewhere unknowable",
                "binkied so high off a sofa cushion that everyone in the room gasped",
            ],
            "fish" =>
            [
                "weaved through every decoration at maximum speed for the pure joy of it",
                "played bubble ring catch — blowing rings and darting through each one before it dissolved",
                "investigated every corner of the tank as if seeing it for the very first time",
                "performed a synchronised routine with their own reflection that neither could tell apart",
                "chased a floating leaf for twenty minutes with the energy of someone who has no responsibilities",
                "blew the most elaborate bubble nest in recorded history and stood guard over it proudly",
                "rearranged all the gravel with their nose into a pattern that felt personally meaningful",
            ],
            "shark" =>
            [
                "played 'torpedo' — shot across the tank at full speed and somehow stopped precisely at the wall",
                "bumped a floating toy around with their snout until it was exactly where they wanted it",
                "played chase with a smaller fish who was somehow completely unafraid and equally enthusiastic",
                "surfed the current from the filter for fifteen minutes with visible delight",
                "circled the tank in increasingly rapid loops until the water was visibly spinning",
                "worked out an elaborate routine with a suspended ball that took thirty minutes to perfect",
                "performed three dramatic breach attempts in sequence, each getting slightly more airborne",
            ],
            "wolf" =>
            [
                "played a ferocious but entirely cooperative game of tug-of-war and only won when permitted",
                "invented a tracking game, hid a toy in the next room, then acted amazed when they found it",
                "raced through the woods at full sprint and returned carrying a stick of enormous personal significance",
                "played an extended session of 'chase me', covering about four miles total, grinning the whole time",
                "performed a howl-along session that lasted eleven minutes and involved genuinely excellent harmonies",
                "dug a very deliberate hole, buried a toy, re-found it, and considered the exercise complete",
                "played an intense staring contest that lasted three full minutes before someone blinked",
            ],
            "lizard" =>
            [
                "sprinted obstacle courses across the furniture at speeds that defied their apparent calm",
                "changed colour rapidly through a full spectrum just because it was an option",
                "played 'disappear into the basking spot' so convincingly you spent five minutes looking for them",
                "solved a treat puzzle box on the first attempt and looked moderately unimpressed by the challenge",
                "climbed everything climbable in sequence and ranked each surface by texture preference",
                "chased a feather on a string with far more competitive energy than expected",
                "invented a game involving pushing a small rock around their space with their nose",
            ],
            "otter" =>
            [
                "played a twenty-minute game of catch using their own belly as a table",
                "launched down every available slide surface and scored each run with visible internal ratings",
                "assembled an intricate puzzle of pebbles, solved it, and then scattered it for later",
                "played a splashing game that soaked everything in a two-metre radius and showed no remorse",
                "rolled across the floor collecting loose items until they were carrying four things at once",
                "invented a water polo variant with only one player and won convincingly",
                "floated on their back humming while balancing three objects on their chest simultaneously",
            ],
            "bear" =>
            [
                "lumbered through a play area scattering everything in the most affectionate way possible",
                "invented a wrestling game that involved mostly sitting on things",
                "chased a ball with the energy of someone much smaller and caught it every single time",
                "climbed a tree for absolutely no reason and sat up there for a while, just thinking",
                "played 'find the snack hidden in the pile of leaves' and found all twelve within a minute",
                "engaged in a tug-of-war with a rope and it was unclear whether they were actually trying",
                "splashed through a stream in an extended game of 'follow the current' that ended several miles away",
            ],
            "insect" =>
            [
                "performed an aerial display so complex it looked choreographed — nobody can prove it wasn't",
                "built an intricate obstacle course from twigs and then ran it at record speed",
                "played hide-and-seek so effectively you had to genuinely search for twenty minutes",
                "constructed the most elaborate web arrangement you've ever seen, solely as art",
                "chased a beam of light around the ceiling for an hour with undiminished enthusiasm",
                "played dead so convincingly for a solid minute that you got worried, then they winked",
                "discovered a spinning top, investigated it thoroughly, and achieved a higher RPM than it started with",
            ],
            "ocean_invertebrate" =>
            [
                "rearranged the entire tank decor into a configuration that made considerably more sense",
                "opened and closed a hinged toy box sixteen times to work out how it functioned",
                "played an eight-armed game of catch that nobody else could quite follow",
                "camouflaged perfectly and waited for you to notice them. You found them eventually. They were smug.",
                "launched across the tank in a jet-propulsion race that peaked at an unexpected velocity",
                "squeezed through a hole that appeared to be two sizes too small and emerged looking pleased",
                "invented a stacking game with shells that demonstrated a grasp of physics that felt pointed",
            ],
            "land_invertebrate" =>
            [
                "constructed an elaborate silk maze and then navigated it faster than should be possible",
                "solved a puzzle box with eight legs working in independent specialised roles",
                "played the world's most focused game of 'follow the dot' across three surfaces and a ceiling",
                "built a web across the entire corner of their enclosure in thirty minutes flat",
                "stalked a toy mouse with such commitment they forgot it wasn't real until it didn't react",
                "investigated a cardboard tube from every angle until they fully understood its structure",
                "spent two hours rearranging substrate into a pattern that looked architectural",
            ],
            _ => ["had a great time"]
        };

        string activity = activities[Random.Shared.Next(activities.Length)];
        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await AddJournalEntryAsync(db, petId, "play", $"{petName} {activity}! 🎮");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🎮  Playtime with {petName}!",
            $"**{petName}** {activity}! 🎉\n\n" +
            $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**\n" +
            $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**\n" +
            $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**" +
            (levelUp is not null ? $"\n\n{levelUp}" : ""),
            ColourPet, footer: $"{Username} • +{PetHelper.XpPlay} XP", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Puts the active pet to sleep to restore energy — only available once energy drops below the sleep threshold.</summary>
    [SlashCommand("sleep", "Put your pet to sleep to restore their energy.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetSleepAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int currentEnergy = row.Energy;
        const int sleepThreshold = 50;

        if (currentEnergy >= sleepThreshold)
        {
            await ErrorAsync(
                $"**{row.Name}** isn't tired yet! Energy is at **{currentEnergy}/100**.\n" +
                $"Sleep is only available below **{sleepThreshold} energy**.");
            return;
        }

        int petId = row.PetId;
        int energy = Math.Min(100, currentEnergy + 50);
        int hunger = row.Hunger;
        int happy = row.Happiness;
        int hygiene = row.Hygiene;
        int xp = row.Xp;
        string petName = row.Name;

        ApplyPetStats(row, hunger, happy, energy, hygiene, xp, isHibernating: false, lastSlept: DateTime.UtcNow);
        await db.SaveChangesAsync();

        await AddJournalEntryAsync(db, petId, "sleep", $"{petName} took a nap and restored some energy. 💤");

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"💤  {petName} is napping!",
            $"**{petName}** curled up for a nap. 😴\n\n" +
            $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Gives the active pet a low-cooldown, XP-free happiness nudge with a species-specific flavor reaction.</summary>
    [SlashCommand("hug", "Give your pet a warm hug! Small happiness boost, no XP.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetHugAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        // 1 minute cooldown — intentionally short, pure flavour
        if (row.LastPetted is { } lastPetted)
        {
            var remaining = lastPetted.AddMinutes(1) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet needs a breather! Try again in **{remaining.Seconds}s**.");
                return;
            }
        }

        int petId = row.PetId;
        int happiness = Math.Min(100, row.Happiness + 5);
        int hunger = row.Hunger;
        int energy = row.Energy;
        int hygiene = row.Hygiene;
        int xp = row.Xp;
        string petName = row.Name;
        string species = row.Species;

        ApplyPetStats(row, hunger, happiness, energy, hygiene, xp,
            isHibernating: PetHelper.ShouldHibernate(hunger, happiness, energy), lastPetted: DateTime.UtcNow);
        await db.SaveChangesAsync();

        // Log to journal
        await AddJournalEntryAsync(db, petId, "hug", $"{Username} gave {petName} a hug! 🤗");

        string[] hugReactions = species.ToLower() switch
        {
            "cat" =>
            [
                "tolerates it with dignity 😸",
                "leans into it just a little 🐱",
                "pretends not to enjoy it but their purr says otherwise 😺",
                "allows exactly three seconds of hugging, then bites once, then purrs for five minutes 🐱",
                "goes completely limp in your arms and makes you carry all of their opinions 😺",
                "lets out one tiny chirp of protest and then relaxes completely 😸",
                "tucks their head under your chin and begins purring at industrial volume 🐱",
            ],
            "dog" =>
            [
                "goes absolutely wild with joy 🐶",
                "licks your entire face 🐕",
                "wiggles so hard they nearly fall over 🐾",
                "wraps two paws around your arm and refuses to be the first to let go 🐶",
                "makes direct eye contact, tail a blur, and leans their whole weight into the hug 🐕",
                "lets out a long, whimpery groan of pure happiness right next to your ear 🐾",
                "gives your face one enormous lick from chin to forehead and acts like that was the hug 🐶",
            ],
            "horse" =>
            [
                "rests their head on your shoulder 🐴",
                "lets out a soft, warm breath 🐎",
                "nuzzles you gently in return 🐴",
                "wraps their neck around you and holds the pose like a professional portrait 🐎",
                "leans into you with the gentle, inexorable weight of an affectionate mountain 🐴",
                "blows a long breath through their lips and makes a sound of profound contentment 🐎",
                "bumps their nose against your cheek three times in a row, which is definitely counting as a hug 🐴",
            ],
            "bird" =>
            [
                "puffs up into a happy little ball 🐦",
                "clicks their beak contentedly 🦜",
                "buries their head in your hair 🐦",
                "grips your finger with both feet and refuses to let go for a full minute 🦜",
                "preens your sleeve very seriously as their form of reciprocation 🐦",
                "makes a sound like a tiny kettle and melts against your chest 🦜",
                "tucks their beak under their wing while still in your arms, perfectly content 🐦",
            ],
            "dinosaur" =>
            [
                "produces a deep, resonant rumble that rattles the windows — affectionately 🦕",
                "nudges you with their snout hard enough to slide you sideways. It is love. 🐉",
                "sits very still, eyes half-closed, radiating contentment at geological scale 🦖",
                "wraps their neck around you as close to a hug as their anatomy allows. It counts. 🦕",
                "lets out a long, slow exhale through their nostrils directly into your face. Maximum intimacy. 🦖",
                "stamps one enormous foot in place — their version of happy bouncing — and regards you warmly 🐉",
                "tilts their head and considers you with one enormous eye, then huffs softly. That's a good hug. 🦕",
            ],
            "bunny" =>
            [
                "licks your nose 🐰",
                "does a tiny binky from happiness 🐇",
                "flops over dramatically onto your lap 🐰",
                "thumps once in mild protest, then melts into the hug anyway 🐇",
                "vibrates at a frequency that means maximum contentment 🐰",
                "grooms your hand very thoroughly before permitting further hugging 🐇",
                "stretches completely flat across your arms and closes their eyes with absolute peace 🐰",
            ],
            "fish" =>
            [
                "swims frantic happy circles while you press your hand to the glass 🐟",
                "hovers right in front of you, fins fanning gently, making the most of the moment 🐠",
                "bumps the glass repeatedly with their nose — kisses, probably 🐡",
                "flares every fin to full display and holds it for five full seconds 🐟",
                "performs three tight circles exactly at your hand level, clearly reciprocating 🐠",
                "rises to the surface and blows a stream of tiny bubbles in your direction 🐡",
                "presses their whole side against the glass right where your hand is, and holds still 🐟",
            ],
            "shark" =>
            [
                "glides into the hug zone and holds completely still, which from a shark is meaningful 🦈",
                "bumps against your hands with surprising gentleness and doesn't move away 🦈",
                "opens their mouth slightly and closes it — this is a shark smile, accept it 🦈",
                "rolls half-sideways, which is the shark equivalent of going belly-up for pets 🦈",
                "circles so close the water pressure shifts noticeably — an embrace, in their way 🦈",
                "rests their snout against your hands and exhales bubbles slowly 🦈",
                "pauses mid-circuit, holds eye contact, and hovers perfectly still for ten seconds 🦈",
            ],
            "wolf" =>
            [
                "allows the hug and emits one soft whine to clarify this doesn't mean anything 🐺",
                "presses their whole face into the crook of your neck and goes still 🐺",
                "permits a single cheek-scritch in exchange for the indignity of being hugged 🐺",
                "leans their full bodyweight against you and pretends it's accidental 🐺",
                "accepts the hug with solemn dignity and then licks your hand once — deal sealed 🐺",
                "rests their chin on your shoulder and exhales a long, low breath through their nose 🐺",
                "makes extremely direct eye contact from within the hug, expressing complex emotions 🐺",
            ],
            "lizard" =>
            [
                "goes completely still and absorbs the warmth with eyes closed 🦎",
                "puffs their throat out proudly and accepts this as tribute 🦎",
                "climbs further up your arm to maximise contact with the warm surface 🦎",
                "bobs their head twice in acknowledgement, which is significant for a lizard 🦎",
                "flicks their tongue to taste the air and apparently approves of what they find 🦎",
                "changes to a subtly warmer colour tone — their version of blushing 🦎",
                "pushes one small foot against your palm and holds the pressure 🦎",
            ],
            "otter" =>
            [
                "wraps both arms around one of your fingers and squeezes with surprising strength 🦦",
                "immediately starts grooming your sleeve as payment for the hug 🦦",
                "makes the highest-pitched squeak you have ever heard from a non-toy 🦦",
                "holds your hand with both paws and rocks slightly from side to side 🦦",
                "buries their face in your sleeve and vibrates rapidly 🦦",
                "performs one immediate full-body wiggle, then settles into the hug contentedly 🦦",
                "offers you their best pebble in exchange for continued hugging 🦦",
            ],
            "bear" =>
            [
                "leans so heavily into the hug that you need to brace your feet 🐻",
                "makes a low, resonant chuff that you feel as much as hear 🐻",
                "rests their chin on your head and exhales completely 🐻",
                "accepts the hug with enormous dignity and places one paw on your shoulder in return 🐻",
                "nuzzles your cheek so gently it's hard to believe how large they are 🐻",
                "lets out a single, slow groan of profound relaxation 🐻",
                "pulls you in slightly closer with both arms — you are being bear-hugged back 🐻",
            ],
            "insect" =>
            [
                "walks slowly in a tight circle on your palm — their version of a squeeze 🦋",
                "fans both wings open to their full span and holds the display in your direction 🐛",
                "vibrates their wings at a frequency that lands somewhere between purring and applause 🦋",
                "taps their front feet against your hand rapidly, which means something warm 🐛",
                "glows softly and steadily for twelve seconds before dimming back down 🦋",
                "presses their antennae gently against your skin and goes still 🐛",
                "extends one foreleg and places it against your fingertip with great ceremony 🦋",
            ],
            "ocean_invertebrate" =>
            [
                "wraps two arms around your finger and pulses with unmistakable affection 🐙",
                "turns a deep, warm amber — their colour for contentment — and holds it 🐙",
                "jets gently into your hands and makes no move to leave for a while 🐙",
                "reaches out three arms and makes gentle contact with your palm simultaneously 🐙",
                "changes to a soft rose pattern and holds it for the entire interaction 🐙",
                "wraps around your wrist loosely and breathes slowly, which is as relaxed as they get 🐙",
                "blinks both large eyes very slowly in your direction — the highest marine compliment 🐙",
            ],
            "land_invertebrate" =>
            [
                "sits very still in your hands and vibrates all eight legs gently 🕷️",
                "raises their front legs slightly in a posture that reads as grateful 🦂",
                "lowers their abdomen slowly — a bow, in their tradition 🕷️",
                "walks once around the perimeter of your palm and settles in the centre 🦂",
                "produces a single silk thread, attaches it to your thumb, and sits with it 🕷️",
                "turns to face you directly and holds perfectly still — this is meaningful 🦂",
                "presses two front legs gently against your wrist for a long, comfortable moment 🕷️",
            ],
            _ => ["enjoys the affection 🐾"]
        };

        string reaction = hugReactions[Random.Shared.Next(hugReactions.Length)];

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🤗  Hugged {petName}!",
            $"**{petName}** {reaction}\n\n😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**",
            ColourPet, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Shows the active pet's most recent journal entries (feed/play/battle/etc. history) as a timestamped log.</summary>
    [SlashCommand("journal", "View the recent activity log for your active pet.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetJournalAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = row.PetId;
        string petName = row.Name;
        string species = row.Species;
        int level = PetHelper.LevelFromXp(row.Xp);
        bool evolved = level >= 50;

        var entries = await db.PetJournals.AsNoTracking()
            .Where(j => j.PetId == petId).OrderByDescending(j => j.JournalId).Take(20).ToListAsync();

        string emoji = PetHelper.PetEmoji(species, 80, 80, false, evolved);

        if (entries.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"📓  {petName}'s Journal", "No journal entries yet — go interact with your pet!",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();

        foreach (var entry in entries)
        {
            string eventEmoji = PetHelper.JournalEventEmoji(entry.Event);
            string timestamp = $"<t:{new DateTimeOffset(entry.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()}:R>";

            sb.AppendLine($"{eventEmoji} {entry.Details} {timestamp}");
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"📓  {emoji} {petName}'s Journal", sb.ToString(),
            ColourInfo, footer: $"Last 20 entries • {Username}", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Makes the active pet perform one of 4 level-gated tricks.</summary>
    [SlashCommand("trick", "Make your pet perform a trick!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleTrickAsync(
        [Choice("Trick 1 (Lv.5)",  "1"),
         Choice("Trick 2 (Lv.20)", "2"),
         Choice("Trick 3 (Lv.50)", "3"),
         Choice("Trick 4 (Lv.75)", "4")]
        string slot = "1")
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(row.Xp);

        if (slot == "1" && level < 5) { await ErrorAsync($"Trick slot 1 unlocks at **level 5**! Your pet is level {level}."); return; }
        if (slot == "2" && level < 20) { await ErrorAsync($"Trick slot 2 unlocks at **level 20**! Your pet is level {level}."); return; }
        if (slot == "3" && level < 50) { await ErrorAsync($"Trick slot 3 unlocks at **level 50**! Your pet is level {level}."); return; }
        if (slot == "4" && level < 75) { await ErrorAsync($"Trick slot 4 unlocks at **level 75**! Your pet is level {level}."); return; }

        string petName = row.Name;
        string species = row.Species;
        string trick = PetHelper.PerformTrick(species, int.Parse(slot));

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🎪  {petName} performs a trick!", $"**{petName}** {trick}",
            ColourPet, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Equips an accessory (hat or collar/outfit) to the active pet's level-gated accessory slot.</summary>
    [SlashCommand("accessory", "Equip an accessory to your active pet. (Unlocks at level 10)")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleAccessoryAsync(
        [Choice("Slot 1 — Hat",           "slot1"),
         Choice("Slot 2 — Collar/Outfit", "slot2")]
        string slot,
        [MinLength(1), MaxLength(32)] string item)
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(row.Xp);

        if (slot == "slot1" && level < 10) { await ErrorAsync("Accessory slot 1 unlocks at **level 10**!"); return; }
        if (slot == "slot2" && level < 15) { await ErrorAsync("Accessory slot 2 unlocks at **level 15**!"); return; }

        if (slot == "slot1") row.Accessory1 = item.Trim();
        else row.Accessory2 = item.Trim();
        await db.SaveChangesAsync();

        string petName = row.Name;

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "👗  Accessory Equipped!", $"**{petName}** is now wearing **{item.Trim()}**! Looking good! ✨",
            ColourSuccess, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Switches which of the user's pets is currently active.</summary>
    [SlashCommand("setactive", "Switch which pet is currently active.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleSetActiveAsync([MinValue(1)] int petId)
    {
        await DeferAsync();

        // NOTE (flagged, not fixed): source (SetActivePet) unconditionally deactivates ALL of
        // the caller's pets FIRST, then tries to activate the given petId scoped to UserID, and
        // only afterward checks whether that pet actually belongs to them (via GetPetByID). If
        // petId is invalid or belongs to someone else, step 2 matches nothing — the user is left
        // with NO active pet at all, silently, before the "Pet not found" error is even shown.
        // Pre-existing in the source proc's design; replicated exactly here rather than
        // reordered to validate-before-mutate.
        await db.Pets.Where(p => p.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        await db.Pets.Where(p => p.PetId == petId && p.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, true));

        var pet = await db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.PetId == petId && p.UserId == UserId);

        if (pet is null) { await ErrorAsync("Pet not found."); return; }

        string name = pet.Name;
        string species = pet.Species;
        string emoji = PetHelper.PetEmoji(species, 50, 50, false, false);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{emoji}  Active pet changed!", $"**{name}** is now your active pet.",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Renames the active pet.</summary>
    [SlashCommand("rename", "Rename your active pet.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleRenameAsync([MinLength(1), MaxLength(32)] string newName)
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        string oldName = row.Name;
        row.Name = newName.Trim();
        await db.SaveChangesAsync();

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "✏️  Pet Renamed!", $"**{oldName}** is now known as **{newName.Trim()}**!",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Prompts for confirmation before permanently releasing one of the user's pets (deletion itself is handled by <see cref="PetComponentHandlers.OnReleaseConfirmAsync"/>).</summary>
    [SlashCommand("release", "Release one of your pets. This cannot be undone!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleReleaseAsync([MinValue(1)] int petId)
    {
        await DeferAsync();

        var pet = await db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.PetId == petId && p.UserId == UserId);

        if (pet is null) { await ErrorAsync("Pet not found or doesn't belong to you."); return; }

        string name = pet.Name;
        string species = pet.Species;
        int level = PetHelper.LevelFromXp(pet.Xp);
        string emoji = PetHelper.PetEmoji(species, 80, 80, false, level >= 50);

        var components = new ComponentBuilder()
            .WithButton("Yes, release them", $"release:confirm:{petId}", ButtonStyle.Danger, new Emoji("🌿"))
            .WithButton("Cancel", "release:cancel", ButtonStyle.Secondary, new Emoji("✖️"))
            .Build();

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"⚠️  Release {name}?",
            $"{emoji} **{name}** is a level **{level}** {species}.\n\n" +
            "Releasing a pet is **permanent** and cannot be undone.\nAre you sure?",
            ColourError, footer: Username, footerIconUrl: AvatarUrl).Build(), components: components);
    }

    // release:confirm and release:cancel are in PetComponentHandlers below (outside [Group])


    /// <summary>Shows the server's top pets ranked by level, with medal emoji for the top 3.</summary>
    [SlashCommand("leaderboard", "Show the top pets in this server by level.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleLeaderboardAsync()
    {
        await DeferAsync();

        // Source (GetPetLeaderboard) LEFT JOINs Users on UserID + TRY_CAST(@ServerID AS BIGINT)
        // — tolerant of a non-numeric ServerID (yields no match rather than erroring). This
        // command is guild-only so ServerId is always numeric in practice; the TryParse below
        // preserves the same tolerance regardless.
        long? serverIdLong = long.TryParse(ServerId, out long sid) ? sid : null;

        var results = await (
            from p in db.Pets.AsNoTracking()
            where p.ServerId == ServerId
            orderby p.Xp descending
            select new
            {
                p.Name,
                p.Species,
                p.Xp,
                Username = db.Users.Where(u => u.UserId == p.UserId && u.ServerUid == serverIdLong)
                    .Select(u => u.Username).FirstOrDefault()
            }
        ).Take(10).ToListAsync();

        if (results.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Leaderboard", "No pets found in this server yet!", Username).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        int rank = 0;
        string[] medals = ["🥇", "🥈", "🥉"];

        foreach (var row in results)
        {
            string medal = rank < 3 ? medals[rank] : $"**{rank + 1}.**";
            string petName = row.Name;
            string petSpecies = row.Species;
            int xp = row.Xp;
            int level = PetHelper.LevelFromXp(xp);
            string owner = row.Username ?? "";
            bool evolved = level >= 50;
            string crown = level >= 100 ? " 👑" : "";
            string evolvedStr = evolved ? $" *({PetHelper.EvolvedName(petSpecies)})*" : "";

            sb.AppendLine($"{medal} **{petName}**{evolvedStr}{crown} — Lv.{level} | Owner: {owner}");
            rank++;
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🏆  Pet Leaderboard — {Context.Guild.Name}", sb.ToString(), ColourVeteran).Build());
    }


    /// <summary>Lists food items available at the active pet's current level.</summary>
    [SlashCommand("foodlist", "Show all available food items for your pet.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleFoodListAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(row.Xp);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "🍽️  Available Food", PetHelper.ListFoods(level),
            ColourPet, footer: $"Your pet is level {level}", footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Sends the active pet exploring for a level-scaled duration, or — if already out — claims the reward once the return time has passed (applying any active XP/explore boost).</summary>
    [SlashCommand("explore", "Send your pet on an adventure! Come back later to collect the reward.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleExploreAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        if (row.IsHibernating)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first before sending them exploring.");
            return;
        }

        string petName = row.Name;
        string species = row.Species;
        int petId = row.PetId;
        int oldXp = row.Xp;
        int level = PetHelper.LevelFromXp(oldXp);

        // Check if already exploring — source (GetPetExplore) re-queried the Pet row for its own
        // ExploreReturnsAt/ExploreRewardKey columns; already have them on the tracked entity.
        if (row.ExploreReturnsAt is { } returnsAt)
        {
            // Ready to claim
            if (DateTime.UtcNow >= returnsAt)
            {
                string rewardKey = row.ExploreRewardKey!;
                var reward = PetHelper.ExploreRewards.First(r => r.key == rewardKey);

                // xp_boost: 2× XP for the duration
                bool hasXpBoost = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "xp_boost");
                int bonusXp = hasXpBoost ? reward.xp : 0;
                int newXp = oldXp + reward.xp + bonusXp;
                int hunger = Math.Max(0, row.Hunger - reward.hungerCost);
                int happiness = Math.Min(100, row.Happiness + reward.happyBonus);
                int energy = Math.Max(0, row.Energy - reward.energyCost);
                int hygiene = row.Hygiene;

                ApplyPetStats(row, hunger, happiness, energy, hygiene, newXp,
                    isHibernating: PetHelper.ShouldHibernate(hunger, happiness, energy));
                row.ExploreReturnsAt = null;
                row.ExploreRewardKey = null;
                await db.SaveChangesAsync();

                // Journal entry
                string rewardDesc = PetHelper.ExploreRewardDescription(rewardKey, species, reward.description);
                await AddJournalEntryAsync(db, petId, "explore",
                    $"{petName} returned from an adventure and found {reward.emoji} {rewardDesc} (+{reward.xp} XP)!");

                var (_, levelUp) = CheckLevelUp(oldXp, newXp);

                string adventure = PetHelper.ExploreNarrative(species, rewardKey);
                string opener = PetHelper.ExploreReturnOpener(petName);
                string? picUrl = row.PictureUrl;

                var eb = _embed.BuildSimpleEmbed(
                    $"{reward.emoji}  {petName} returned from their adventure!",
                    $"{opener}\n\n" +
                    $"{adventure}\n\n" +
                    $"**Reward:** {reward.emoji} {rewardDesc}\n\n" +
                    (hasXpBoost ? $"✨ **+{reward.xp + bonusXp} XP** *(XP Boost! +{bonusXp} bonus)*\n" : $"✨ **+{reward.xp} XP**\n") +
                    $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**\n" +
                    $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**\n" +
                    $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**" +
                    (levelUp is not null ? $"\n\n{levelUp}" : ""),
                    ColourSuccess, footer: $"{Username} • +{reward.xp} XP", footerIconUrl: AvatarUrl);

                if (picUrl is not null) eb.WithThumbnailUrl(picUrl);

                await FollowupAsync(embed: eb.Build());
                return;
            }

            // Still out exploring
            var remaining = returnsAt - DateTime.UtcNow;
            string timeLeft = remaining.TotalMinutes < 1
                ? $"{remaining.Seconds}s"
                : $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s";

            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"🗺️  {petName} is still exploring!",
                $"**{petName}** is out on an adventure and hasn't returned yet.\n\n" +
                $"⏳ Returns in **{timeLeft}** — come back to collect their reward!",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
            return;
        }

        // Send pet exploring — duration scales slightly with level (30–60 min)
        int durationMinutes = Math.Min(60, 30 + (level / 10) * 5);

        // explore_boost: guarantees a rare+ reward tier
        bool hasExploreBoost = await ShopHelper.HasActiveEffectAsync(db, UserId, ServerId, "explore_boost");
        var rewardPick = hasExploreBoost
            ? PetHelper.PickExploreRewardBoosted(level)
            : PetHelper.PickExploreReward(level);
        if (hasExploreBoost) await ShopHelper.ConsumeActiveEffectAsync(db, UserId, ServerId, "explore_boost");

        var returnsAtNew = DateTime.UtcNow.AddMinutes(durationMinutes);

        row.ExploreReturnsAt = returnsAtNew;
        row.ExploreRewardKey = rewardPick.key;
        await db.SaveChangesAsync();

        await AddJournalEntryAsync(db, petId, "explore_depart",
            $"{petName} set off on an adventure! Returns <t:{new DateTimeOffset(returnsAtNew, TimeSpan.Zero).ToUnixTimeSeconds()}:R>.");

        string departureMsg = PetHelper.ExploreDeparture(species);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🗺️  {petName} set off on an adventure!",
            $"{departureMsg}\n\n" +
            $"⏳ They'll be back in **{durationMinutes} minutes**.\n" +
            $"Use `/explore` again to collect their reward when they return!",
            ColourPet, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Challenges another user's active pet to a power-score battle (rate-limited per user), animating the result across 3 message edits and granting XP to both pets.</summary>
    [SlashCommand("battle", "Challenge another user's active pet to a battle!")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetBattleAsync(IUser opponent)
    {
        await DeferAsync();


        if (opponent.Id == Context.User.Id)
        {
            await ErrorAsync("You can't battle yourself!");
            return;
        }

        if (opponent.IsBot)
        {
            await ErrorAsync("Bots don't have pets to battle!");
            return;
        }

        // Per-user cooldown
        if (_battleCooldowns.TryGetValue(UserId, out var lastBattle))
        {
            var remaining = lastBattle.AddSeconds(BattleCooldownSeconds) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet needs to rest after that battle! Try again in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }
        _battleCooldowns[UserId] = DateTime.UtcNow;

        var (challengerRow, challengerError) = await GetActivePetAsync();
        if (challengerRow is null) { await ErrorAsync(challengerError!); return; }

        if (challengerRow.IsHibernating)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first before battling.");
            return;
        }

        string opponentIdStr = opponent.Id.ToString();
        var opponentRow = await db.Pets.FirstOrDefaultAsync(p => p.UserId == opponentIdStr && p.IsActive);

        if (opponentRow is null)
        {
            await ErrorAsync($"**{opponent.Username}** doesn't have an active pet!");
            return;
        }

        if (opponentRow.IsHibernating)
        {
            await ErrorAsync($"**{opponent.Username}**'s pet is hibernating and can't battle right now!");
            return;
        }


        string challengerName = challengerRow.Name;
        string challengerSpecies = challengerRow.Species;
        int challengerXp = challengerRow.Xp;
        int challengerLevel = PetHelper.LevelFromXp(challengerXp);
        int challengerHunger = challengerRow.Hunger;
        int challengerHappy = challengerRow.Happiness;
        int challengerEnergy = challengerRow.Energy;

        string opponentName = opponentRow.Name;
        string opponentSpecies = opponentRow.Species;
        int opponentXp = opponentRow.Xp;
        int opponentLevel = PetHelper.LevelFromXp(opponentXp);
        int opponentHunger = opponentRow.Hunger;
        int opponentHappy = opponentRow.Happiness;
        int opponentEnergy = opponentRow.Energy;

        // Power score = weighted sum of level, stats, and a luck roll

        int challengerPower = PetHelper.BattlePower(
            challengerLevel, challengerHunger, challengerHappy, challengerEnergy);
        int opponentPower = PetHelper.BattlePower(
            opponentLevel, opponentHunger, opponentHappy, opponentEnergy);

        bool challengerWon = challengerPower >= opponentPower;
        bool draw = challengerPower == opponentPower;


        int winnerXpGain = 20 + (Math.Abs(challengerPower - opponentPower) / 2);
        int loserXpGain = 8;
        int energyCost = 20;
        int hungerCost = 10;

        // Apply costs to both pets
        void ApplyBattleCost(PetEntity pet, int oldXp, int xpGain, int hunger, int happy, int energy, int hygiene)
        {
            int newXp = oldXp + xpGain;
            int newHunger = Math.Max(0, hunger - hungerCost);
            int newEnergy = Math.Max(0, energy - energyCost);

            ApplyPetStats(pet, newHunger, happy, newEnergy, hygiene, newXp,
                isHibernating: PetHelper.ShouldHibernate(newHunger, happy, newEnergy));
        }

        int cXpGain = draw ? loserXpGain : challengerWon ? winnerXpGain : loserXpGain;
        int oXpGain = draw ? loserXpGain : challengerWon ? loserXpGain : winnerXpGain;

        ApplyBattleCost(challengerRow, challengerXp, cXpGain,
            challengerHunger, challengerHappy, challengerEnergy, challengerRow.Hygiene);

        ApplyBattleCost(opponentRow, opponentXp, oXpGain,
            opponentHunger, opponentHappy, opponentEnergy, opponentRow.Hygiene);

        // Both pets' battle costs commit together — same reasoning as the other Pet-care
        // commands (see ApplyPetStats): source called two independent auto-committing
        // UpdatePetStats proc calls, bundled here into one transaction.
        await db.SaveChangesAsync();


        var (_, cLevelUp) = CheckLevelUp(challengerXp, challengerXp + cXpGain);
        var (_, oLevelUp) = CheckLevelUp(opponentXp, opponentXp + oXpGain);


        string challengerEmoji = PetHelper.PetEmoji(challengerSpecies, challengerHappy, challengerHunger, false, challengerLevel >= 50);
        string opponentEmoji = PetHelper.PetEmoji(opponentSpecies, opponentHappy, opponentHunger, false, opponentLevel >= 50);
        string? challengerPic = challengerRow.PictureUrl;
        string? opponentPic = opponentRow.PictureUrl;

        string resultTitle = draw
            ? $"🤝  Draw! {challengerName} vs {opponentName}"
            : challengerWon
                ? $"🏆  {challengerName} wins!"
                : $"🏆  {opponentName} wins!";

        Color resultColour = draw ? Color.Blue
            : challengerWon ? ColourSuccess : ColourError;

        string winnerMention = challengerWon ? Context.User.Mention : opponent.Mention;
        string resultLine = draw
            ? "Both pets fought valiantly — it's a draw! 🤝"
            : $"{winnerMention}'s pet wins the battle! 🎉";

        // Generate 3 individual rounds for animation
        var rounds = PetHelper.GenerateBattleRounds(
            challengerName, challengerSpecies, challengerPower,
            opponentName, opponentSpecies, opponentPower,
            draw);

        // Emoji labels — stripped from title if picture is set
        string cLabel = challengerPic is null ? $"{challengerEmoji} {challengerName}" : challengerName;
        string oLabel = opponentPic is null ? $"{opponentEmoji} {opponentName}" : opponentName;

        EmbedBuilder BuildBattleEmbed(int roundsShown)
        {
            // Use picture if available — winner's pic on final frame, challenger's pic during battle
            string? thumbUrl = roundsShown < 3
                ? (challengerPic ?? opponentPic)
                : (challengerWon ? challengerPic : opponentPic)
                  ?? (challengerWon ? opponentPic : challengerPic);

            var eb = new EmbedBuilder()
                .WithTitle(roundsShown < 3
                    ? $"⚔️  {cLabel} vs {oLabel}"
                    : $"⚔️  {resultTitle}")
                .WithColor(roundsShown < 3 ? ColourInfo : resultColour)
                .AddField(
                    $"{cLabel} (Lv.{challengerLevel})",
                    $"Power: **{challengerPower}**{(roundsShown == 3 ? $" | +{cXpGain} XP" : "")}", inline: true)
                .AddField("vs", "⚔️", inline: true)
                .AddField(
                    $"{oLabel} (Lv.{opponentLevel})",
                    $"Power: **{opponentPower}**{(roundsShown == 3 ? $" | +{oXpGain} XP" : "")}", inline: true);

            if (thumbUrl is not null) eb.WithThumbnailUrl(thumbUrl);

            string log = string.Join("\n", rounds.Take(roundsShown));
            eb.AddField("Battle Log", log, inline: false);

            if (roundsShown == 3)
            {
                eb.WithDescription(resultLine);
                if (cLevelUp is not null) eb.AddField($"🎉 {challengerName}", cLevelUp, inline: false);
                if (oLevelUp is not null) eb.AddField($"🎉 {opponentName}", oLevelUp, inline: false);
            }
            else
            {
                eb.WithDescription("*Battle in progress…*");
            }

            eb.WithFooter($"{Username} challenged {opponent.Username}").WithCurrentTimestamp();
            return eb;
        }


        // Pre-battle tease
        var preBattleEmbed = _embed.BuildSimpleEmbed(
            $"⚔️  {cLabel} vs {oLabel}", "*Two pets step onto the field…*", ColourInfo,
            footer: $"{Username} challenged {opponent.Username}",
            fields: [($"{cLabel} (Lv.{challengerLevel})", $"Power: **{challengerPower}**", true),
                     ("vs", "⚔️", true),
                     ($"{oLabel} (Lv.{opponentLevel})", $"Power: **{opponentPower}**", true),
                     ("Battle Log", "*Sizing each other up…*", false)]);

        string? prePic = challengerPic ?? opponentPic;
        if (prePic is not null) preBattleEmbed.WithThumbnailUrl(prePic);

        var battleMsg = await FollowupAsync(embed: preBattleEmbed.Build());
        await Task.Delay(1200);

        await battleMsg.ModifyAsync(m => m.Embed = BuildBattleEmbed(1).Build());
        await Task.Delay(1800);
        await battleMsg.ModifyAsync(m => m.Embed = BuildBattleEmbed(2).Build());
        await Task.Delay(1600);
        await battleMsg.ModifyAsync(m => m.Embed = BuildBattleEmbed(3).Build());
    }


    /// <summary>Sets (or shows) a custom picture for the active pet from an image attachment, used as a thumbnail in its embeds.</summary>
    [SlashCommand("picture", "Upload a photo of your active pet — it will appear in all their embeds.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetPictureAsync(
        IAttachment? picture = null)
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        string petName = row.Name;
        string species = row.Species;
        int level = PetHelper.LevelFromXp(row.Xp);

        // If no attachment — show current picture or instructions
        if (picture is null)
        {
            string? current = row.PictureUrl;
            if (!string.IsNullOrWhiteSpace(current))
            {
                await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                    $"🖼️  {petName}'s Picture",
                    $"**{petName}** already has a picture set.\n\n" +
                    $"Upload a new image with `/pet picture [image]` to replace it, " +
                    $"or use `/pet pictureclear` to remove it.",
                    ColourPet, footer: Username, footerIconUrl: AvatarUrl)
                    .WithImageUrl(current)
                    .Build());
            }
            else
            {
                await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                    $"🖼️  No Picture Set",
                    $"**{petName}** doesn't have a picture yet.\n\n" +
                    $"Attach an image when using `/pet picture` to set one.\n" +
                    $"Supported formats: PNG, JPG, GIF, WEBP",
                    ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
            }
            return;
        }

        // Validate it's an image
        string contentType = picture.ContentType?.ToLower() ?? "";
        if (!contentType.StartsWith("image/"))
        {
            await ErrorAsync("That file doesn't look like an image. Please attach a PNG, JPG, GIF, or WEBP.");
            return;
        }

        // Discord CDN URLs are permanent for attachments posted in messages
        string url = picture.Url;

        row.PictureUrl = url;
        await db.SaveChangesAsync();

        string emoji = PetHelper.PetEmoji(species, 80, 80, false, level >= 50);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🖼️  Picture updated for {petName}!",
            $"{emoji} **{petName}**'s picture has been set.\n" +
            $"It will now appear as a thumbnail in all their embeds.",
            ColourSuccess, footer: Username, footerIconUrl: AvatarUrl).WithThumbnailUrl(url).Build());
    }

    /// <summary>Removes the active pet's custom picture, reverting to emoji display.</summary>
    [SlashCommand("pictureclear", "Remove the photo from your active pet.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetPictureClearAsync()
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        string petName = row.Name;

        row.PictureUrl = null;
        await db.SaveChangesAsync();

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🖼️  Picture cleared",
            $"**{petName}**'s picture has been removed. Emojis will be used instead.",
            ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
    }


    /// <summary>Sets or clears a custom bio (up to 1000 characters) for the active pet.</summary>
    [SlashCommand("bio", "Set a custom bio for your active pet (up to 1000 characters). Leave blank to clear it.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandlePetBioAsync(
        [MaxLength(1000)] string bio = "")
    {
        await DeferAsync();

        var (row, error) = await GetActivePetAsync();
        if (row is null) { await ErrorAsync(error!); return; }

        string petName = row.Name;
        string cleaned = bio.Trim();

        row.Bio = cleaned;
        await db.SaveChangesAsync();

        if (string.IsNullOrEmpty(cleaned))
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"📝  Bio cleared", $"**{petName}**'s bio has been removed.",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build());
        }
        else
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                $"📝  Bio updated!", $"**{petName}**'s bio:\n\n*{cleaned}*",
                ColourSuccess, footer: Username, footerIconUrl: AvatarUrl).Build());
        }
    }


    /// <summary>Lists all valid breeds for a given species.</summary>
    [SlashCommand("breedlist", "Show all available breeds for a species.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task HandleBreedListAsync(
        [Choice("Bear",         "bear"),
         Choice("Bird",         "bird"),
         Choice("Bunny",        "bunny"),
         Choice("Cat",          "cat"),
         Choice("Dinosaur",     "dinosaur"),
         Choice("Dog",          "dog"),
         Choice("Fish",         "fish"),
         Choice("Horse",        "horse"),
         Choice("Insect",       "insect"),
         Choice("Invertebrate (Land)",  "land_invertebrate"),
         Choice("Invertebrate (Ocean)", "ocean_invertebrate"),
         Choice("Lizard",       "lizard"),
         Choice("Otter",        "otter"),
         Choice("Shark",        "shark"),
         Choice("Wolf",         "wolf")]
        string species)
    {
        await DeferAsync();

        if (!PetHelper.Breeds.TryGetValue(species.ToLower(), out var breeds))
        {
            await ErrorAsync($"No breed list found for **{species}**. This is a bug — please report it.");
            return;
        }

        string list = string.Join("\n", breeds.Select(b => $"• {b}"));

        string emoji = PetHelper.PetEmoji(species, 80, 80, false, false);

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{emoji}  {char.ToUpper(species[0])}{species[1..]} Breeds", list,
            ColourPet, footer: "Use /adopt to choose your breed").Build());
    }


    /// <summary>Fetches the user's currently active pet (tracked, so callers can mutate and save it), or an error message if they have none.</summary>
    private async Task<(PetEntity? pet, string? error)> GetActivePetAsync()
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p => p.UserId == UserId && p.IsActive);

        return pet is null
            ? (null, "You don't have an active pet! Use `/adopt` to get one.")
            : (pet, null);
    }

    /// <summary>Posts a standard pet error embed as the interaction followup.</summary>
    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Pet", message, Username).Build());

    /// <summary>
    /// Applies a stat update to a tracked pet entity — mirrors the source UpdatePetStats proc's
    /// signature exactly, including its CASE-WHEN-NOT-NULL pattern: a null "last activity"
    /// timestamp leaves that field untouched rather than clearing it. Does not save; caller
    /// calls SaveChangesAsync (often bundled with a journal entry — see AddJournalEntryAsync).
    /// </summary>
    private static void ApplyPetStats(
        PetEntity pet, int hunger, int happiness, int energy, int hygiene, int xp, bool isHibernating,
        DateTime? lastFed = null, DateTime? lastPetted = null, DateTime? lastGroomed = null,
        DateTime? lastPlayed = null, DateTime? lastSlept = null)
    {
        pet.Hunger = hunger;
        pet.Happiness = happiness;
        pet.Energy = energy;
        pet.Hygiene = hygiene;
        pet.Xp = xp;
        pet.IsHibernating = isHibernating;
        if (lastFed is not null) pet.LastFed = lastFed;
        if (lastPetted is not null) pet.LastPetted = lastPetted;
        if (lastGroomed is not null) pet.LastGroomed = lastGroomed;
        if (lastPlayed is not null) pet.LastPlayed = lastPlayed;
        if (lastSlept is not null) pet.LastSlept = lastSlept;
    }

    /// <summary>
    /// Adds a pet journal entry and prunes to the 50 most recent for that pet — mirrors the
    /// source AddPetJournalEntry proc exactly. Saves the new entry first (the prune query needs
    /// it to already exist in the DB), then prunes as a second save.
    /// </summary>
    private static async Task AddJournalEntryAsync(DiscordbotContext db, int petId, string eventType, string details)
    {
        db.PetJournals.Add(new PetJournal { PetId = petId, Event = eventType, Details = details });
        await db.SaveChangesAsync();

        var oldIds = await db.PetJournals.AsNoTracking()
            .Where(j => j.PetId == petId)
            .OrderByDescending(j => j.JournalId)
            .Skip(50)
            .Select(j => j.JournalId)
            .ToListAsync();
        if (oldIds.Count > 0)
        {
            db.PetJournals.RemoveRange(db.PetJournals.Where(j => oldIds.Contains(j.JournalId)));
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Compares XP before/after a change and, if the pet leveled up, builds the celebratory level-up message (including any unlock text).</summary>
    private static (int newLevel, string? unlockMessage) CheckLevelUp(int oldXp, int newXp)
    {
        int oldLevel = PetHelper.LevelFromXp(oldXp);
        int newLevel = PetHelper.LevelFromXp(newXp);
        if (newLevel <= oldLevel) return (newLevel, null);
        string base_ = $"🎉 **Level Up! Your pet is now level {newLevel}!**";
        string? unlock = PetHelper.LevelUpUnlock(newLevel);
        return (newLevel, unlock is not null ? $"{base_}\n{unlock}" : base_);
    }

    /// <summary>Builds the pet stat embed shared by /pet card and other commands — species/level/XP always shown, hunger/happiness/energy/hygiene only when <paramref name="detailed"/>, plus accessories, bio, and cosmetics when present.</summary>
    private (string petName, EmbedBuilder embed) BuildPetEmbed(PetEntity row, bool detailed, string? lastActivity = null, string? titleKey = null, string? auraKey = null)
    {
        string petName = row.Name;
        string species = row.Species;
        string breed = row.Breed;
        int hunger = row.Hunger;
        int happiness = row.Happiness;
        int energy = row.Energy;
        int hygiene = row.Hygiene;
        int xp = row.Xp;
        int level = PetHelper.LevelFromXp(xp);
        bool hibernating = row.IsHibernating;
        bool evolved = level >= 50;
        string acc1 = row.Accessory1;
        string acc2 = row.Accessory2;
        string bio = row.Bio;
        string? picUrl = row.PictureUrl;

        string emoji = PetHelper.PetEmoji(species, happiness, hunger, hibernating, evolved);
        bool veteran = level >= 20;
        Color colour = hibernating ? Color.DarkGrey : veteran ? ColourVeteran : ColourPet;

        float progress = PetHelper.LevelProgress(xp);
        int xpNext = PetHelper.XpForLevel(level + 1);
        string progressBar = PetHelper.StatBar((int)(progress * 100));

        string speciesDisplay = evolved
            ? $"{PetHelper.EvolvedName(species)} (evolved {breed})"
            : breed;

        // Use emoji in title only when no picture is set
        string title = hibernating
            ? (picUrl is null ? $"💤  {petName} is hibernating..." : $"{petName} is hibernating… 💤")
            : (picUrl is null ? $"{emoji}  {petName}" : $"🐾  {petName}");

        // Footer: last journal activity if available, otherwise owner
        string footerText = lastActivity is not null
            ? $"Last activity: {lastActivity}"
            : $"Owner: {Username}";

        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(colour)
            .WithFooter(footerText, AvatarUrl)
            .WithCurrentTimestamp()
            .AddField("Species", speciesDisplay, inline: true)
            .AddField("Level", $"**{level}**{(level >= 100 ? " 👑" : "")}", inline: true)
            .AddField("XP", $"{progressBar} `{xp}/{xpNext}`", inline: false);

        if (!string.IsNullOrWhiteSpace(picUrl))
            embed.WithThumbnailUrl(picUrl);

        if (detailed)
        {
            embed
                .AddField("🍽️ Hunger", PetHelper.StatDisplay("Hunger", hunger), inline: true)
                .AddField("😊 Happiness", PetHelper.StatDisplay("Happiness", happiness), inline: true)
                .AddField("⚡ Energy", PetHelper.StatDisplay("Energy", energy), inline: true)
                .AddField("🧼 Hygiene", PetHelper.StatDisplay("Hygiene", hygiene), inline: true);
        }

        if (!string.IsNullOrWhiteSpace(acc1)) embed.AddField("🎩 Hat", acc1, inline: true);
        if (!string.IsNullOrWhiteSpace(acc2)) embed.AddField("👗 Outfit", acc2, inline: true);
        if (!string.IsNullOrWhiteSpace(bio)) embed.AddField("📝 Bio", bio, inline: false);

        // Shop cosmetics
        if (titleKey is not null || auraKey is not null)
        {
            var cosLines = new System.Collections.Generic.List<string>();
            if (titleKey is not null) cosLines.Add(ShopHelper.CosmeticDisplay(titleKey));
            if (auraKey is not null) cosLines.Add(ShopHelper.CosmeticDisplay(auraKey));
            embed.AddField("🎨 Cosmetics", string.Join("  ·  ", cosLines), inline: false);
        }

        if (hibernating)
            embed.WithDescription("⚠️ Your pet is too hungry, unhappy, and tired to stay awake.\nFeed them to wake them up!");

        return (petName, embed);
    }
}

// ── Shared page-building helpers (used by Pet group and PetComponentHandlers) ──

/// <summary>Shared paginated-list embed/button builders for the pet list, used by both the Pet group and PetComponentHandlers.</summary>
internal static class PetPageHelper
{
    /// <summary>Builds one page of the user's pet list embed (5 pets per page).</summary>
    internal static EmbedBuilder BuildPetsPageEmbed(System.Collections.Generic.List<DiscordBot.Models.Generated.Pet> pets, int page, string username)
    {
        int total      = pets.Count;
        int totalPages = (total + Pet.PetsPerPage - 1) / Pet.PetsPerPage;
        int start      = page * Pet.PetsPerPage;
        int end        = Math.Min(start + Pet.PetsPerPage, total);

        var sb = new System.Text.StringBuilder();

        for (int i = start; i < end; i++)
        {
            var row        = pets[i];
            string petName = row.Name;
            string species = row.Species;
            string breed   = row.Breed;
            int xp         = row.Xp;
            int level      = PetHelper.LevelFromXp(xp);
            bool active      = row.IsActive;
            bool hibernating = row.IsHibernating;
            bool evolved     = level >= 50;
            int petId        = row.PetId;
            int happiness    = row.Happiness;
            int hunger       = row.Hunger;

            string emoji       = PetHelper.PetEmoji(species, happiness, hunger, hibernating, evolved);
            string status      = hibernating ? "💤 Hibernating" : active ? "✅ Active" : "💤 Resting";
            string breedDisplay = evolved ? PetHelper.EvolvedName(species) : breed;

            sb.AppendLine($"{emoji} **{petName}** — {breedDisplay} — Lv.{level} — {status} `[ID: {petId}]`");
        }

        return new EmbedHelper().BuildSimpleEmbed(
            $"🐾  {username}'s Pets ({total} total)", sb.ToString(), Pet.PetAccentColor,
            footer: $"Page {page + 1}/{totalPages} • Use /pet setactive [ID] to switch");
    }

    /// <summary>Builds the Prev/Next pagination buttons for a pet list page, disabling at the first/last page.</summary>
    internal static MessageComponent BuildPetsPageButtons(string userId, int page, int totalPets)
    {
        int totalPages = (totalPets + Pet.PetsPerPage - 1) / Pet.PetsPerPage;
        return new ComponentBuilder()
            .WithButton("◀ Prev", $"pets:nav:{userId}:{page - 1}",
                        ButtonStyle.Secondary, disabled: page == 0)
            .WithButton("Next ▶", $"pets:nav:{userId}:{page + 1}",
                        ButtonStyle.Secondary, disabled: page >= totalPages - 1)
            .Build();
    }
}

// ── Component interaction handlers for the pet list (must be outside [Group]) ──

/// <summary>Button handlers for the pet list and release-confirmation flow — declared outside [Group] since component interaction IDs aren't routed through the slash-command group.</summary>
public class PetComponentHandlers(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string UserId    => Context.User.Id.ToString();
    private string Username  => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly Color ColourInfo    = EmbedColors.Blue;
    private static readonly Color ColourSuccess = EmbedColors.Green;

    // ── release:confirm ────────────────────────────────────────────────────────

    /// <summary>Confirms and permanently deletes the pet from the /pet release confirmation prompt.</summary>
    [ComponentInteraction("release:confirm:*")]
    public async Task OnReleaseConfirmAsync(string petIdStr)
    {
        await DeferAsync();

        if (!int.TryParse(petIdStr, out int petId)) return;

        var pet = await db.Pets.FirstOrDefaultAsync(p => p.PetId == petId && p.UserId == UserId);

        if (pet is null)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Pet", "Pet not found.", Username).Build());
            return;
        }

        string name = pet.Name;

        db.Pets.Remove(pet);
        await db.SaveChangesAsync();

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                "🌈  Farewell!",
                $"You released **{name}** into the wild. 🌿\n" +
                "They'll always remember you. Goodbye, little friend!",
                ColourInfo, footer: Username, footerIconUrl: AvatarUrl).Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ── release:cancel ─────────────────────────────────────────────────────────

    /// <summary>Cancels the /pet release confirmation prompt, leaving the pet untouched.</summary>
    [ComponentInteraction("release:cancel")]
    public async Task OnReleaseCancelAsync()
    {
        await DeferAsync();
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = _embed.BuildSimpleEmbed(
                "✅  Cancelled", "Your pet is safe. 🐾",
                ColourSuccess, footer: Username, footerIconUrl: AvatarUrl).Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ── pets:nav ───────────────────────────────────────────────────────────────

    /// <summary>Handles Prev/Next clicks on a paginated pet list, re-rendering the requested page (only for the list's original owner).</summary>
    [ComponentInteraction("pets:nav:*:*")]
    public async Task OnPetsNavAsync(string targetUserId, string pageStr)
    {
        await DeferAsync();

        if (targetUserId != UserId)
        {
            await FollowupAsync("This isn't your pet list!", ephemeral: true);
            return;
        }

        int page = int.TryParse(pageStr, out int p) ? p : 0;

        var pets = await db.Pets.AsNoTracking().Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.BirthDate).ToListAsync();

        if (pets.Count == 0) return;

        page = Math.Clamp(page, 0, (pets.Count - 1) / Pet.PetsPerPage);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = PetPageHelper.BuildPetsPageEmbed(pets, page, Username).Build();
            m.Components = PetPageHelper.BuildPetsPageButtons(UserId, page, pets.Count);
        });
    }
}