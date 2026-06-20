using PriceCheckPoe2.ItemCheck.Parsing;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class ItemTextParserTests
{
    private static ParsedItem Parse(string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ItemCheck", fixture);
        var text = File.ReadAllText(path);
        Assert.True(ItemTextParser.TryParse(text, out var item), $"failed to parse {fixture}");
        return item;
    }

    [Fact]
    public void RareRing_ParsesHeaderLevelAndMods()
    {
        var item = Parse("rare_ring.txt");

        Assert.Equal("Rings", item.ItemClass);
        Assert.Equal(ItemRarity.Rare, item.Rarity);
        Assert.Equal("Woe Turn", item.Name);
        Assert.Equal("Sapphire Ring", item.BaseType);
        Assert.Equal(72, item.ItemLevel);
        Assert.Single(item.Implicits);
        Assert.Equal("+25% to Cold Resistance", item.Implicits[0].Text);
        Assert.Equal(4, item.Explicits.Count);
        Assert.False(item.Corrupted);
    }

    [Fact]
    public void UniqueAmulet_DetectsCorruptedAndAffixes()
    {
        var item = Parse("unique_amulet.txt");

        Assert.Equal(ItemRarity.Unique, item.Rarity);
        Assert.Equal("Astramentis", item.Name);
        Assert.Equal("Stellar Amulet", item.BaseType);
        Assert.Equal(84, item.ItemLevel);
        Assert.True(item.Corrupted);
        Assert.Single(item.Implicits);
        Assert.Equal(2, item.Explicits.Count);
    }

    [Fact]
    public void Currency_ParsesClassAndStackSize_NoMods()
    {
        var item = Parse("currency_exalted.txt");

        Assert.Equal("Stackable Currency", item.ItemClass);
        Assert.Equal(ItemRarity.Currency, item.Rarity);
        Assert.Equal("Exalted Orb", item.Name);
        Assert.Equal("7/20", item.StackSize);
        Assert.False(item.IsGear);
        Assert.Empty(item.Explicits);
        Assert.Empty(item.Implicits);
    }

    [Fact]
    public void SkillGem_ParsesNameWithoutMods()
    {
        var item = Parse("gem_skill.txt");

        Assert.Equal("Skill Gems", item.ItemClass);
        Assert.Equal(ItemRarity.Gem, item.Rarity);
        Assert.Equal("Spark", item.Name);
        Assert.Equal(1, item.GemLevel);
        Assert.Empty(item.Explicits);
        Assert.Empty(item.Implicits);
    }

    [Fact]
    public void RareHelmet_ParsesQualitySocketsAndRune()
    {
        var item = Parse("rare_helmet.txt");

        Assert.Equal("Helmets", item.ItemClass);
        Assert.Equal("Dread Veil", item.Name);
        Assert.Equal("Expert Spired Greathelm", item.BaseType);
        Assert.Equal(20, item.Quality);
        Assert.Equal("S S", item.Sockets);
        Assert.Equal(2, item.SocketCount);
        Assert.Equal(67, item.RequireLevel);
        Assert.Null(item.GemLevel);
        Assert.Equal(81, item.ItemLevel);
        Assert.Single(item.Runes);
        Assert.Equal("+12% to Fire Resistance", item.Runes[0].Text);
        Assert.Equal(3, item.Explicits.Count);
    }

    [Fact]
    public void AdvancedDescription_SkipsAnnotationsAndStripsRollRanges()
    {
        var item = Parse("rare_helmet_advanced.txt");

        Assert.Equal(ItemRarity.Rare, item.Rarity);
        Assert.True(item.Corrupted);

        // «{ ... Modifier ... }» строки не моды; диапазоны ролла «(105-124)» убраны.
        var allMods = item.Implicits.Concat(item.Explicits).Select(m => m.Text).ToList();
        Assert.All(allMods, t => Assert.DoesNotContain("{", t));
        Assert.All(allMods, t => Assert.DoesNotContain("(", t));

        Assert.Contains("+114 to maximum Mana", item.Explicits.Select(m => m.Text));
        Assert.Contains("+153 to maximum Life", item.Explicits.Select(m => m.Text));
        Assert.Contains("+29% to Lightning Resistance", item.Explicits.Select(m => m.Text));
        Assert.Contains("+27 to Spirit", item.Implicits.Select(m => m.Text));
    }

    [Fact]
    public void NonItemText_ReturnsFalse()
    {
        Assert.False(ItemTextParser.TryParse("just some random text", out _));
        Assert.False(ItemTextParser.TryParse("", out _));
    }
}
