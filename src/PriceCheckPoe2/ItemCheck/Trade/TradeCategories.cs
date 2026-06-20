namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>
/// Сопоставление класса предмета PoE2 («Item Class») с категорией trade2 (slot).
/// Идентификаторы категорий — из EE2 CATEGORY_TO_TRADE_ID (MIT). Поиск рара по
/// категории слота находит похожие предметы любой базы — как и делает EE2.
/// </summary>
public static class TradeCategories
{
    private static readonly Dictionary<string, string> ByClass = new(StringComparer.OrdinalIgnoreCase)
    {
        // Броня
        ["Helmets"] = "armour.helmet",
        ["Body Armours"] = "armour.chest",
        ["Gloves"] = "armour.gloves",
        ["Boots"] = "armour.boots",
        ["Shields"] = "armour.shield",
        ["Bucklers"] = "armour.buckler",
        ["Quivers"] = "armour.quiver",
        ["Foci"] = "armour.focus",
        ["Focus"] = "armour.focus",

        // Аксессуары
        ["Amulets"] = "accessory.amulet",
        ["Rings"] = "accessory.ring",
        ["Belts"] = "accessory.belt",

        // Оружие PoE2
        ["Wands"] = "weapon.wand",
        ["Sceptres"] = "weapon.sceptre",
        ["Staves"] = "weapon.staff",
        ["Quarterstaves"] = "weapon.warstaff",
        ["Bows"] = "weapon.bow",
        ["Crossbows"] = "weapon.crossbow",
        ["Spears"] = "weapon.spear",
        ["Flails"] = "weapon.flail",
        ["Daggers"] = "weapon.dagger",
        ["Claws"] = "weapon.claw",
        ["One Hand Maces"] = "weapon.onemace",
        ["Two Hand Maces"] = "weapon.twomace",

        // Прочее
        ["Jewels"] = "jewel",
        ["Charms"] = "flask.charm",
        ["Life Flasks"] = "flask",
        ["Mana Flasks"] = "flask",
        ["Skill Gems"] = "gem.activegem",
        ["Support Gems"] = "gem.supportgem",
        ["Waystones"] = "map.waystone",
    };

    public static string? ForClass(string? itemClass) =>
        itemClass is not null && ByClass.TryGetValue(itemClass, out var id) ? id : null;
}
