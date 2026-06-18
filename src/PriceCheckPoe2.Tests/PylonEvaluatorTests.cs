using PriceCheckPoe2.Pricing;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class PylonEvaluatorTests
{
    private static PylonEvaluator Evaluator()
    {
        var prices = new Dictionary<string, RewardPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["Exalted Orb"] = new("Exalted Orb", 1.0, null),
            ["Divine Orb"] = new("Divine Orb", 150.0, 1.0),
            ["Chaos Orb"] = new("Chaos Orb", 0.5, null),
        };
        return new PylonEvaluator(prices);
    }

    [Fact]
    public void Evaluate_SumsPriceTimesStack()
    {
        var pylon = new Pylon("A", new[]
        {
            new Reward("Exalted Orb", 10),  // 10
            new Reward("Divine Orb", 2),    // 300
        });

        var result = Evaluator().Evaluate(pylon);

        Assert.Equal(310.0, result.TotalExalted, 3);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public void Evaluate_UnknownReward_CountsAsZero()
    {
        var pylon = new Pylon("B", new[] { new Reward("Unknown Thing", 5) });

        var result = Evaluator().Evaluate(pylon);

        Assert.Equal(0.0, result.TotalExalted, 3);
        Assert.Null(result.Lines[0].Price);
    }

    [Fact]
    public void Rank_OrdersByTotalDescending()
    {
        var weak = new Pylon("weak", new[] { new Reward("Chaos Orb", 4) });     // 2
        var strong = new Pylon("strong", new[] { new Reward("Divine Orb", 1) }); // 150

        var ranked = Evaluator().Rank(new[] { weak, strong });

        Assert.Equal("strong", ranked[0].PylonId);
        Assert.Equal("weak", ranked[1].PylonId);
    }

    [Fact]
    public void Reward_StackBelowOne_NormalizedToOne()
    {
        var reward = new Reward("Exalted Orb", 0);
        Assert.Equal(1, reward.Stack);
    }
}
