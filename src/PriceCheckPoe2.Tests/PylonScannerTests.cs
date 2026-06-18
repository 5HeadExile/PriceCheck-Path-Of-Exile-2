using PriceCheckPoe2.Scanning;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class PylonScannerTests
{
    [Theory]
    [InlineData("5x Exalted Orb", 5, "Exalted Orb")]
    [InlineData("3 Divine Orb", 3, "Divine Orb")]
    [InlineData("Chaos Orb x12", 12, "Chaos Orb")]
    [InlineData("Vaal Orb 7", 7, "Vaal Orb")]
    public void SplitCount_ExtractsStack(string line, int stack, string text)
    {
        var (parsedStack, parsedText) = PylonScanner.SplitCount(line);
        Assert.Equal(stack, parsedStack);
        Assert.Equal(text, parsedText);
    }

    [Theory]
    [InlineData("Mirror of Kalandra")]
    [InlineData("Orb of Annulment")]
    public void SplitCount_NoNumber_DefaultsToOne(string line)
    {
        var (stack, text) = PylonScanner.SplitCount(line);
        Assert.Equal(1, stack);
        Assert.Equal(line, text);
    }
}
