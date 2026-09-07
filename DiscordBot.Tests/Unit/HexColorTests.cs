using DiscordBot.Helper;

namespace DiscordBot.Tests.Unit;

public class HexColorTests
{
    [Theory]
    [InlineData("#607C8C", 0x60, 0x7C, 0x8C)]
    [InlineData("607C8C", 0x60, 0x7C, 0x8C)]
    [InlineData("  #ff0000 ", 0xFF, 0x00, 0x00)]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC)]      // shorthand expands
    [InlineData("fff", 0xFF, 0xFF, 0xFF)]
    public void TryParse_ValidHex_ReturnsExpectedRgb(string input, int r, int g, int b)
    {
        Assert.True(HexColor.TryParse(input, out var c));
        Assert.Equal((r, g, b), ((int)c.R, (int)c.G, (int)c.B));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("cornflowerblue")]              // named colours are no longer supported
    public void TryParse_Invalid_ReturnsFalse(string input) =>
        Assert.False(HexColor.TryParse(input, out _));
}
