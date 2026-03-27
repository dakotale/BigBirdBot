using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Tamagotchi-inspired pet system.
/// Players adopt pets, care for them, earn XP through server activity,
/// and level them up to unlock new abilities and forms.
/// Up to 5 pets per user — one is "active" at a time.
/// </summary>
[Group("pet", "Pet commands")]
public class Pet : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourPet = new(255, 179, 71);
    private static readonly Color ColourSuccess = new(87, 242, 135);
    private static readonly Color ColourError = new(237, 66, 69);
    private static readonly Color ColourInfo = new(88, 101, 242);
    private static readonly Color ColourVeteran = new(255, 215, 0);

    // Per-user battle cooldown (5 minutes)
    private static readonly ConcurrentDictionary<string, DateTime> _battleCooldowns = new();
    private const int BattleCooldownSeconds = 300;


    [SlashCommand("adopt", "Adopt a new pet and give it a name!")]
    [EnabledInDm(false)]
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

        var existing = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetsByUser",
            [new SqlParameter("@UserID", UserId)]);

        if (existing.Rows.Count >= 100)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Adopt", "You already have 100 pets! Use `/release` to make room.", Username).Build());
            return;
        }

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPet",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId),
            new SqlParameter("@Name",     name),
            new SqlParameter("@Species",  species),
            new SqlParameter("@Breed",    breed),
            new SqlParameter("@IsActive", existing.Rows.Count == 0)
        ]);

        string emoji = PetHelper.PetEmoji(species, 100, 100, false, false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{emoji}  Welcome, {name}!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"You adopted a **{breed}** named **{name}**! 🎉\n\n" +
                $"Take good care of them — feed them, play with them, and keep them happy.\n\n" +
                $"Use `/pet card` to see their stats, and `/pet feed` when they get hungry!")
            .WithFooter($"Adopted by {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    internal const int PetsPerPage = 5;
    internal static readonly Color PetAccentColor = new(255, 179, 71);

    [SlashCommand("list", "List all your pets.")]
    [EnabledInDm(false)]
    public async Task HandlePetsAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetsByUser",
            [new SqlParameter("@UserID", UserId)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Pets", "You don't have any pets yet! Use `/adopt` to get one.", Username).Build());
            return;
        }

        await FollowupAsync(
            embed: PetPageHelper.BuildPetsPageEmbed(dt, 0, Username).Build(),
            components: PetPageHelper.BuildPetsPageButtons(UserId, 0, dt.Rows.Count));
    }


    [SlashCommand("card", "Show your active pet's full stat card.")]
    [EnabledInDm(false)]
    public async Task HandlePetCardAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);

        // Fetch last journal entry to show in the card
        var journal = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetJournal",
            [new SqlParameter("@PetID", petId)]);

        string? lastActivity = null;
        if (journal.Rows.Count > 0)
        {
            var lastRow = journal.Rows[0];
            string details = lastRow["Details"].ToString()!;
            string emoji = PetHelper.JournalEventEmoji(lastRow["Event"].ToString()!);
            string relTime = DateTime.TryParse(lastRow["CreatedAt"].ToString(), out var ca)
                ? $"<t:{new DateTimeOffset(ca, TimeSpan.Zero).ToUnixTimeSeconds()}:R>"
                : "recently";
            lastActivity = $"{emoji} {details} ({relTime})";
        }

        // Fetch cosmetics applied to this pet
        var cosmeticsDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetCosmetics",
            [new SqlParameter("@PetID", petId)]);

        string? titleKey = null, auraKey = null;
        foreach (System.Data.DataRow cr in cosmeticsDt.Rows)
        {
            string ct = cr["CosmeticType"].ToString()!;
            if (ct == "title") titleKey = cr["CosmeticKey"].ToString();
            if (ct == "aura") auraKey = cr["CosmeticKey"].ToString();
        }

        var (_, embed) = BuildPetEmbed(row, detailed: true, lastActivity: lastActivity,
            titleKey: titleKey, auraKey: auraKey);
        await FollowupAsync(embed: embed.Build());
    }


    [SlashCommand("feed", "Feed your active pet.")]
    [EnabledInDm(false)]
    public async Task HandleFeedAsync(
        [Autocomplete(typeof(FoodAutocompleteHandler))][MinLength(1), MaxLength(64)] string food = "Kibble")
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));
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

        if (DateTime.TryParse(row["LastFed"].ToString(), out var lastFed))
        {
            var remaining = lastFed.AddMinutes(PetHelper.FeedCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet isn't hungry yet! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int hunger = Math.Min(100, int.Parse(row["Hunger"].ToString()!) + foodItem.hungerRestore);
        int happiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + foodItem.happyBonus);
        int energy = int.Parse(row["Energy"].ToString()!);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int oldXp = int.Parse(row["XP"].ToString()!);
        int newXp = oldXp + PetHelper.XpFeed;
        bool wasHibernating = bool.TryParse(row["IsHibernating"].ToString(), out bool hib) && hib;
        string petName = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happiness),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            newXp),
            new SqlParameter("@IsHibernating", false),
            new SqlParameter("@LastFed",       DateTime.UtcNow),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        // Clear HibernatedAt if waking up
        if (wasHibernating)
        {
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "WakePet",
                [new SqlParameter("@PetID", petId)]);
        }

        // Journal entry
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   wasHibernating ? "wake" : "feed"),
            new SqlParameter("@Details", wasHibernating
                ? $"{Username} fed {petName} {foodItem.emoji} {food} and woke them from hibernation!"
                : $"{Username} fed {petName} {foodItem.emoji} {food}.")
        ]);

        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{foodItem.emoji}  Fed {petName}!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"You fed **{petName}** some **{food}**! {foodItem.emoji}\n\n" +
                (wasHibernating ? "🌅 **Your pet woke up from hibernation!**\n\n" : "") +
                $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**\n" +
                $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**" +
                (levelUp is not null ? $"\n\n{levelUp}" : ""))
            .WithFooter($"{Username} • +{PetHelper.XpFeed} XP", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("pat", "Pet your active pet to boost their happiness!")]
    [EnabledInDm(false)]
    public async Task HandlePetPetAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (DateTime.TryParse(row["LastPetted"].ToString(), out var lastPetted))
        {
            var remaining = lastPetted.AddMinutes(PetHelper.PetCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet needs a moment! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int happiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + 15);
        int hunger = int.Parse(row["Hunger"].ToString()!);
        int energy = int.Parse(row["Energy"].ToString()!);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int oldXp = int.Parse(row["XP"].ToString()!);
        int newXp = oldXp + PetHelper.XpPet;
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happiness),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            newXp),
            new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(hunger, happiness, energy)),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DateTime.UtcNow),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        string[] reactions = species.ToLower() switch
        {
            "cat" => ["*purrs contentedly* 😺", "*slow blinks at you* 😸", "*headbutts your hand* 🐱"],
            "dog" => ["*tail wagging intensifies* 🐶", "*licks your face* 🐕", "*rolls over for belly rubs* 🐾"],
            "horse" => ["*nuzzles you gently* 🐴", "*whinnies happily* 🐎", "*tosses their mane* 🐴"],
            "bird" => ["*chirps excitedly* 🐦", "*flaps their wings happily* 🦜", "*whistles a little tune* 🎵"],
            "dinosaur" => ["*nuzzles you with their snout* 🦕", "*makes a tiny happy roar* 🐉", "*wags their tail enthusiastically* 🦖"],
            "bunny" => ["*thumps happily* 🐰", "*licks your hand* 🐇", "*flops onto their side (the highest bunny compliment)* 😊"],
            _ => ["*enjoys the attention* 🐾"]
        };

        string reaction = reactions[Random.Shared.Next(reactions.Length)];
        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🤗  Petted {petName}!")
            .WithColor(ColourPet)
            .WithDescription(
                $"**{petName}** {reaction}\n\n" +
                $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**" +
                (levelUp is not null ? $"\n\n{levelUp}" : ""))
            .WithFooter($"{Username} • +{PetHelper.XpPet} XP", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("groom", "Groom your active pet to boost their hygiene!")]
    [EnabledInDm(false)]
    public async Task HandleGroomAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (DateTime.TryParse(row["LastGroomed"].ToString(), out var lastGroomed))
        {
            var remaining = lastGroomed.AddMinutes(PetHelper.GroomCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet is already clean! Come back in **{(int)remaining.TotalMinutes}m**.");
                return;
            }
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int hygiene = Math.Min(100, int.Parse(row["Hygiene"].ToString()!) + 40);
        int happy = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + 10);
        int hunger = int.Parse(row["Hunger"].ToString()!);
        int energy = int.Parse(row["Energy"].ToString()!);
        int oldXp = int.Parse(row["XP"].ToString()!);
        int newXp = oldXp + PetHelper.XpGroom;
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happy),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            newXp),
            new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(hunger, happy, energy)),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DateTime.UtcNow),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        string groomVerb = species.ToLower() switch
        {
            "cat" => "brushed",
            "dog" => "bathed",
            "horse" => "groomed",
            "bird" => "preened",
            "dinosaur" => "scrubbed down",
            "bunny" => "gently brushed",
            _ => "cleaned"
        };

        // Journal entry
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   "groom"),
            new SqlParameter("@Details", $"{Username} {groomVerb} {petName}. Squeaky clean! 🛁")
        ]);

        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🛁  Groomed {petName}!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"You {groomVerb} **{petName}**! They're squeaky clean! ✨\n\n" +
                $"🧼 Hygiene: {PetHelper.StatBar(hygiene)} **{hygiene}/100**\n" +
                $"😊 Happiness: {PetHelper.StatBar(happy)} **{happy}/100**" +
                (levelUp is not null ? $"\n\n{levelUp}" : ""))
            .WithFooter($"{Username} • +{PetHelper.XpGroom} XP", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("play", "Play with your active pet!")]
    [EnabledInDm(false)]
    public async Task HandlePlayWithAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (bool.TryParse(row["IsHibernating"].ToString(), out bool hib) && hib)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first to wake them up.");
            return;
        }

        if (DateTime.TryParse(row["LastPlayed"].ToString(), out var lastPlayed))
        {
            var remaining = lastPlayed.AddMinutes(PetHelper.PlayCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet is tired! Come back in **{(int)remaining.TotalMinutes}m {remaining.Seconds}s**.");
                return;
            }
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int happiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + 25);
        int energy = Math.Max(0, int.Parse(row["Energy"].ToString()!) - 15);
        int hunger = Math.Max(0, int.Parse(row["Hunger"].ToString()!) - 10);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int oldXp = int.Parse(row["XP"].ToString()!);
        int newXp = oldXp + PetHelper.XpPlay;
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happiness),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            newXp),
            new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(hunger, happiness, energy)),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DateTime.UtcNow),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        string[] activities = species.ToLower() switch
        {
            "cat" => ["chased a laser pointer", "played with a ball of yarn", "pounced on a toy mouse"],
            "dog" => ["fetched the ball 12 times", "zoomed around the yard", "played tug-of-war"],
            "horse" => ["galloped through a field", "jumped a fence gracefully", "trotted around the paddock"],
            "bird" => ["learned a new song", "played with a mirror", "flew acrobatic loops"],
            "dinosaur" => ["stomped around the yard looking prehistoric", "chased a ball and accidentally sat on it", "roared at a squirrel until it left"],
            "bunny" => ["binkied non-stop for five minutes", "zoomed laps around the living room", "tossed their toy in the air repeatedly"],
            _ => ["had a great time"]
        };

        string activity = activities[Random.Shared.Next(activities.Length)];
        var (_, levelUp) = CheckLevelUp(oldXp, newXp);

        // Journal entry
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   "play"),
            new SqlParameter("@Details", $"{petName} {activity}! 🎮")
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎮  Playtime with {petName}!")
            .WithColor(ColourPet)
            .WithDescription(
                $"**{petName}** {activity}! 🎉\n\n" +
                $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**\n" +
                $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**\n" +
                $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**" +
                (levelUp is not null ? $"\n\n{levelUp}" : ""))
            .WithFooter($"{Username} • +{PetHelper.XpPlay} XP", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("sleep", "Put your pet to sleep to restore their energy.")]
    [EnabledInDm(false)]
    public async Task HandlePetSleepAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int currentEnergy = int.Parse(row["Energy"].ToString()!);
        const int sleepThreshold = 50;

        if (currentEnergy >= sleepThreshold)
        {
            await ErrorAsync(
                $"**{row["Name"]}** isn't tired yet! Energy is at **{currentEnergy}/100**.\n" +
                $"Sleep is only available below **{sleepThreshold} energy**.");
            return;
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int energy = Math.Min(100, currentEnergy + 50);
        int hunger = int.Parse(row["Hunger"].ToString()!);
        int happy = int.Parse(row["Happiness"].ToString()!);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int xp = int.Parse(row["XP"].ToString()!);
        string petName = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happy),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            xp),
            new SqlParameter("@IsHibernating", false),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DBNull.Value),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DateTime.UtcNow)
        ]);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   "sleep"),
            new SqlParameter("@Details", $"{petName} took a nap and restored some energy. 💤")
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"💤  {petName} is napping!")
            .WithColor(ColourInfo)
            .WithDescription(
                $"**{petName}** curled up for a nap. 😴\n\n" +
                $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("hug", "Give your pet a warm hug! Small happiness boost, no XP.")]
    [EnabledInDm(false)]
    public async Task HandlePetHugAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        // 1 minute cooldown — intentionally short, pure flavour
        if (DateTime.TryParse(row["LastPetted"].ToString(), out var lastPetted))
        {
            var remaining = lastPetted.AddMinutes(1) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await ErrorAsync($"Your pet needs a breather! Try again in **{remaining.Seconds}s**.");
                return;
            }
        }

        int petId = int.Parse(row["PetID"].ToString()!);
        int happiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + 5);
        int hunger = int.Parse(row["Hunger"].ToString()!);
        int energy = int.Parse(row["Energy"].ToString()!);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int xp = int.Parse(row["XP"].ToString()!);
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
        [
            new SqlParameter("@PetID",         petId),
            new SqlParameter("@Hunger",        hunger),
            new SqlParameter("@Happiness",     happiness),
            new SqlParameter("@Energy",        energy),
            new SqlParameter("@Hygiene",       hygiene),
            new SqlParameter("@XP",            xp),
            new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(hunger, happiness, energy)),
            new SqlParameter("@LastFed",       DBNull.Value),
            new SqlParameter("@LastPetted",    DateTime.UtcNow),
            new SqlParameter("@LastGroomed",   DBNull.Value),
            new SqlParameter("@LastPlayed",    DBNull.Value),
            new SqlParameter("@LastSlept",     DBNull.Value)
        ]);

        // Log to journal
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   "hug"),
            new SqlParameter("@Details", $"{Username} gave {petName} a hug! 🤗")
        ]);

        string[] hugReactions = species.ToLower() switch
        {
            "cat" => ["tolerates it with dignity 😸", "leans into it just a little 🐱", "pretends not to enjoy it but their purr says otherwise 😺"],
            "dog" => ["goes absolutely wild with joy 🐶", "licks your entire face 🐕", "wiggles so hard they nearly fall over 🐾"],
            "horse" => ["rests their head on your shoulder 🐴", "lets out a soft, warm breath 🐎", "nuzzles you gently in return 🐴"],
            "bird" => ["puffs up into a happy little ball 🐦", "clicks their beak contentedly 🦜", "buries their head in your hair 🐦"],
            "dinosaur" => ["makes a small, rumbling happy sound 🦕", "nudges you with their giant snout 🐉", "sits very still and looks extremely pleased 🦖"],
            "bunny" => ["licks your nose 🐰", "does a tiny binky from happiness 🐇", "flops over dramatically onto your lap 🐰"],
            _ => ["enjoys the affection 🐾"]
        };

        string reaction = hugReactions[Random.Shared.Next(hugReactions.Length)];

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🤗  Hugged {petName}!")
            .WithColor(ColourPet)
            .WithDescription($"**{petName}** {reaction}\n\n😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("journal", "View the recent activity log for your active pet.")]
    [EnabledInDm(false)]
    public async Task HandlePetJournalAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;
        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));
        bool evolved = level >= 50;

        var entries = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetJournal",
            [new SqlParameter("@PetID", petId)]);

        string emoji = PetHelper.PetEmoji(species, 80, 80, false, evolved);

        if (entries.Rows.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"📓  {petName}'s Journal")
                .WithColor(ColourInfo)
                .WithDescription("No journal entries yet — go interact with your pet!")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        var sb = new System.Text.StringBuilder();

        foreach (DataRow entry in entries.Rows)
        {
            string eventType = entry["Event"].ToString()!;
            string details = entry["Details"].ToString()!;
            string eventEmoji = PetHelper.JournalEventEmoji(eventType);
            string timestamp = DateTime.TryParse(entry["CreatedAt"].ToString(), out var ts)
                ? $"<t:{new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeSeconds()}:R>"
                : "";

            sb.AppendLine($"{eventEmoji} {details} {timestamp}");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"📓  {emoji} {petName}'s Journal")
            .WithColor(ColourInfo)
            .WithDescription(sb.ToString())
            .WithFooter($"Last 20 entries • {Username}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("trick", "Make your pet perform a trick!")]
    [EnabledInDm(false)]
    public async Task HandleTrickAsync(
        [Choice("Trick 1 (Lv.5)",  "1"),
         Choice("Trick 2 (Lv.20)", "2"),
         Choice("Trick 3 (Lv.50)", "3"),
         Choice("Trick 4 (Lv.75)", "4")]
        string slot = "1")
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));

        if (slot == "1" && level < 5) { await ErrorAsync($"Trick slot 1 unlocks at **level 5**! Your pet is level {level}."); return; }
        if (slot == "2" && level < 20) { await ErrorAsync($"Trick slot 2 unlocks at **level 20**! Your pet is level {level}."); return; }
        if (slot == "3" && level < 50) { await ErrorAsync($"Trick slot 3 unlocks at **level 50**! Your pet is level {level}."); return; }
        if (slot == "4" && level < 75) { await ErrorAsync($"Trick slot 4 unlocks at **level 75**! Your pet is level {level}."); return; }

        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;
        string trick = PetHelper.PerformTrick(species, int.Parse(slot));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎪  {petName} performs a trick!")
            .WithColor(ColourPet)
            .WithDescription($"**{petName}** {trick}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("accessory", "Equip an accessory to your active pet. (Unlocks at level 10)")]
    [EnabledInDm(false)]
    public async Task HandleAccessoryAsync(
        [Choice("Slot 1 — Hat",           "slot1"),
         Choice("Slot 2 — Collar/Outfit", "slot2")]
        string slot,
        [MinLength(1), MaxLength(32)] string item)
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));

        if (slot == "slot1" && level < 10) { await ErrorAsync("Accessory slot 1 unlocks at **level 10**!"); return; }
        if (slot == "slot2" && level < 15) { await ErrorAsync("Accessory slot 2 unlocks at **level 15**!"); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string slotCol = slot == "slot1" ? "Accessory1" : "Accessory2";

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetAccessory",
        [
            new SqlParameter("@PetID",    petId),
            new SqlParameter("@SlotName", slotCol),
            new SqlParameter("@Item",     item.Trim())
        ]);

        string petName = row["Name"].ToString()!;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("👗  Accessory Equipped!")
            .WithColor(ColourSuccess)
            .WithDescription($"**{petName}** is now wearing **{item.Trim()}**! Looking good! ✨")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("setactive", "Switch which pet is currently active.")]
    [EnabledInDm(false)]
    public async Task HandleSetActiveAsync([MinValue(1)] int petId)
    {
        await DeferAsync();

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "SetActivePet",
        [
            new SqlParameter("@UserID", UserId),
            new SqlParameter("@PetID",  petId)
        ]);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetByID",
        [
            new SqlParameter("@PetID",  petId),
            new SqlParameter("@UserID", UserId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Pet not found."); return; }

        string name = dt.Rows[0]["Name"].ToString()!;
        string species = dt.Rows[0]["Species"].ToString()!;
        string emoji = PetHelper.PetEmoji(species, 50, 50, false, false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{emoji}  Active pet changed!")
            .WithColor(ColourInfo)
            .WithDescription($"**{name}** is now your active pet.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("rename", "Rename your active pet.")]
    [EnabledInDm(false)]
    public async Task HandleRenameAsync([MinLength(1), MaxLength(32)] string newName)
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string oldName = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "RenamePet",
        [
            new SqlParameter("@PetID", petId),
            new SqlParameter("@Name",  newName.Trim())
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("✏️  Pet Renamed!")
            .WithColor(ColourInfo)
            .WithDescription($"**{oldName}** is now known as **{newName.Trim()}**!")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("release", "Release one of your pets. This cannot be undone!")]
    [EnabledInDm(false)]
    public async Task HandleReleaseAsync([MinValue(1)] int petId)
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetByID",
        [
            new SqlParameter("@PetID",  petId),
            new SqlParameter("@UserID", UserId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Pet not found or doesn't belong to you."); return; }

        string name = dt.Rows[0]["Name"].ToString()!;
        string species = dt.Rows[0]["Species"].ToString()!;
        int level = PetHelper.LevelFromXp(int.Parse(dt.Rows[0]["XP"].ToString()!));
        string emoji = PetHelper.PetEmoji(species, 80, 80, false, level >= 50);

        var components = new ComponentBuilder()
            .WithButton("Yes, release them", $"release:confirm:{petId}", ButtonStyle.Danger, new Emoji("🌿"))
            .WithButton("Cancel", "release:cancel", ButtonStyle.Secondary, new Emoji("✖️"))
            .Build();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"⚠️  Release {name}?")
            .WithColor(ColourError)
            .WithDescription(
                $"{emoji} **{name}** is a level **{level}** {species}.\n\n" +
                "Releasing a pet is **permanent** and cannot be undone.\nAre you sure?")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), components: components);
    }

    // release:confirm and release:cancel are in PetComponentHandlers below (outside [Group])


    [SlashCommand("leaderboard", "Show the top pets in this server by level.")]
    [EnabledInDm(false)]
    public async Task HandleLeaderboardAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Leaderboard", "No pets found in this server yet!", Username).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        int rank = 0;
        string[] medals = ["🥇", "🥈", "🥉"];

        foreach (DataRow row in dt.Rows)
        {
            string medal = rank < 3 ? medals[rank] : $"**{rank + 1}.**";
            string petName = row["Name"].ToString()!;
            string petSpecies = row["Species"].ToString()!;
            int xp = int.Parse(row["XP"].ToString()!);
            int level = PetHelper.LevelFromXp(xp);
            string owner = row["Username"].ToString()!;
            bool evolved = level >= 50;
            string crown = level >= 100 ? " 👑" : "";
            string evolvedStr = evolved ? $" *({PetHelper.EvolvedName(petSpecies)})*" : "";

            sb.AppendLine($"{medal} **{petName}**{evolvedStr}{crown} — Lv.{level} | Owner: {owner}");
            rank++;
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🏆  Pet Leaderboard — {Context.Guild.Name}")
            .WithColor(ColourVeteran)
            .WithDescription(sb.ToString())
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("foodlist", "Show all available food items for your pet.")]
    [EnabledInDm(false)]
    public async Task HandleFoodListAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("🍽️  Available Food")
            .WithColor(ColourPet)
            .WithDescription(PetHelper.ListFoods(level))
            .WithFooter($"Your pet is level {level}", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("explore", "Send your pet on an adventure! Come back later to collect the reward.")]
    [EnabledInDm(false)]
    public async Task HandleExploreAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        if (bool.TryParse(row["IsHibernating"].ToString(), out bool hib) && hib)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first before sending them exploring.");
            return;
        }

        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;
        int petId = int.Parse(row["PetID"].ToString()!);
        int oldXp = int.Parse(row["XP"].ToString()!);
        int level = PetHelper.LevelFromXp(oldXp);

        // Check if already exploring
        var exploreStatus = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetExplore",
            [new SqlParameter("@PetID", petId)]);

        if (exploreStatus.Rows.Count > 0)
        {
            var returnsAt = DateTime.Parse(exploreStatus.Rows[0]["ReturnsAt"].ToString()!);

            // Ready to claim
            if (DateTime.UtcNow >= returnsAt)
            {
                string rewardKey = exploreStatus.Rows[0]["RewardKey"].ToString()!;
                var reward = PetHelper.ExploreRewards.First(r => r.key == rewardKey);

                // xp_boost: 2× XP for the duration
                bool hasXpBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "xp_boost");
                int bonusXp = hasXpBoost ? reward.xp : 0;
                int newXp = oldXp + reward.xp + bonusXp;
                int hunger = Math.Max(0, int.Parse(row["Hunger"].ToString()!) - reward.hungerCost);
                int happiness = Math.Min(100, int.Parse(row["Happiness"].ToString()!) + reward.happyBonus);
                int energy = Math.Max(0, int.Parse(row["Energy"].ToString()!) - reward.energyCost);
                int hygiene = int.Parse(row["Hygiene"].ToString()!);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
                [
                    new SqlParameter("@PetID",         petId),
                    new SqlParameter("@Hunger",        hunger),
                    new SqlParameter("@Happiness",     happiness),
                    new SqlParameter("@Energy",        energy),
                    new SqlParameter("@Hygiene",       hygiene),
                    new SqlParameter("@XP",            newXp),
                    new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(hunger, happiness, energy)),
                    new SqlParameter("@LastFed",       DBNull.Value),
                    new SqlParameter("@LastPetted",    DBNull.Value),
                    new SqlParameter("@LastGroomed",   DBNull.Value),
                    new SqlParameter("@LastPlayed",    DBNull.Value),
                    new SqlParameter("@LastSlept",     DBNull.Value)
                ]);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "ClearPetExplore",
                    [new SqlParameter("@PetID", petId)]);

                // Journal entry
                string rewardDesc = PetHelper.ExploreRewardDescription(rewardKey, species, reward.description);
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
                [
                    new SqlParameter("@PetID",   petId),
                    new SqlParameter("@Event",   "explore"),
                    new SqlParameter("@Details", $"{petName} returned from an adventure and found {reward.emoji} {rewardDesc} (+{reward.xp} XP)!")
                ]);

                var (_, levelUp) = CheckLevelUp(oldXp, newXp);

                string adventure = PetHelper.ExploreNarrative(species, rewardKey);
                string opener = PetHelper.ExploreReturnOpener(petName);
                string? picUrl = row["PictureUrl"] as string;

                var eb = new EmbedBuilder()
                    .WithTitle($"{reward.emoji}  {petName} returned from their adventure!")
                    .WithColor(ColourSuccess)
                    .WithDescription(
                        $"{opener}\n\n" +
                        $"{adventure}\n\n" +
                        $"**Reward:** {reward.emoji} {rewardDesc}\n\n" +
                        (hasXpBoost ? $"✨ **+{reward.xp + bonusXp} XP** *(XP Boost! +{bonusXp} bonus)*\n" : $"✨ **+{reward.xp} XP**\n") +
                        $"😊 Happiness: {PetHelper.StatBar(happiness)} **{happiness}/100**\n" +
                        $"⚡ Energy: {PetHelper.StatBar(energy)} **{energy}/100**\n" +
                        $"🍽️ Hunger: {PetHelper.StatBar(hunger)} **{hunger}/100**" +
                        (levelUp is not null ? $"\n\n{levelUp}" : ""))
                    .WithFooter($"{Username} • +{reward.xp} XP", AvatarUrl)
                    .WithCurrentTimestamp();

                if (picUrl is not null) eb.WithThumbnailUrl(picUrl);

                await FollowupAsync(embed: eb.Build());
                return;
            }

            // Still out exploring
            var remaining = returnsAt - DateTime.UtcNow;
            string timeLeft = remaining.TotalMinutes < 1
                ? $"{remaining.Seconds}s"
                : $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s";

            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"🗺️  {petName} is still exploring!")
                .WithColor(ColourInfo)
                .WithDescription(
                    $"**{petName}** is out on an adventure and hasn't returned yet.\n\n" +
                    $"⏳ Returns in **{timeLeft}** — come back to collect their reward!")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
            return;
        }

        // Send pet exploring — duration scales slightly with level (30–60 min)
        int durationMinutes = Math.Min(60, 30 + (level / 10) * 5);

        // explore_boost: guarantees a rare+ reward tier
        bool hasExploreBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "explore_boost");
        var rewardPick = hasExploreBoost
            ? PetHelper.PickExploreRewardBoosted(level)
            : PetHelper.PickExploreReward(level);
        if (hasExploreBoost) ShopHelper.ConsumeActiveEffect(UserId, ServerId, "explore_boost");

        var returnsAtNew = DateTime.UtcNow.AddMinutes(durationMinutes);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "SetPetExplore",
        [
            new SqlParameter("@PetID",     petId),
            new SqlParameter("@ReturnsAt", returnsAtNew),
            new SqlParameter("@RewardKey", rewardPick.key)
        ]);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddPetJournalEntry",
        [
            new SqlParameter("@PetID",   petId),
            new SqlParameter("@Event",   "explore_depart"),
            new SqlParameter("@Details", $"{petName} set off on an adventure! Returns <t:{new DateTimeOffset(returnsAtNew, TimeSpan.Zero).ToUnixTimeSeconds()}:R>.")
        ]);

        string departureMsg = PetHelper.ExploreDeparture(species);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🗺️  {petName} set off on an adventure!")
            .WithColor(ColourPet)
            .WithDescription(
                $"{departureMsg}\n\n" +
                $"⏳ They'll be back in **{durationMinutes} minutes**.\n" +
                $"Use `/explore` again to collect their reward when they return!")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("battle", "Challenge another user's active pet to a battle!")]
    [EnabledInDm(false)]
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

        var (challengerRow, challengerError) = GetActivePet();
        if (challengerRow is null) { await ErrorAsync(challengerError!); return; }

        if (bool.TryParse(challengerRow["IsHibernating"].ToString(), out bool chib) && chib)
        {
            await ErrorAsync("Your pet is hibernating! Feed them first before battling.");
            return;
        }

        var opponentPetDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", opponent.Id.ToString())]);

        if (opponentPetDt.Rows.Count == 0)
        {
            await ErrorAsync($"**{opponent.Username}** doesn't have an active pet!");
            return;
        }

        var opponentRow = opponentPetDt.Rows[0];

        if (bool.TryParse(opponentRow["IsHibernating"].ToString(), out bool ohib) && ohib)
        {
            await ErrorAsync($"**{opponent.Username}**'s pet is hibernating and can't battle right now!");
            return;
        }


        string challengerName = challengerRow["Name"].ToString()!;
        string challengerSpecies = challengerRow["Species"].ToString()!;
        int challengerXp = int.Parse(challengerRow["XP"].ToString()!);
        int challengerLevel = PetHelper.LevelFromXp(challengerXp);
        int challengerHunger = int.Parse(challengerRow["Hunger"].ToString()!);
        int challengerHappy = int.Parse(challengerRow["Happiness"].ToString()!);
        int challengerEnergy = int.Parse(challengerRow["Energy"].ToString()!);
        int challengerPetId = int.Parse(challengerRow["PetID"].ToString()!);

        string opponentName = opponentRow["Name"].ToString()!;
        string opponentSpecies = opponentRow["Species"].ToString()!;
        int opponentXp = int.Parse(opponentRow["XP"].ToString()!);
        int opponentLevel = PetHelper.LevelFromXp(opponentXp);
        int opponentHunger = int.Parse(opponentRow["Hunger"].ToString()!);
        int opponentHappy = int.Parse(opponentRow["Happiness"].ToString()!);
        int opponentEnergy = int.Parse(opponentRow["Energy"].ToString()!);
        int opponentPetId = int.Parse(opponentRow["PetID"].ToString()!);

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
        void ApplyBattleCost(int petId, int oldXp, int xpGain, int hunger, int happy, int energy, int hygiene)
        {
            int newXp = oldXp + xpGain;
            int newHunger = Math.Max(0, hunger - hungerCost);
            int newEnergy = Math.Max(0, energy - energyCost);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetStats",
            [
                new SqlParameter("@PetID",         petId),
                new SqlParameter("@Hunger",        newHunger),
                new SqlParameter("@Happiness",     happy),
                new SqlParameter("@Energy",        newEnergy),
                new SqlParameter("@Hygiene",       hygiene),
                new SqlParameter("@XP",            newXp),
                new SqlParameter("@IsHibernating", PetHelper.ShouldHibernate(newHunger, happy, newEnergy)),
                new SqlParameter("@LastFed",       DBNull.Value),
                new SqlParameter("@LastPetted",    DBNull.Value),
                new SqlParameter("@LastGroomed",   DBNull.Value),
                new SqlParameter("@LastPlayed",    DBNull.Value),
                new SqlParameter("@LastSlept",     DBNull.Value)
            ]);
        }

        int cXpGain = draw ? loserXpGain : challengerWon ? winnerXpGain : loserXpGain;
        int oXpGain = draw ? loserXpGain : challengerWon ? loserXpGain : winnerXpGain;

        ApplyBattleCost(challengerPetId, challengerXp, cXpGain,
            challengerHunger, challengerHappy, challengerEnergy,
            int.Parse(challengerRow["Hygiene"].ToString()!));

        ApplyBattleCost(opponentPetId, opponentXp, oXpGain,
            opponentHunger, opponentHappy, opponentEnergy,
            int.Parse(opponentRow["Hygiene"].ToString()!));


        var (_, cLevelUp) = CheckLevelUp(challengerXp, challengerXp + cXpGain);
        var (_, oLevelUp) = CheckLevelUp(opponentXp, opponentXp + oXpGain);


        string challengerEmoji = PetHelper.PetEmoji(challengerSpecies, challengerHappy, challengerHunger, false, challengerLevel >= 50);
        string opponentEmoji = PetHelper.PetEmoji(opponentSpecies, opponentHappy, opponentHunger, false, opponentLevel >= 50);
        string? challengerPic = challengerRow["PictureUrl"] as string;
        string? opponentPic = opponentRow["PictureUrl"] as string;

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

        EmbedBuilder BuildBattleEmbed(int roundsShown)
        {
            // Use picture if available — winner's pic on final frame, challenger's pic during battle
            string? thumbUrl = roundsShown < 3
                ? (challengerPic ?? opponentPic)
                : (challengerWon ? challengerPic : opponentPic)
                  ?? (challengerWon ? opponentPic : challengerPic);

            // Emoji labels — stripped from title if picture is set
            string cLabel = challengerPic is null ? $"{challengerEmoji} {challengerName}" : challengerName;
            string oLabel = opponentPic is null ? $"{opponentEmoji} {opponentName}" : opponentName;

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


        var battleMsg = await FollowupAsync(embed: BuildBattleEmbed(1).Build());
        await Task.Delay(1500);
        await battleMsg.ModifyAsync(m => m.Embed = BuildBattleEmbed(2).Build());
        await Task.Delay(1500);
        await battleMsg.ModifyAsync(m => m.Embed = BuildBattleEmbed(3).Build());
    }


    [SlashCommand("picture", "Upload a photo of your active pet — it will appear in all their embeds.")]
    [EnabledInDm(false)]
    public async Task HandlePetPictureAsync(
        IAttachment? picture = null)
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;
        int level = PetHelper.LevelFromXp(int.Parse(row["XP"].ToString()!));

        // If no attachment — show current picture or instructions
        if (picture is null)
        {
            string? current = row["PictureUrl"] as string;
            if (!string.IsNullOrWhiteSpace(current))
            {
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle($"🖼️  {petName}'s Picture")
                    .WithColor(ColourPet)
                    .WithDescription(
                        $"**{petName}** already has a picture set.\n\n" +
                        $"Upload a new image with `/pet picture [image]` to replace it, " +
                        $"or use `/pet pictureclear` to remove it.")
                    .WithImageUrl(current)
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
            }
            else
            {
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle($"🖼️  No Picture Set")
                    .WithColor(ColourInfo)
                    .WithDescription(
                        $"**{petName}** doesn't have a picture yet.\n\n" +
                        $"Attach an image when using `/pet picture` to set one.\n" +
                        $"Supported formats: PNG, JPG, GIF, WEBP")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
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

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetPicture",
        [
            new SqlParameter("@PetID",      petId),
            new SqlParameter("@UserID",     UserId),
            new SqlParameter("@PictureUrl", url)
        ]);

        string emoji = PetHelper.PetEmoji(species, 80, 80, false, level >= 50);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🖼️  Picture updated for {petName}!")
            .WithColor(ColourSuccess)
            .WithDescription(
                $"{emoji} **{petName}**'s picture has been set.\n" +
                $"It will now appear as a thumbnail in all their embeds.")
            .WithThumbnailUrl(url)
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    [SlashCommand("pictureclear", "Remove the photo from your active pet.")]
    [EnabledInDm(false)]
    public async Task HandlePetPictureClearAsync()
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string petName = row["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetPicture",
        [
            new SqlParameter("@PetID",      petId),
            new SqlParameter("@UserID",     UserId),
            new SqlParameter("@PictureUrl", DBNull.Value)
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🖼️  Picture cleared")
            .WithColor(ColourInfo)
            .WithDescription($"**{petName}**'s picture has been removed. Emojis will be used instead.")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("bio", "Set a custom bio for your active pet (up to 1000 characters). Leave blank to clear it.")]
    [EnabledInDm(false)]
    public async Task HandlePetBioAsync(
        [MaxLength(1000)] string bio = "")
    {
        await DeferAsync();

        var (row, error) = GetActivePet();
        if (row is null) { await ErrorAsync(error!); return; }

        int petId = int.Parse(row["PetID"].ToString()!);
        string petName = row["Name"].ToString()!;
        string cleaned = bio.Trim();

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdatePetBio",
        [
            new SqlParameter("@PetID", petId),
            new SqlParameter("@Bio",   cleaned)
        ]);

        if (string.IsNullOrEmpty(cleaned))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"📝  Bio cleared")
                .WithColor(ColourInfo)
                .WithDescription($"**{petName}**'s bio has been removed.")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
        }
        else
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle($"📝  Bio updated!")
                .WithColor(ColourSuccess)
                .WithDescription($"**{petName}**'s bio:\n\n*{cleaned}*")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build());
        }
    }


    [SlashCommand("breedlist", "Show all available breeds for a species.")]
    [EnabledInDm(false)]
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

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{emoji}  {char.ToUpper(species[0])}{species[1..]} Breeds")
            .WithColor(ColourPet)
            .WithDescription(list)
            .WithFooter("Use /adopt to choose your breed")
            .WithCurrentTimestamp()
            .Build());
    }


    private (DataRow? row, string? error) GetActivePet()
    {
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", UserId)]);

        return dt.Rows.Count == 0
            ? (null, "You don't have an active pet! Use `/adopt` to get one.")
            : (dt.Rows[0], null);
    }

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Pet", message, Username).Build());

    private static (int newLevel, string? unlockMessage) CheckLevelUp(int oldXp, int newXp)
    {
        int oldLevel = PetHelper.LevelFromXp(oldXp);
        int newLevel = PetHelper.LevelFromXp(newXp);
        if (newLevel <= oldLevel) return (newLevel, null);
        string base_ = $"🎉 **Level Up! Your pet is now level {newLevel}!**";
        string? unlock = PetHelper.LevelUpUnlock(newLevel);
        return (newLevel, unlock is not null ? $"{base_}\n{unlock}" : base_);
    }

    private (string petName, EmbedBuilder embed) BuildPetEmbed(DataRow row, bool detailed, string? lastActivity = null, string? titleKey = null, string? auraKey = null)
    {
        string petName = row["Name"].ToString()!;
        string species = row["Species"].ToString()!;
        string breed = row["Breed"].ToString()!;
        int hunger = int.Parse(row["Hunger"].ToString()!);
        int happiness = int.Parse(row["Happiness"].ToString()!);
        int energy = int.Parse(row["Energy"].ToString()!);
        int hygiene = int.Parse(row["Hygiene"].ToString()!);
        int xp = int.Parse(row["XP"].ToString()!);
        int level = PetHelper.LevelFromXp(xp);
        bool hibernating = bool.TryParse(row["IsHibernating"].ToString(), out bool h) && h;
        bool evolved = level >= 50;
        string acc1 = row["Accessory1"].ToString()!;
        string acc2 = row["Accessory2"].ToString()!;
        string bio = row["Bio"].ToString()!;
        string? picUrl = row["PictureUrl"] as string;

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

internal static class PetPageHelper
{
    internal static EmbedBuilder BuildPetsPageEmbed(System.Data.DataTable dt, int page, string username)
    {
        int total      = dt.Rows.Count;
        int totalPages = (total + Pet.PetsPerPage - 1) / Pet.PetsPerPage;
        int start      = page * Pet.PetsPerPage;
        int end        = Math.Min(start + Pet.PetsPerPage, total);

        var sb = new System.Text.StringBuilder();

        for (int i = start; i < end; i++)
        {
            var row        = dt.Rows[i];
            string petName = row["Name"].ToString()!;
            string species = row["Species"].ToString()!;
            string breed   = row["Breed"].ToString()!;
            int xp         = int.Parse(row["XP"].ToString()!);
            int level      = PetHelper.LevelFromXp(xp);
            bool active      = bool.TryParse(row["IsActive"].ToString(),      out bool a) && a;
            bool hibernating = bool.TryParse(row["IsHibernating"].ToString(), out bool h) && h;
            bool evolved     = level >= 50;
            int petId        = int.Parse(row["PetID"].ToString()!);
            int happiness    = int.Parse(row["Happiness"].ToString()!);
            int hunger       = int.Parse(row["Hunger"].ToString()!);

            string emoji       = PetHelper.PetEmoji(species, happiness, hunger, hibernating, evolved);
            string status      = hibernating ? "💤 Hibernating" : active ? "✅ Active" : "💤 Resting";
            string breedDisplay = evolved ? PetHelper.EvolvedName(species) : breed;

            sb.AppendLine($"{emoji} **{petName}** — {breedDisplay} — Lv.{level} — {status} `[ID: {petId}]`");
        }

        return new EmbedBuilder()
            .WithTitle($"🐾  {username}'s Pets ({total} total)")
            .WithColor(Pet.PetAccentColor)
            .WithDescription(sb.ToString())
            .WithFooter($"Page {page + 1}/{totalPages} • Use /pet setactive [ID] to switch")
            .WithCurrentTimestamp();
    }

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

public class PetComponentHandlers : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp  = new();
    private readonly EmbedHelper     _embed = new();

    private string UserId    => Context.User.Id.ToString();
    private string Username  => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly Color ColourInfo    = new(88, 101, 242);
    private static readonly Color ColourSuccess = new(87, 242, 135);

    // ── release:confirm ────────────────────────────────────────────────────────

    [ComponentInteraction("release:confirm:*")]
    public async Task OnReleaseConfirmAsync(string petIdStr)
    {
        await DeferAsync();

        if (!int.TryParse(petIdStr, out int petId)) return;

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetByID",
        [
            new SqlParameter("@PetID",  petId),
            new SqlParameter("@UserID", UserId)
        ]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Pet", "Pet not found.", Username).Build());
            return;
        }

        string name = dt.Rows[0]["Name"].ToString()!;

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeletePet",
        [
            new SqlParameter("@PetID",  petId),
            new SqlParameter("@UserID", UserId)
        ]);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle("🌈  Farewell!")
                .WithColor(ColourInfo)
                .WithDescription(
                    $"You released **{name}** into the wild. 🌿\n" +
                    "They'll always remember you. Goodbye, little friend!")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ── release:cancel ─────────────────────────────────────────────────────────

    [ComponentInteraction("release:cancel")]
    public async Task OnReleaseCancelAsync()
    {
        await DeferAsync();
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithTitle("✅  Cancelled")
                .WithColor(ColourSuccess)
                .WithDescription("Your pet is safe. 🐾")
                .WithFooter(Username, AvatarUrl)
                .WithCurrentTimestamp()
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ── pets:nav ───────────────────────────────────────────────────────────────

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

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPetsByUser",
            [new SqlParameter("@UserID", UserId)]);

        if (dt.Rows.Count == 0) return;

        page = Math.Clamp(page, 0, (dt.Rows.Count - 1) / Pet.PetsPerPage);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = PetPageHelper.BuildPetsPageEmbed(dt, page, Username).Build();
            m.Components = PetPageHelper.BuildPetsPageButtons(UserId, page, dt.Rows.Count);
        });
    }
}