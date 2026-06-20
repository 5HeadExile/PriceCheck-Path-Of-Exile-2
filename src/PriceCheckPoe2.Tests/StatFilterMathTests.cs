using PriceCheckPoe2.ItemCheck.Data;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class StatFilterMathTests
{
    [Fact]
    public void PositiveRoll_FillsMinAtMinusPercent_NoMax()
    {
        // value 100, +better, 10% окно → min = floor(100 - 10) = 90, max пуст.
        var (min, max) = StatFilterMath.FilterBounds(100, StatFilterMath.PositiveRoll, dp: false, inverted: false);
        Assert.Equal(90, min);
        Assert.Null(max);
    }

    [Fact]
    public void NegativeRoll_FillsMaxAtPlusPercent_NoMin()
    {
        // value 20, -better (меньше лучше) → max = ceil(20 + 2) = 22, min пуст.
        var (min, max) = StatFilterMath.FilterBounds(20, StatFilterMath.NegativeRoll, dp: false, inverted: false);
        Assert.Null(min);
        Assert.Equal(22, max);
    }

    [Fact]
    public void NotComparable_BuildsNoWindow()
    {
        var (min, max) = StatFilterMath.FilterBounds(3492, StatFilterMath.NotComparable, dp: false, inverted: false);
        Assert.Null(min);
        Assert.Null(max);
    }

    [Fact]
    public void Inverted_NegatesAndSwapsBounds()
    {
        // "#% less Damage" (inverted): positive-better value 30 → min 27, после инверсии
        // → max = -27 (ищем «не более -27% = 27% less или хуже»).
        var (min, max) = StatFilterMath.FilterBounds(30, StatFilterMath.PositiveRoll, dp: false, inverted: true);
        Assert.Null(min);
        Assert.Equal(-27, max);
    }

    [Fact]
    public void DecimalRoll_KeepsTwoPlacesForSmallValues()
    {
        // value 1.5, dp → 2 знака: min = floor((1.5 - 0.15)*100)/100 = 1.35.
        var (min, _) = StatFilterMath.FilterBounds(1.5, StatFilterMath.PositiveRoll, dp: true, inverted: false);
        Assert.Equal(1.35, min!.Value, 3);
    }
}
