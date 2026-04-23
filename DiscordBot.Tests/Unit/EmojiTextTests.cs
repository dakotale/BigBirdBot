using DiscordBot.Misc;

namespace DiscordBot.Tests.Unit;

public class EmojiTextTests
{
    private readonly EmojiText _sut = new();

    // ── Empty / whitespace input ──────────────────────────────────────────────

    [Fact]
    public void GetEmojiString_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _sut.GetEmojiString(""));
    }

    [Fact]
    public void GetEmojiString_Space_ReturnsSingleSpace()
    {
        Assert.Equal(" ", _sut.GetEmojiString(" "));
    }

    [Fact]
    public void GetEmojiString_MultipleSpaces_PreservesSpaces()
    {
        string result = _sut.GetEmojiString("   ");
        Assert.Equal("   ", result);
    }

    // ── Special-cased letters (not :regional_indicator_X:) ───────────────────

    [Fact]
    public void GetEmojiString_LetterA_ProducesAEmoji()
    {
        Assert.Contains(":a:", _sut.GetEmojiString("a"));
    }

    [Fact]
    public void GetEmojiString_LetterB_ProducesBEmoji()
    {
        Assert.Contains(":b:", _sut.GetEmojiString("b"));
    }

    [Fact]
    public void GetEmojiString_LetterM_ProducesMEmoji()
    {
        Assert.Contains(":m:", _sut.GetEmojiString("m"));
    }

    [Fact]
    public void GetEmojiString_LetterO_ProducesOEmoji()
    {
        Assert.Contains(":o:", _sut.GetEmojiString("o"));
    }

    [Fact]
    public void GetEmojiString_LetterV_ProducesVEmoji()
    {
        Assert.Contains(":v:", _sut.GetEmojiString("v"));
    }

    [Fact]
    public void GetEmojiString_LetterX_ProducesXEmoji()
    {
        Assert.Contains(":x:", _sut.GetEmojiString("x"));
    }

    // ── :regional_indicator_X: letters ───────────────────────────────────────

    [Theory]
    [InlineData('c')]
    [InlineData('d')]
    [InlineData('e')]
    [InlineData('f')]
    [InlineData('g')]
    [InlineData('h')]
    [InlineData('i')]
    [InlineData('j')]
    [InlineData('k')]
    [InlineData('l')]
    [InlineData('n')]
    [InlineData('p')]
    [InlineData('q')]
    [InlineData('r')]
    [InlineData('s')]
    [InlineData('t')]
    [InlineData('u')]
    [InlineData('w')]
    [InlineData('y')]
    [InlineData('z')]
    public void GetEmojiString_RegionalIndicatorLetter_ContainsRegionalEmoji(char letter)
    {
        string result = _sut.GetEmojiString(letter.ToString());
        Assert.Contains($":regional_indicator_{letter}:", result);
    }

    // ── Digits ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData('0', "zero")]
    [InlineData('1', "one")]
    [InlineData('2', "two")]
    [InlineData('3', "three")]
    [InlineData('4', "four")]
    [InlineData('5', "five")]
    [InlineData('6', "six")]
    [InlineData('7', "seven")]
    [InlineData('8', "eight")]
    [InlineData('9', "nine")]
    public void GetEmojiString_Digit_ContainsSpelledName(char digit, string expectedWord)
    {
        string result = _sut.GetEmojiString(digit.ToString());
        Assert.Contains($":{expectedWord}:", result);
    }

    // ── Case folding ─────────────────────────────────────────────────────────

    [Fact]
    public void GetEmojiString_UppercaseA_TreatedSameAsLowercase()
    {
        Assert.Equal(_sut.GetEmojiString("a"), _sut.GetEmojiString("A"));
    }

    [Fact]
    public void GetEmojiString_UppercaseZ_TreatedSameAsLowercase()
    {
        Assert.Equal(_sut.GetEmojiString("z"), _sut.GetEmojiString("Z"));
    }

    [Fact]
    public void GetEmojiString_MixedCase_SameAsLowercase()
    {
        Assert.Equal(_sut.GetEmojiString("hello"), _sut.GetEmojiString("HELLO"));
    }

    // ── Unknown / unhandled characters are silently dropped ──────────────────

    [Fact]
    public void GetEmojiString_Punctuation_ProducesEmptyResult()
    {
        // Punctuation has no case entry — produces empty string
        string result = _sut.GetEmojiString("!@#$%^&*()-_=+[]{}|;':\",.<>?/`~\\");
        Assert.Equal("", result);
    }

    [Fact]
    public void GetEmojiString_Newline_Dropped()
    {
        string result = _sut.GetEmojiString("\n");
        Assert.Equal("", result);
    }

    [Fact]
    public void GetEmojiString_Tab_Dropped()
    {
        string result = _sut.GetEmojiString("\t");
        Assert.Equal("", result);
    }

    // ── Multi-character strings ───────────────────────────────────────────────

    [Fact]
    public void GetEmojiString_Word_ContainsEachLetterEmoji()
    {
        string result = _sut.GetEmojiString("hi");
        Assert.Contains(":regional_indicator_h:", result);
        Assert.Contains(":regional_indicator_i:", result);
    }

    [Fact]
    public void GetEmojiString_Hello_ContainsAllLetterEmojis()
    {
        string result = _sut.GetEmojiString("hello");
        Assert.Contains(":regional_indicator_h:", result);
        Assert.Contains(":regional_indicator_e:", result);
        Assert.Contains(":regional_indicator_l:", result);
        Assert.Contains(":o:", result);
    }

    [Fact]
    public void GetEmojiString_Abc_ContainsAllThreeEmojis()
    {
        string result = _sut.GetEmojiString("abc");
        Assert.Contains(":a:", result);
        Assert.Contains(":b:", result);
        Assert.Contains(":regional_indicator_c:", result);
    }

    [Fact]
    public void GetEmojiString_AllDigits_ContainsAllWordNames()
    {
        string result = _sut.GetEmojiString("0123456789");
        foreach (string word in new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" })
            Assert.Contains($":{word}:", result);
    }

    [Fact]
    public void GetEmojiString_SpacePreserved_BetweenLetters()
    {
        string result = _sut.GetEmojiString("a b");
        Assert.Contains(":a:", result);
        Assert.Contains(":b:", result);
        // The space itself is preserved as a literal space between emoji tokens
        Assert.Contains(" ", result);
    }

    // ── EmojiStr property side-effect ─────────────────────────────────────────

    [Fact]
    public void GetEmojiString_SetsEmojiStrProperty()
    {
        _sut.GetEmojiString("hello");
        Assert.Equal("hello", _sut.EmojiStr);
    }

    [Fact]
    public void GetEmojiString_SetsEmojiStrToOriginalInput_NotLowercased()
    {
        _sut.GetEmojiString("HELLO");
        // EmojiStr is set to the raw input before ToLower()
        Assert.Equal("HELLO", _sut.EmojiStr);
    }

    // ── All 26 letters produce non-empty output ───────────────────────────────

    [Theory]
    [InlineData("a")] [InlineData("b")] [InlineData("c")] [InlineData("d")]
    [InlineData("e")] [InlineData("f")] [InlineData("g")] [InlineData("h")]
    [InlineData("i")] [InlineData("j")] [InlineData("k")] [InlineData("l")]
    [InlineData("m")] [InlineData("n")] [InlineData("o")] [InlineData("p")]
    [InlineData("q")] [InlineData("r")] [InlineData("s")] [InlineData("t")]
    [InlineData("u")] [InlineData("v")] [InlineData("w")] [InlineData("x")]
    [InlineData("y")] [InlineData("z")]
    public void GetEmojiString_EachLetter_ProducesNonEmptyOutput(string letter)
    {
        Assert.False(string.IsNullOrEmpty(_sut.GetEmojiString(letter)));
    }

    // ── All digits produce non-empty output ───────────────────────────────────

    [Theory]
    [InlineData("0")] [InlineData("1")] [InlineData("2")] [InlineData("3")]
    [InlineData("4")] [InlineData("5")] [InlineData("6")] [InlineData("7")]
    [InlineData("8")] [InlineData("9")]
    public void GetEmojiString_EachDigit_ProducesNonEmptyOutput(string digit)
    {
        Assert.False(string.IsNullOrEmpty(_sut.GetEmojiString(digit)));
    }

    // ── Output format: each token is surrounded by spaces ─────────────────────

    [Fact]
    public void GetEmojiString_SingleLetter_HasLeadingAndTrailingSpaces()
    {
        string result = _sut.GetEmojiString("a");
        // Implementation appends " :a: " (space-colon-name-colon-space)
        Assert.StartsWith(" ", result);
        Assert.EndsWith(" ", result);
    }

    [Fact]
    public void GetEmojiString_SingleDigit_HasLeadingAndTrailingSpaces()
    {
        string result = _sut.GetEmojiString("5");
        Assert.StartsWith(" ", result);
        Assert.EndsWith(" ", result);
    }
}
