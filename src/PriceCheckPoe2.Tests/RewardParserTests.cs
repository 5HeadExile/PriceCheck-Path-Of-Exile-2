using PriceCheckPoe2.Ocr;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class RewardParserTests
{
    private const string Json = """
    {
      "Exalted Orb": ["exalted orb", "exalted"],
      "Divine Orb": ["divine orb", "divine"],
      "Chaos Orb": ["chaos orb", "chaos"]
    }
    """;

    private static RewardParser Parser() => RewardParser.FromJson(Json, maxDistance: 3);

    [Theory]
    [InlineData("Exalted Orb", "Exalted Orb")]
    [InlineData("exalted", "Exalted Orb")]
    [InlineData("  DIVINE ORB ", "Divine Orb")]
    public void Parse_ExactAlias_ReturnsCanonical(string input, string expected) =>
        Assert.Equal(expected, Parser().Parse(input));

    [Theory]
    [InlineData("Exaited Orb", "Exalted Orb")]   // OCR i↔l
    [InlineData("Dlvine Orb", "Divine Orb")]     // OCR i↔l
    [InlineData("Chaes Orb", "Chaos Orb")]       // OCR o↔e
    public void Parse_OcrTypo_FuzzyMatches(string input, string expected) =>
        Assert.Equal(expected, Parser().Parse(input));

    [Fact]
    public void Parse_Garbage_ReturnsNull() =>
        Assert.Null(Parser().Parse("zzzzzzzzzz qwerty"));

    [Fact]
    public void Parse_Empty_ReturnsNull() =>
        Assert.Null(Parser().Parse("   "));

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("same", "same", 0)]
    public void Levenshtein_KnownDistances(string a, string b, int expected) =>
        Assert.Equal(expected, RewardParser.Levenshtein(a, b));
}
