using System.Drawing;
using PriceCheckPoe2.Scanning;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class PylonScannerTests
{
    [Theory]
    [InlineData("1x Perfect Orb of Transmutation", true)]
    [InlineData("3x Exalted Orb", true)]
    [InlineData("3 Divine Orb", true)]
    [InlineData("Chaos Orb x12", true)]
    [InlineData("Masterwork Rune", false)]
    [InlineData("Perfect Orb of Augmentation", false)]
    public void HasExplicitCount_DetectsLeadingOrTrailingCount(string line, bool expected) =>
        Assert.Equal(expected, PylonScanner.HasExplicitCount(line));

    [Fact]
    public void VerticalOverlap_FullOverlap_IsOne()
    {
        var a = new Rectangle(0, 100, 200, 30);
        var b = new Rectangle(50, 102, 180, 28); // почти та же строка
        Assert.True(PylonScanner.VerticalOverlap(a, b) > 0.6);
    }

    [Fact]
    public void VerticalOverlap_SeparateRows_IsZero()
    {
        var a = new Rectangle(0, 100, 200, 30);
        var b = new Rectangle(0, 140, 200, 30); // следующая строка
        Assert.Equal(0, PylonScanner.VerticalOverlap(a, b), 3);
    }

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
