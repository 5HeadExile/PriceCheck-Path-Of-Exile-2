using PriceCheckPoe2.ItemCheck.Data;
using PriceCheckPoe2.ItemCheck.Parsing;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class StatDatabaseTests
{
    private static readonly string[] Lines =
    {
        """{"ref":"# to maximum Life","matchers":[{"string":"# to maximum Life"}],"trade":{"ids":{"explicit":["explicit.stat_3299347043"],"implicit":["implicit.stat_3299347043"],"rune":["rune.stat_3299347043"]}},"id":"base_maximum_life"}""",
        """{"ref":"#% to Fire Resistance","matchers":[{"string":"#% to Fire Resistance"}],"trade":{"ids":{"explicit":["explicit.stat_3372524247"],"rune":["rune.stat_3372524247"]}},"id":"base_fire_damage_resistance_%"}""",
        """{"ref":"# to all Attributes","matchers":[{"string":"# to all Attributes"}],"trade":{"ids":{"explicit":["explicit.stat_1379411836"]}},"id":"additional_all_attributes"}""",
        """{"ref":"Adds # to # Fire Damage to Attacks","matchers":[{"string":"Adds # to # Fire Damage to Attacks"}],"trade":{"ids":{"explicit":["explicit.stat_addsfire"]}},"id":"adds_fire_damage_attacks"}""",
        """{"ref":"# to maximum Life per 10 Strength","matchers":[{"string":"# to maximum Life per 10 Strength"}],"trade":{"ids":{"explicit":["explicit.stat_lifeperstr"]}},"id":"life_per_10_str"}""",
    };

    private static StatDatabase Db() => StatDatabase.LoadFromLines(Lines);

    [Fact]
    public void MatchLine_Life_ExtractsIdValueAndExplicitTradeId()
    {
        var s = Db().MatchLine("+90 to maximum Life", ModKind.Explicit);
        Assert.NotNull(s);
        Assert.Equal("base_maximum_life", s!.StatId);
        Assert.Equal("explicit.stat_3299347043", s.TradeId);
        Assert.Equal(90, s.Value!.Value, 3);
    }

    [Fact]
    public void MatchLine_PicksTradeIdByAffixKind()
    {
        var implicitLife = Db().MatchLine("+30 to maximum Life", ModKind.Implicit);
        Assert.Equal("implicit.stat_3299347043", implicitLife!.TradeId);

        var runeRes = Db().MatchLine("+12% to Fire Resistance", ModKind.Rune);
        Assert.Equal("rune.stat_3372524247", runeRes!.TradeId);
    }

    [Fact]
    public void MatchLine_Range_CapturesBothNumbers()
    {
        var s = Db().MatchLine("Adds 5 to 12 Fire Damage to Attacks", ModKind.Explicit);
        Assert.NotNull(s);
        Assert.Equal(new[] { 5.0, 12.0 }, s!.Values);
        Assert.Equal(8.5, s.Value!.Value, 3);
    }

    [Fact]
    public void MatchLine_DoesNotConfuseConstantWithVariable()
    {
        // «per 10 Strength» — это другой стат, не базовая жизнь.
        var s = Db().MatchLine("+5 to maximum Life per 10 Strength", ModKind.Explicit);
        Assert.Equal("life_per_10_str", s!.StatId);

        var plain = Db().MatchLine("+90 to maximum Life", ModKind.Explicit);
        Assert.Equal("base_maximum_life", plain!.StatId);
    }

    [Fact]
    public void MatchLine_Unknown_ReturnsNull() =>
        Assert.Null(Db().MatchLine("Grants something completely unknown", ModKind.Explicit));

    [Fact]
    public void Pseudo_SumsElementalResistanceAndLife()
    {
        var mods = new[]
        {
            new MatchedStat("base_fire_damage_resistance_%", null, ModKind.Explicit, "+30% to Fire Resistance", 30, new[] { 30.0 }),
            new MatchedStat("base_cold_damage_resistance_%", null, ModKind.Explicit, "+25% to Cold Resistance", 25, new[] { 25.0 }),
            new MatchedStat("base_lightning_damage_resistance_%", null, ModKind.Explicit, "+20% to Lightning Resistance", 20, new[] { 20.0 }),
            new MatchedStat("base_maximum_life", null, ModKind.Explicit, "+80 to maximum Life", 80, new[] { 80.0 }),
        };

        var pseudo = PseudoRules.Compute(mods);

        var ele = pseudo.Single(p => p.TradeId == "pseudo.pseudo_total_elemental_resistance");
        Assert.Equal(75, ele.Value!.Value, 3);
        var life = pseudo.Single(p => p.TradeId == "pseudo.pseudo_total_life");
        Assert.Equal(80, life.Value!.Value, 3);
    }
}
