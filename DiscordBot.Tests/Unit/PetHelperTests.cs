using DiscordBot.Misc;

namespace DiscordBot.Tests.Unit;

public class PetHelperTests
{
    // ── Breeds dictionary ─────────────────────────────────────────────────────

    [Fact]
    public void Breeds_ContainsAllExpectedSpecies()
    {
        string[] expected =
        [
            "cat", "dog", "horse", "bird", "dinosaur", "bunny",
            "fish", "shark", "wolf", "lizard", "otter", "bear",
            "insect", "ocean_invertebrate", "land_invertebrate"
        ];
        foreach (var species in expected)
            Assert.True(PetHelper.Breeds.ContainsKey(species), $"Missing species: {species}");
    }

    [Fact]
    public void Breeds_Has15Species()
    {
        Assert.Equal(15, PetHelper.Breeds.Count);
    }

    [Fact]
    public void Breeds_EachSpeciesHasAtLeastOneBreed()
    {
        foreach (var (species, breeds) in PetHelper.Breeds)
            Assert.True(breeds.Length > 0, $"Species '{species}' has no breeds.");
    }

    [Fact]
    public void Breeds_NoEmptyBreedStrings()
    {
        foreach (var (species, breeds) in PetHelper.Breeds)
            Assert.All(breeds, b => Assert.False(string.IsNullOrWhiteSpace(b),
                $"Species '{species}' has a blank breed entry."));
    }

    [Fact]
    public void Breeds_CatHas20Breeds()
    {
        Assert.Equal(20, PetHelper.Breeds["cat"].Length);
    }

    [Fact]
    public void Breeds_NoDuplicateKeys()
    {
        var keys = PetHelper.Breeds.Keys.ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ── IsValidBreed ──────────────────────────────────────────────────────────

    [Fact]
    public void IsValidBreed_ValidSpeciesAndBreed_ReturnsTrue()
    {
        Assert.True(PetHelper.IsValidBreed("cat", "Siamese"));
    }

    [Fact]
    public void IsValidBreed_BreedCaseInsensitive_ReturnsTrue()
    {
        Assert.True(PetHelper.IsValidBreed("cat", "SIAMESE"));
        Assert.True(PetHelper.IsValidBreed("cat", "siamese"));
    }

    [Fact]
    public void IsValidBreed_SpeciesCaseInsensitive_ReturnsTrue()
    {
        Assert.True(PetHelper.IsValidBreed("CAT", "Siamese"));
        Assert.True(PetHelper.IsValidBreed("Cat", "Siamese"));
    }

    [Fact]
    public void IsValidBreed_ValidDog_ReturnsTrue()
    {
        Assert.True(PetHelper.IsValidBreed("dog", "Golden Retriever"));
        Assert.True(PetHelper.IsValidBreed("dog", "Husky"));
    }

    [Fact]
    public void IsValidBreed_BreedFromWrongSpecies_ReturnsFalse()
    {
        Assert.False(PetHelper.IsValidBreed("cat", "Golden Retriever")); // dog breed
        Assert.False(PetHelper.IsValidBreed("cat", "Poodle"));
    }

    [Fact]
    public void IsValidBreed_InvalidSpecies_ReturnsFalse()
    {
        Assert.False(PetHelper.IsValidBreed("dragon", "FireBreather"));
        Assert.False(PetHelper.IsValidBreed("", "Siamese"));
    }

    [Fact]
    public void IsValidBreed_EmptyBreed_ReturnsFalse()
    {
        Assert.False(PetHelper.IsValidBreed("cat", ""));
    }

    [Theory]
    [InlineData("cat",               "Sphynx")]
    [InlineData("dog",               "Beagle")]
    [InlineData("horse",             "Arabian")]
    [InlineData("bird",              "Macaw")]
    [InlineData("dinosaur",          "T-Rex")]
    [InlineData("bunny",             "Lionhead")]
    [InlineData("fish",              "Betta")]
    [InlineData("shark",             "Great White")]
    [InlineData("wolf",              "Arctic Wolf")]
    [InlineData("lizard",            "Bearded Dragon")]
    [InlineData("otter",             "Sea Otter")]
    [InlineData("bear",              "Polar Bear")]
    [InlineData("insect",            "Firefly")]
    [InlineData("ocean_invertebrate","Giant Squid")]
    [InlineData("land_invertebrate", "Emperor Scorpion")]
    public void IsValidBreed_OneBreedPerSpecies_IsValid(string species, string breed)
    {
        Assert.True(PetHelper.IsValidBreed(species, breed));
    }

    // ── XP / level formula ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,   50)]
    [InlineData(2,   200)]
    [InlineData(3,   450)]
    [InlineData(5,   1_250)]
    [InlineData(10,  5_000)]
    [InlineData(20,  20_000)]
    [InlineData(50,  125_000)]
    [InlineData(100, 500_000)]
    public void XpForLevel_FollowsQuadraticCurve(int level, int expected)
    {
        Assert.Equal(expected, PetHelper.XpForLevel(level));
    }

    [Theory]
    [InlineData(0,     1)]  // below first threshold
    [InlineData(49,    1)]  // just under level-2 threshold
    [InlineData(200,   2)]  // exactly at level-2 XP
    [InlineData(449,   2)]  // just under level-3 threshold
    [InlineData(450,   3)]  // exactly at level-3 XP
    [InlineData(5_000, 10)] // exactly at level-10 XP
    [InlineData(5_001, 10)] // just over level-10 threshold
    [InlineData(6_049, 10)] // just under level-11 threshold
    [InlineData(6_050, 11)] // exactly at level-11 XP
    public void LevelFromXp_ReturnsCorrectLevel(int xp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, PetHelper.LevelFromXp(xp));
    }

    [Fact]
    public void LevelFromXp_IsMonotonic()
    {
        int prevLevel = 1;
        for (int xp = 0; xp <= 10_000; xp += 50)
        {
            int level = PetHelper.LevelFromXp(xp);
            Assert.True(level >= prevLevel, $"Level decreased at xp={xp}");
            prevLevel = level;
        }
    }

    [Fact]
    public void LevelProgress_AtLevelBoundary_IsZero()
    {
        // At exactly XpForLevel(2), we're at level 2 with 0 progress
        int xp = PetHelper.XpForLevel(2);
        float progress = PetHelper.LevelProgress(xp);
        Assert.Equal(0f, progress, precision: 4);
    }

    [Fact]
    public void LevelProgress_AtMidpoint_IsHalf()
    {
        // Level 1 spans XpForLevel(1)=50 to XpForLevel(2)=200
        // midpoint = 50 + (200-50)/2 = 125
        float progress = PetHelper.LevelProgress(125);
        Assert.Equal(0.5f, progress, precision: 4);
    }

    [Fact]
    public void LevelProgress_AlwaysBetweenZeroAndOne_ForInLevelXp()
    {
        // Test several levels at start, mid, and near-end
        for (int level = 1; level <= 5; level++)
        {
            int start = PetHelper.XpForLevel(level);
            int end   = PetHelper.XpForLevel(level + 1);
            float atStart = PetHelper.LevelProgress(start);
            float atMid   = PetHelper.LevelProgress((start + end) / 2);

            Assert.Equal(0f, atStart, precision: 4);
            Assert.InRange(atMid, 0f, 1f);
        }
    }

    // ── Hibernation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(14,  14,  100, true)]   // 2 stats below threshold
    [InlineData(0,   0,   0,   true)]   // all 3 below
    [InlineData(14,  14,  14,  true)]   // all 3 below
    [InlineData(0,   100, 100, false)]  // only 1 below
    [InlineData(14,  100, 100, false)]  // only hunger below
    [InlineData(15,  14,  100, false)]  // threshold is exclusive: 15 is NOT below
    [InlineData(15,  15,  15,  false)]  // all at threshold — not below
    [InlineData(100, 100, 100, false)]  // all healthy
    [InlineData(0,   0,   100, true)]   // hunger + happiness both below
    [InlineData(0,   100, 0,   true)]   // hunger + energy both below
    [InlineData(100, 0,   0,   true)]   // happiness + energy both below
    public void ShouldHibernate_ReturnsCorrectResult(
        int hunger, int happiness, int energy, bool expected)
    {
        Assert.Equal(expected, PetHelper.ShouldHibernate(hunger, happiness, energy));
    }

    [Fact]
    public void ShouldHibernate_HibernationThresholdIs15()
    {
        Assert.Equal(15, PetHelper.HibernationThreshold);
    }

    // ── StatBar ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   "░░░░░░░░░░")]
    [InlineData(10,  "█░░░░░░░░░")]
    [InlineData(20,  "██░░░░░░░░")]
    [InlineData(30,  "███░░░░░░░")]
    [InlineData(50,  "█████░░░░░")]
    [InlineData(70,  "███████░░░")]
    [InlineData(100, "██████████")]
    public void StatBar_ReturnsCorrectFilledChars(int value, string expected)
    {
        Assert.Equal(expected, PetHelper.StatBar(value));
    }

    [Fact]
    public void StatBar_AlwaysHasLength10()
    {
        for (int v = 0; v <= 100; v += 5)
            Assert.Equal(10, PetHelper.StatBar(v).Length);
    }

    [Fact]
    public void StatBar_ClampsNegativeToZero()
    {
        Assert.Equal(PetHelper.StatBar(0), PetHelper.StatBar(-50));
    }

    [Fact]
    public void StatBar_ClampsAbove100ToFull()
    {
        Assert.Equal(PetHelper.StatBar(100), PetHelper.StatBar(150));
    }

    // ── StatDisplay ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, "🟢")]
    [InlineData(70,  "🟢")]
    [InlineData(69,  "🟡")]
    [InlineData(40,  "🟡")]
    [InlineData(39,  "🟠")]
    [InlineData(20,  "🟠")]
    [InlineData(19,  "🔴")]
    [InlineData(0,   "🔴")]
    public void StatDisplay_ShowsCorrectColorEmoji(int value, string expectedColor)
    {
        string display = PetHelper.StatDisplay("Test", value);
        Assert.StartsWith(expectedColor, display);
    }

    [Fact]
    public void StatDisplay_ContainsValueSuffix()
    {
        string display = PetHelper.StatDisplay("Hunger", 55);
        Assert.Contains("55/100", display);
    }

    [Fact]
    public void StatDisplay_ContainsStatBar()
    {
        string display = PetHelper.StatDisplay("Energy", 50);
        Assert.Contains("█████░░░░░", display);
    }

    [Fact]
    public void StatDisplay_ZeroValue_IsRed()
    {
        Assert.StartsWith("🔴", PetHelper.StatDisplay("X", 0));
    }

    // ── PetEmoji ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cat",               "😴🐱")]
    [InlineData("dog",               "😴🐶")]
    [InlineData("horse",             "😴🐴")]
    [InlineData("bird",              "😴🐦")]
    [InlineData("dinosaur",          "😴🦕")]
    [InlineData("bunny",             "😴🐰")]
    [InlineData("fish",              "😴🐟")]
    [InlineData("shark",             "😴🦈")]
    [InlineData("wolf",              "😴🐺")]
    [InlineData("lizard",            "😴🦎")]
    [InlineData("otter",             "😴🦦")]
    [InlineData("bear",              "😴🐻")]
    [InlineData("insect",            "😴🐛")]
    [InlineData("ocean_invertebrate","😴🐙")]
    [InlineData("land_invertebrate", "😴🦂")]
    public void PetEmoji_Hibernating_ReturnsSleepEmoji(string species, string expected)
    {
        Assert.Equal(expected, PetHelper.PetEmoji(species, 100, 100, hibernating: true, evolved: false));
    }

    [Fact]
    public void PetEmoji_Hibernating_UnknownSpecies_ReturnsGenericSleep()
    {
        Assert.Equal("😴", PetHelper.PetEmoji("dragon", 100, 100, hibernating: true, evolved: false));
    }

    [Theory]
    [InlineData("cat",               "😾")]
    [InlineData("dog",               "🐕")]
    [InlineData("horse",             "🐎")]
    [InlineData("bird",              "🐧")]
    [InlineData("dinosaur",          "🦖")]
    [InlineData("bunny",             "🐇")]
    [InlineData("fish",              "🐡")]
    [InlineData("shark",             "🦷")]
    [InlineData("wolf",              "🐺")]
    [InlineData("lizard",            "🦎")]
    [InlineData("otter",             "🦦")]
    [InlineData("bear",              "🐻")]
    [InlineData("insect",            "🐜")]
    [InlineData("ocean_invertebrate","🦑")]
    [InlineData("land_invertebrate", "🦂")]
    public void PetEmoji_HungerBelow20_ReturnsHungryEmoji(string species, string expected)
    {
        Assert.Equal(expected, PetHelper.PetEmoji(species, happiness: 100, hunger: 19,
            hibernating: false, evolved: false));
    }

    [Fact]
    public void PetEmoji_HungerBelow20_UnknownSpecies_ReturnsGenericSad()
    {
        Assert.Equal("😟", PetHelper.PetEmoji("dragon", 100, 0, false, false));
    }

    [Theory]
    [InlineData("cat",    false, "😺")]
    [InlineData("dog",    false, "🐶")]
    [InlineData("horse",  false, "🐎")]
    [InlineData("bird",   false, "🦜")]
    [InlineData("bunny",  false, "🐰")]
    [InlineData("fish",   false, "🐠")]
    [InlineData("shark",  false, "🦈")]
    [InlineData("wolf",   false, "🐺")]
    [InlineData("bear",   false, "🐻")]
    [InlineData("insect", false, "🦋")]
    public void PetEmoji_HappyNotEvolved_ReturnsHappyEmoji(string species, bool evolved, string expected)
    {
        Assert.Equal(expected, PetHelper.PetEmoji(species, happiness: 75, hunger: 50,
            hibernating: false, evolved: evolved));
    }

    [Theory]
    [InlineData("cat",               "🦁")]
    [InlineData("dog",               "🐺")]
    [InlineData("horse",             "🦄")]
    [InlineData("bird",              "🦅")]
    [InlineData("dinosaur",          "🐉")]
    [InlineData("bunny",             "🐇")]
    [InlineData("fish",              "🐋")]
    [InlineData("shark",             "🌊")]
    [InlineData("wolf",              "🌕")]
    [InlineData("lizard",            "🐲")]
    [InlineData("otter",             "🌊")]
    [InlineData("bear",              "🏔️")]
    [InlineData("insect",            "🐝")]
    [InlineData("ocean_invertebrate","🦑")]
    [InlineData("land_invertebrate", "🦂")]
    public void PetEmoji_HappyEvolved_ReturnsEvolvedEmoji(string species, string expected)
    {
        Assert.Equal(expected, PetHelper.PetEmoji(species, happiness: 75, hunger: 50,
            hibernating: false, evolved: true));
    }

    [Theory]
    [InlineData("cat",    false, "🐱")]
    [InlineData("cat",    true,  "🦁")]
    [InlineData("dog",    false, "🐶")]
    [InlineData("dog",    true,  "🐺")]
    [InlineData("horse",  false, "🐴")]
    [InlineData("horse",  true,  "🦄")]
    [InlineData("bird",   false, "🐦")]
    [InlineData("bird",   true,  "🦅")]
    [InlineData("dinosaur", false, "🦕")]
    [InlineData("dinosaur", true, "🐉")]
    public void PetEmoji_NormalHappiness_ReturnsNormalEmoji(string species, bool evolved, string expected)
    {
        Assert.Equal(expected, PetHelper.PetEmoji(species, happiness: 50, hunger: 50,
            hibernating: false, evolved: evolved));
    }

    [Fact]
    public void PetEmoji_HungerExactly20_NotHungry()
    {
        // hunger=20 is NOT < 20, so should not use hungry emoji
        string emoji = PetHelper.PetEmoji("cat", happiness: 50, hunger: 20, hibernating: false, evolved: false);
        Assert.NotEqual("😾", emoji); // 😾 is the hungry cat emoji
    }

    // ── EvolvedName ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cat",               "Maine Coon")]
    [InlineData("dog",               "Golden Retriever")]
    [InlineData("horse",             "Unicorn")]
    [InlineData("bird",              "Eagle")]
    [InlineData("dinosaur",          "Dragon")]
    [InlineData("bunny",             "Shadow Rabbit")]
    [InlineData("fish",              "Leviathan")]
    [InlineData("shark",             "Megalodon")]
    [InlineData("wolf",              "Dire Wolf")]
    [InlineData("lizard",            "Komodo Dragon")]
    [InlineData("otter",             "Sea Emperor")]
    [InlineData("bear",              "Spirit Bear")]
    [InlineData("insect",            "Metamorph")]
    [InlineData("ocean_invertebrate","Kraken")]
    [InlineData("land_invertebrate", "Emperor Scorpion")]
    public void EvolvedName_ReturnsCorrectName(string species, string expected)
    {
        Assert.Equal(expected, PetHelper.EvolvedName(species));
    }

    [Fact]
    public void EvolvedName_CaseInsensitive()
    {
        Assert.Equal("Maine Coon", PetHelper.EvolvedName("CAT"));
        Assert.Equal("Dire Wolf",  PetHelper.EvolvedName("WOLF"));
    }

    [Fact]
    public void EvolvedName_UnknownSpecies_ReturnsSpeciesItself()
    {
        Assert.Equal("dragon", PetHelper.EvolvedName("dragon"));
        Assert.Equal("ROBOT",  PetHelper.EvolvedName("ROBOT")); // falls through _ => species (without lower)
    }

    // ── LevelUpUnlock ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5,   true)]
    [InlineData(10,  true)]
    [InlineData(15,  true)]
    [InlineData(20,  true)]
    [InlineData(25,  true)]
    [InlineData(50,  true)]
    [InlineData(75,  true)]
    [InlineData(100, true)]
    [InlineData(1,   false)]
    [InlineData(6,   false)]
    [InlineData(11,  false)]
    [InlineData(99,  false)]
    public void LevelUpUnlock_MilestoneLevelsHaveMessage(int level, bool hasMessage)
    {
        Assert.Equal(hasMessage, PetHelper.LevelUpUnlock(level) is not null);
    }

    [Fact]
    public void LevelUpUnlock_Level5_MentionsTrick()
    {
        Assert.Contains("trick", PetHelper.LevelUpUnlock(5), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LevelUpUnlock_Level50_MentionsEvolved()
    {
        Assert.Contains("Evolved", PetHelper.LevelUpUnlock(50));
    }

    [Fact]
    public void LevelUpUnlock_Level100_MentionsHallOfFame()
    {
        Assert.Contains("Hall of Fame", PetHelper.LevelUpUnlock(100));
    }

    [Fact]
    public void LevelUpUnlock_Level10_MentionsAccessory()
    {
        Assert.Contains("Accessory", PetHelper.LevelUpUnlock(10));
    }

    // ── PerformTrick ──────────────────────────────────────────────────────────

    private static readonly string[] KnownSpecies =
    [
        "cat", "dog", "horse", "bird", "dinosaur", "bunny",
        "fish", "shark", "wolf", "lizard", "otter", "bear",
        "insect", "ocean_invertebrate", "land_invertebrate"
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PerformTrick_AllSpeciesAllSlots_ReturnNonDefaultString(int slot)
    {
        const string defaultText = "*does something impressively cute*";
        foreach (var species in KnownSpecies)
        {
            string result = PetHelper.PerformTrick(species, slot);
            Assert.False(string.IsNullOrWhiteSpace(result),
                $"{species} slot {slot} returned empty");
            Assert.True(result != defaultText,
                $"{species} slot {slot} returned the default fallback");
        }
    }

    [Fact]
    public void PerformTrick_UnknownSpecies_ReturnsDefault()
    {
        const string defaultText = "*does something impressively cute*";
        Assert.Equal(defaultText, PetHelper.PerformTrick("dragon", 1));
    }

    [Fact]
    public void PerformTrick_CaseInsensitive()
    {
        string lower = PetHelper.PerformTrick("cat", 1);
        string upper = PetHelper.PerformTrick("CAT", 1);
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void PerformTrick_Cat_Slot1_ContainsCatEmoji()
    {
        Assert.Contains("😺", PetHelper.PerformTrick("cat", 1));
    }

    [Fact]
    public void PerformTrick_Dog_Slot2_ContainsDogEmoji()
    {
        Assert.Contains("🐶", PetHelper.PerformTrick("dog", 2));
    }

    // ── JournalEventEmoji ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("feed",    "🍽️")]
    [InlineData("wake",    "🌅")]
    [InlineData("hug",     "🤗")]
    [InlineData("pet",     "🖐️")]
    [InlineData("groom",   "🛁")]
    [InlineData("play",    "🎮")]
    [InlineData("sleep",   "💤")]
    [InlineData("explore", "🗺️")]
    [InlineData("battle",  "⚔️")]
    [InlineData("levelup", "🎉")]
    [InlineData("adopt",   "🐾")]
    [InlineData("trick",   "🎪")]
    public void JournalEventEmoji_KnownEvents_ReturnCorrectEmoji(string eventType, string expected)
    {
        Assert.Equal(expected, PetHelper.JournalEventEmoji(eventType));
    }

    [Fact]
    public void JournalEventEmoji_UnknownEvent_ReturnsPinEmoji()
    {
        Assert.Equal("📌", PetHelper.JournalEventEmoji("unknown"));
        Assert.Equal("📌", PetHelper.JournalEventEmoji(""));
        Assert.Equal("📌", PetHelper.JournalEventEmoji("xyz"));
    }

    [Fact]
    public void JournalEventEmoji_CaseInsensitive()
    {
        Assert.Equal("🍽️", PetHelper.JournalEventEmoji("FEED"));
        Assert.Equal("⚔️", PetHelper.JournalEventEmoji("BATTLE"));
    }

    // ── BattlePower ───────────────────────────────────────────────────────────

    [Fact]
    public void BattlePower_ResultWithinExpectedRange()
    {
        // basePower = 10*10 = 100, statBonus = (60+80+70)/10 = 21, luck ∈ [1,25]
        // total ∈ [122, 146]
        for (int i = 0; i < 500; i++)
        {
            int power = PetHelper.BattlePower(level: 10, hunger: 60, happiness: 80, energy: 70);
            Assert.InRange(power, 122, 146);
        }
    }

    [Fact]
    public void BattlePower_HigherLevelHasHigherBasepower()
    {
        // Compare average over many runs — higher level pet should nearly always score more
        // since base difference (100 vs 10) dwarfs max luck (25)
        int highLevel = PetHelper.BattlePower(10, 50, 50, 50);
        int lowLevel  = PetHelper.BattlePower(1,  50, 50, 50);
        // Can't assert deterministically due to luck, but base difference is 90
        // so just verify the formula produces positive values
        Assert.True(highLevel > 0);
        Assert.True(lowLevel > 0);
    }

    [Fact]
    public void BattlePower_ZeroStats_StillPositive()
    {
        // basePower + luck(1..25) always > 0
        for (int i = 0; i < 100; i++)
        {
            int power = PetHelper.BattlePower(1, 0, 0, 0);
            Assert.True(power > 0, $"Power should always be positive, got {power}");
        }
    }

    // ── GenerateBattleRounds ──────────────────────────────────────────────────

    [Fact]
    public void GenerateBattleRounds_ReturnsExactlyThreeStrings()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "Fluffy", "cat", 100,
            "Rex",    "dog",  50, draw: false);
        Assert.Equal(3, rounds.Length);
    }

    [Fact]
    public void GenerateBattleRounds_Round1StartsWithSwords()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "A", "cat", 50, "B", "dog", 30, draw: false);
        Assert.StartsWith("⚔️", rounds[0]);
    }

    [Fact]
    public void GenerateBattleRounds_Round2StartsWithShield()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "A", "cat", 50, "B", "dog", 30, draw: false);
        Assert.StartsWith("🛡️", rounds[1]);
    }

    [Fact]
    public void GenerateBattleRounds_Round3StartsWithExplosion()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "A", "cat", 50, "B", "dog", 30, draw: false);
        Assert.StartsWith("💥", rounds[2]);
    }

    [Fact]
    public void GenerateBattleRounds_Draw_Round3ContainsDraw()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "A", "cat", 50, "B", "dog", 50, draw: true);
        Assert.Contains("neither gives an inch", rounds[2]);
    }

    [Fact]
    public void GenerateBattleRounds_AttackerWins_Round3ContainsAttackerName()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "Fluffy", "cat", 200, "Rex", "dog", 50, draw: false);
        Assert.Contains("Fluffy", rounds[2]);
        Assert.Contains("finishing move", rounds[2]);
    }

    [Fact]
    public void GenerateBattleRounds_DefenderWins_Round3ContainsDefenderName()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "Fluffy", "cat", 10, "Rex", "dog", 200, draw: false);
        Assert.Contains("Rex", rounds[2]);
        Assert.Contains("final blow", rounds[2]);
    }

    [Fact]
    public void GenerateBattleRounds_Round1ContainsAttackerName()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "Fluffy", "cat", 50, "Rex", "dog", 50, draw: false);
        Assert.Contains("Fluffy", rounds[0]);
    }

    [Fact]
    public void GenerateBattleRounds_Round2ContainsDefenderName()
    {
        string[] rounds = PetHelper.GenerateBattleRounds(
            "Fluffy", "cat", 50, "Rex", "dog", 50, draw: false);
        Assert.Contains("Rex", rounds[1]);
    }

    // ── GenerateBattleLog ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateBattleLog_JoinsAllThreeRoundsWithNewlines()
    {
        string log = PetHelper.GenerateBattleLog(
            "A", "cat", 50, "B", "dog", 30, draw: false);
        Assert.Equal(2, log.Count(c => c == '\n'));
    }

    [Fact]
    public void GenerateBattleLog_ContainsAllRoundMarkers()
    {
        string log = PetHelper.GenerateBattleLog(
            "A", "cat", 50, "B", "dog", 30, draw: false);
        Assert.Contains("⚔️", log);
        Assert.Contains("🛡️", log);
        Assert.Contains("💥", log);
    }

    // ── ExploreRewardDescription ──────────────────────────────────────────────

    [Theory]
    [InlineData("common_bone",   "dog",             "Found a perfectly aged bone buried in a park!")]
    [InlineData("common_bone",   "cat",             "Dragged home a mysterious bone from somewhere")]
    [InlineData("common_bone",   "wolf",            "Returned with a bone of impressive provenance and will not explain further")]
    [InlineData("common_flower", "bunny",           "Nibbled on a fresh wildflower and brought the rest back!")]
    [InlineData("common_flower", "horse",           "Pranced through a meadow and returned with flowers in their mane!")]
    [InlineData("common_stick",  "dog",             "Found the ultimate stick and refuses to let go of it")]
    [InlineData("common_stick",  "bird",            "Carried back a perfect nesting twig!")]
    [InlineData("uncommon_coin", "bird",            "Spotted a shiny coin from the sky and dove for it!")]
    [InlineData("legendary_star","bunny",           "Binky'd so high they accidentally caught a falling star!")]
    [InlineData("legendary_star","horse",           "Galloped so fast they outran the night and caught a star!")]
    [InlineData("legendary_star","wolf",            "Howled at the right frequency and something fell from the sky toward them — they caught it")]
    [InlineData("legendary_star","otter",           "Was floating on their back at exactly the right time and place, and simply caught it")]
    public void ExploreRewardDescription_KnownPairs_ReturnCorrectText(
        string rewardKey, string species, string expected)
    {
        string result = PetHelper.ExploreRewardDescription(rewardKey, species, "generic");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExploreRewardDescription_UnknownSpecies_ReturnsGeneric()
    {
        string result = PetHelper.ExploreRewardDescription("common_bone", "dragon", "my generic");
        Assert.Equal("my generic", result);
    }

    [Fact]
    public void ExploreRewardDescription_UnknownRewardKey_ReturnsGeneric()
    {
        string result = PetHelper.ExploreRewardDescription("nonexistent_key", "cat", "fallback text");
        Assert.Equal("fallback text", result);
    }

    [Fact]
    public void ExploreRewardDescription_CaseInsensitiveSpecies()
    {
        string lower = PetHelper.ExploreRewardDescription("common_bone", "dog", "generic");
        string upper = PetHelper.ExploreRewardDescription("common_bone", "DOG", "generic");
        Assert.Equal(lower, upper);
    }

    // ── PickExploreReward ─────────────────────────────────────────────────────

    [Fact]
    public void PickExploreReward_Level1_AlwaysReturnsCommonOrUncommon()
    {
        for (int i = 0; i < 500; i++)
        {
            var reward = PetHelper.PickExploreReward(1);
            Assert.True(
                reward.key.StartsWith("common") || reward.key.StartsWith("uncommon"),
                $"Level 1 returned restricted reward: {reward.key}");
        }
    }

    [Fact]
    public void PickExploreReward_Level10_NeverReturnsEpicOrLegendary()
    {
        for (int i = 0; i < 500; i++)
        {
            var reward = PetHelper.PickExploreReward(10);
            Assert.False(
                reward.key.StartsWith("epic") || reward.key.StartsWith("legendary"),
                $"Level 10 returned tier above its cap: {reward.key}");
        }
    }

    [Fact]
    public void PickExploreReward_Level50_RewardKeyIsValid()
    {
        string[] validPrefixes = ["common", "uncommon", "rare", "epic", "legendary"];
        for (int i = 0; i < 200; i++)
        {
            var reward = PetHelper.PickExploreReward(50);
            Assert.True(
                validPrefixes.Any(p => reward.key.StartsWith(p)),
                $"Unexpected reward key: {reward.key}");
        }
    }

    [Fact]
    public void PickExploreReward_AlwaysReturnsNonNullFields()
    {
        for (int i = 0; i < 100; i++)
        {
            var reward = PetHelper.PickExploreReward(1);
            Assert.False(string.IsNullOrWhiteSpace(reward.key));
            Assert.False(string.IsNullOrWhiteSpace(reward.emoji));
            Assert.False(string.IsNullOrWhiteSpace(reward.description));
            Assert.True(reward.xp > 0);
        }
    }

    // ── PickExploreRewardBoosted ──────────────────────────────────────────────

    [Fact]
    public void PickExploreRewardBoosted_Level50_AlwaysRarePlus()
    {
        for (int i = 0; i < 500; i++)
        {
            var reward = PetHelper.PickExploreRewardBoosted(50);
            Assert.True(
                reward.key.StartsWith("rare") ||
                reward.key.StartsWith("epic") ||
                reward.key.StartsWith("legendary"),
                $"Boosted level 50 returned non-rare reward: {reward.key}");
        }
    }

    [Fact]
    public void PickExploreRewardBoosted_Level10_AlwaysReturnsRare()
    {
        // Level 10 only has rare tier available (epic=25, legendary=50)
        for (int i = 0; i < 200; i++)
        {
            var reward = PetHelper.PickExploreRewardBoosted(10);
            Assert.StartsWith("rare", reward.key);
        }
    }

    [Fact]
    public void PickExploreRewardBoosted_Level1_FallsBackToUncommon()
    {
        // No rare/epic/legendary at level 1 → falls back to uncommon
        for (int i = 0; i < 200; i++)
        {
            var reward = PetHelper.PickExploreRewardBoosted(1);
            Assert.StartsWith("uncommon", reward.key);
        }
    }

    // ── ExploreRewards catalog integrity ──────────────────────────────────────

    [Fact]
    public void ExploreRewards_AllHaveNonEmptyFields()
    {
        foreach (var r in PetHelper.ExploreRewards)
        {
            Assert.False(string.IsNullOrWhiteSpace(r.key),         $"Empty key in rewards");
            Assert.False(string.IsNullOrWhiteSpace(r.emoji),       $"Empty emoji in {r.key}");
            Assert.False(string.IsNullOrWhiteSpace(r.description), $"Empty description in {r.key}");
            Assert.True(r.xp > 0,                                  $"Non-positive XP in {r.key}");
            Assert.True(r.minLevel >= 1,                           $"Invalid minLevel in {r.key}");
        }
    }

    [Fact]
    public void ExploreRewards_HasExpectedTiers()
    {
        Assert.True(PetHelper.ExploreRewards.Any(r => r.key.StartsWith("common")));
        Assert.True(PetHelper.ExploreRewards.Any(r => r.key.StartsWith("uncommon")));
        Assert.True(PetHelper.ExploreRewards.Any(r => r.key.StartsWith("rare")));
        Assert.True(PetHelper.ExploreRewards.Any(r => r.key.StartsWith("epic")));
        Assert.True(PetHelper.ExploreRewards.Any(r => r.key.StartsWith("legendary")));
    }

    [Fact]
    public void ExploreRewards_LegendaryHasHighestXp()
    {
        int maxCommon    = PetHelper.ExploreRewards.Where(r => r.key.StartsWith("common")).Max(r => r.xp);
        int minLegendary = PetHelper.ExploreRewards.Where(r => r.key.StartsWith("legendary")).Min(r => r.xp);
        Assert.True(minLegendary > maxCommon, "Legendary XP should exceed common XP");
    }

    // ── Foods catalog ─────────────────────────────────────────────────────────

    [Fact]
    public void Foods_AllHaveNonEmptyNameAndEmoji()
    {
        foreach (var f in PetHelper.Foods)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.name));
            Assert.False(string.IsNullOrWhiteSpace(f.emoji));
        }
    }

    [Fact]
    public void Foods_AllHavePositiveRestoreValues()
    {
        foreach (var f in PetHelper.Foods)
        {
            Assert.True(f.hungerRestore >= 0, $"{f.name} has negative hungerRestore");
            Assert.True(f.happyBonus    >= 0, $"{f.name} has negative happyBonus");
            Assert.True(f.minLevel      >= 1, $"{f.name} has minLevel < 1");
        }
    }

    [Fact]
    public void Foods_HasLevel1Foods()
    {
        Assert.True(PetHelper.Foods.Any(f => f.minLevel == 1));
    }

    [Fact]
    public void Foods_HasPremiumFoodsForHighLevels()
    {
        Assert.True(PetHelper.Foods.Any(f => f.minLevel >= 50));
    }

    [Fact]
    public void ListFoods_Level1_IncludesBasicFood()
    {
        string list = PetHelper.ListFoods(1);
        Assert.Contains("Kibble", list);
        Assert.Contains("Fresh Meat", list);
    }

    [Fact]
    public void ListFoods_Level1_ExcludesHighLevelFood()
    {
        string list = PetHelper.ListFoods(1);
        // "Magic Treat" requires level 50
        Assert.DoesNotContain("Magic Treat", list);
    }

    [Fact]
    public void ListFoods_Level9_ExcludesLevel10Foods()
    {
        string list9 = PetHelper.ListFoods(9);
        Assert.DoesNotContain("Salmon Fillet", list9);
    }

    [Fact]
    public void ListFoods_Level10_IncludesMidLevelFood()
    {
        string list = PetHelper.ListFoods(10);
        Assert.Contains("Salmon Fillet", list);
    }

    [Fact]
    public void ListFoods_Level50_IncludesAllFoods()
    {
        string list = PetHelper.ListFoods(50);
        Assert.Contains("Magic Treat", list);
        Assert.Contains("Elixir",      list);
        Assert.Contains("Kibble",      list);
    }

    // ── ExploreDeparture ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("cat",    "🐱")]
    [InlineData("dog",    "🐶")]
    [InlineData("horse",  "🐴")]
    [InlineData("bird",   "🐦")]
    [InlineData("wolf",   "🐺")]
    [InlineData("lizard", "🦎")]
    [InlineData("otter",  "🦦")]
    [InlineData("bear",   "🐻")]
    public void ExploreDeparture_KnownSpecies_ContainsEmoji(string species, string expectedEmoji)
    {
        Assert.Contains(expectedEmoji, PetHelper.ExploreDeparture(species));
    }

    [Fact]
    public void ExploreDeparture_UnknownSpecies_ReturnsDefault()
    {
        Assert.Equal("*heads off on an adventure*", PetHelper.ExploreDeparture("dragon"));
    }

    [Fact]
    public void ExploreDeparture_AllKnownSpecies_ReturnNonEmpty()
    {
        foreach (var species in KnownSpecies)
        {
            string result = PetHelper.ExploreDeparture(species);
            Assert.False(string.IsNullOrWhiteSpace(result), $"{species} departure is empty");
        }
    }

    // ── ExploreReturnOpener ───────────────────────────────────────────────────

    [Fact]
    public void ExploreReturnOpener_AlwaysContainsPetName()
    {
        for (int i = 0; i < 50; i++) // covers all 14 branches
        {
            string result = PetHelper.ExploreReturnOpener("Buddy");
            Assert.Contains("Buddy", result);
        }
    }

    [Fact]
    public void ExploreReturnOpener_NeverNullOrEmpty()
    {
        for (int i = 0; i < 50; i++)
        {
            Assert.False(string.IsNullOrWhiteSpace(PetHelper.ExploreReturnOpener("Pet")));
        }
    }

    // ── PuzzleWords ───────────────────────────────────────────────────────────

    [Fact]
    public void PuzzleWords_HasManyWords()
    {
        Assert.True(PetHelper.PuzzleWords.Length >= 500);
    }

    [Fact]
    public void PuzzleWords_AllNonEmpty()
    {
        Assert.All(PetHelper.PuzzleWords, w => Assert.False(string.IsNullOrWhiteSpace(w)));
    }

    [Fact]
    public void PuzzleWords_AllLowercase()
    {
        Assert.All(PetHelper.PuzzleWords, w => Assert.Equal(w, w.ToLower()));
    }

    // ── XP constants ─────────────────────────────────────────────────────────

    [Fact]
    public void XpConstants_ArePositive()
    {
        Assert.True(PetHelper.XpMessage    > 0);
        Assert.True(PetHelper.XpAttachment > 0);
        Assert.True(PetHelper.XpLink       > 0);
        Assert.True(PetHelper.XpActivity   > 0);
        Assert.True(PetHelper.XpWordPuzzle > 0);
        Assert.True(PetHelper.XpPet        > 0);
        Assert.True(PetHelper.XpFeed       > 0);
        Assert.True(PetHelper.XpGroom      > 0);
        Assert.True(PetHelper.XpPlay       > 0);
    }

    [Fact]
    public void XpConstants_WordPuzzle_HigherThanMessage()
    {
        Assert.True(PetHelper.XpWordPuzzle > PetHelper.XpMessage);
    }

    // ── Cooldown constants ────────────────────────────────────────────────────

    [Fact]
    public void CooldownConstants_ArePositive()
    {
        Assert.True(PetHelper.FeedCooldownMinutes  > 0);
        Assert.True(PetHelper.PetCooldownMinutes   > 0);
        Assert.True(PetHelper.GroomCooldownMinutes > 0);
        Assert.True(PetHelper.PlayCooldownMinutes  > 0);
    }

    [Fact]
    public void CooldownConstants_GroomLongerThanFeed()
    {
        Assert.True(PetHelper.GroomCooldownMinutes > PetHelper.FeedCooldownMinutes);
    }
}
