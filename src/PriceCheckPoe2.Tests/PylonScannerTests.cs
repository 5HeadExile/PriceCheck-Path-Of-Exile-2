using PriceCheckPoe2.Scanning;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class PylonScannerTests
{
    [Theory]
    [InlineData("5x Exalted Orb", 5, "Exalted Orb")]
    [InlineData("3x Greater Orb of Transmutation", 3, "Greater Orb of Transmutation")]
    [InlineData("3xGemcutter's Prism", 3, "Gemcutter's Prism")]   // склеено, без пробела
    [InlineData("3 Divine Orb", 3, "Divine Orb")]
    [InlineData("Chaos Orb x12", 12, "Chaos Orb")]
    public void SplitCount_ExtractsStack(string line, int stack, string text)
    {
        var (parsedStack, parsedText) = PylonScanner.SplitCount(line);
        Assert.Equal(stack, parsedStack);
        Assert.Equal(text, parsedText);
    }

    [Theory]
    [InlineData("Mirror of Kalandra")]
    [InlineData("Orb of Annulment")]
    // Число в конце без явного 'x' НЕ должно трактоваться как количество.
    [InlineData("1x Uncut Skill Gem (Level 19)", "Uncut Skill Gem (Level 19)")]
    public void SplitCount_NoExplicitCount_DefaultsToOne(string line, string? expected = null)
    {
        var (stack, text) = PylonScanner.SplitCount(line);
        Assert.Equal(1, stack);
        Assert.Equal(expected ?? line, text);
    }

    [Theory]
    [InlineData("Skill: Skyfall", "Skyfall")]
    [InlineData("Support: Healing Runes", "Healing Runes")]
    [InlineData("Greater Orb of Transmutation", "Greater Orb of Transmutation")]
    [InlineData("Uncut Skill Gem (Level 19)", "Uncut Skill Gem (Level 19)")]
    public void StripTag_RemovesLeadingTag(string input, string expected) =>
        Assert.Equal(expected, PylonScanner.StripTag(input));
}
