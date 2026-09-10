namespace DiscordBot.Tests.Unit;

public class PersonaHelperTests
{
    private const string DefaultPersona = "You are a friendly and helpful assistant.";

    // ── Default / unknown personality ─────────────────────────────────────────

    [Fact]
    public void ResolvePersona_None_ReturnsDefaultPersona()
    {
        Assert.Equal(DefaultPersona, PersonaHelper.ResolvePersona("None"));
    }

    [Fact]
    public void ResolvePersona_EmptyString_ReturnsDefaultPersona()
    {
        Assert.Equal(DefaultPersona, PersonaHelper.ResolvePersona(""));
    }

    [Fact]
    public void ResolvePersona_UnknownPersonality_ReturnsDefaultPersona()
    {
        Assert.Equal(DefaultPersona, PersonaHelper.ResolvePersona("eSports Gamer Lesbian"));
    }

    [Fact]
    public void ResolvePersona_RandomString_ReturnsDefaultPersona()
    {
        Assert.Equal(DefaultPersona, PersonaHelper.ResolvePersona("something_completely_unknown"));
    }

    // ── Each named personality returns a non-empty, non-default prompt ────────

    [Theory]
    [InlineData("Transfirmation")]
    [InlineData("Sett")]
    [InlineData("T. M. Opera O")]
    [InlineData("Meisho Doto")]
    [InlineData("Vi")]
    [InlineData("Cottagecore Witch")]
    public void ResolvePersona_NamedPersonality_ReturnsNonEmptyPrompt(string personality)
    {
        string result = PersonaHelper.ResolvePersona(personality);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(DefaultPersona, result);
    }

    // ── Matching is case-sensitive (personality values come from Discord choices) ─

    [Theory]
    [InlineData("transfirmation")]
    [InlineData("SETT")]
    [InlineData("vi")]
    [InlineData("cottagecore witch")]
    public void ResolvePersona_WrongCase_ReturnsDefaultPersona(string personality)
    {
        Assert.Equal(DefaultPersona, PersonaHelper.ResolvePersona(personality));
    }

    // ── Transfirmation — content covers expected topics ───────────────────────

    [Fact]
    public void ResolvePersona_Transfirmation_CoversTransfemAndTransmasc()
    {
        string result = PersonaHelper.ResolvePersona("Transfirmation");
        Assert.Contains("transfeminine", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transmasculine", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePersona_Transfirmation_MentionsKeyTransTopics()
    {
        string result = PersonaHelper.ResolvePersona("Transfirmation");
        Assert.Contains("HRT", result, StringComparison.Ordinal);
        Assert.Contains("coming out", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePersona_Transfirmation_IsAffirmingAndNonJudgmental()
    {
        string result = PersonaHelper.ResolvePersona("Transfirmation");
        Assert.Contains("positive", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("judgmental", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── NamedPersonalities list ───────────────────────────────────────────────

    [Fact]
    public void NamedPersonalities_ContainsTransfirmation()
    {
        Assert.Contains("Transfirmation", PersonaHelper.NamedPersonalities);
    }

    [Fact]
    public void NamedPersonalities_DoesNotContainRemovedPersonality()
    {
        Assert.DoesNotContain("eSports Gamer Lesbian", PersonaHelper.NamedPersonalities);
    }

    [Fact]
    public void NamedPersonalities_AllResolveToNonDefaultPrompt()
    {
        foreach (string name in PersonaHelper.NamedPersonalities)
        {
            string result = PersonaHelper.ResolvePersona(name);
            Assert.False(string.IsNullOrWhiteSpace(result), $"{name}: prompt is empty");
            Assert.True(result != DefaultPersona,            $"{name}: returned default prompt");
        }
    }

    [Fact]
    public void NamedPersonalities_CountIsCorrect()
    {
        Assert.Equal(18, PersonaHelper.NamedPersonalities.Count);
    }

    // ── SupportTopics / ChatPersonas partition NamedPersonalities ─────────────

    [Fact]
    public void SupportTopics_AndChatPersonas_PartitionNamedPersonalities()
    {
        var combined = PersonaHelper.SupportTopics
            .Concat(PersonaHelper.ChatPersonas)
            .ToList();

        Assert.Equal(PersonaHelper.NamedPersonalities.OrderBy(x => x), combined.OrderBy(x => x));
        Assert.Empty(PersonaHelper.SupportTopics.Intersect(PersonaHelper.ChatPersonas));
        Assert.Equal(13, PersonaHelper.SupportTopics.Count);
        Assert.Equal(5, PersonaHelper.ChatPersonas.Count);
    }

    [Fact]
    public void ChatPersonas_AreCharacterPersonas_NotSupportGuides()
    {
        Assert.DoesNotContain(PersonaHelper.ChatPersonas, p => p.Contains("Support Guide"));
    }

    [Fact]
    public void SupportTopics_AllResolveToNonDefaultPrompt()
    {
        foreach (string topic in PersonaHelper.SupportTopics)
        {
            string result = PersonaHelper.ResolvePersona(topic);
            Assert.False(string.IsNullOrWhiteSpace(result), $"{topic}: prompt is empty");
            Assert.NotEqual(DefaultPersona, result);
        }
    }

    // ── Every support topic carries crisis resources ─────────────────────────
    // Clinical guides get this via MentalHealthCore; the identity-affirming guides
    // (Bisexual/Gay/Queer/Transfirmation) get it via CrisisCore — previously they
    // had no crisis framing at all.

    [Theory]
    [InlineData("Bisexual Support Guide")]
    [InlineData("Gay Support Guide")]
    [InlineData("Queer Support Guide")]
    [InlineData("Transfirmation")]
    public void ResolvePersona_IdentityGuide_IncludesCrisisResources(string persona)
    {
        string result = PersonaHelper.ResolvePersona(persona);

        Assert.Contains("suicide or self-harm", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("988", result, StringComparison.Ordinal);
        Assert.Contains("116 123", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePersona_EverySupportTopic_IncludesCrisisResources()
    {
        foreach (string topic in PersonaHelper.SupportTopics)
        {
            string result = PersonaHelper.ResolvePersona(topic);
            Assert.Contains("988", result, StringComparison.Ordinal);
            Assert.Contains("116 123", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResolvePersona_CharacterPersona_HasNoCrisisFraming()
    {
        // Character personas are novelty roleplay, not support — they must not carry
        // the support-guide safety scaffolding.
        foreach (string persona in PersonaHelper.ChatPersonas)
        {
            string result = PersonaHelper.ResolvePersona(persona);
            Assert.DoesNotContain("988", result, StringComparison.Ordinal);
        }
    }

    // ── Mental-health support guides carry the safety framing ─────────────────

    [Theory]
    [InlineData("ADHD Support Guide")]
    [InlineData("Anxiety Support Guide")]
    [InlineData("Bipolar Support Guide")]
    [InlineData("BPD Support Guide")]
    [InlineData("Depression Support Guide")]
    [InlineData("Eating Disorder Recovery Guide")]
    [InlineData("OCD Support Guide")]
    [InlineData("PTSD & Trauma Support Guide")]
    [InlineData("Schizophrenia Support Guide")]
    public void ResolvePersona_MentalHealthGuide_IncludesSafetyFraming(string persona)
    {
        string result = PersonaHelper.ResolvePersona(persona);

        Assert.Contains("not therapy or medical advice", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never diagnose", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("988", result, StringComparison.Ordinal);                    // crisis line
        Assert.Contains("licensed mental-health professional", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-judgmental", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePersona_EatingDisorderGuide_RefusesToGiveNumbers()
    {
        string result = PersonaHelper.ResolvePersona("Eating Disorder Recovery Guide");
        Assert.Contains("never provide weight, calorie", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recovery", result, StringComparison.OrdinalIgnoreCase);
    }
}
