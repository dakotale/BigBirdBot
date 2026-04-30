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
        Assert.Equal(6, PersonaHelper.NamedPersonalities.Count);
    }
}
