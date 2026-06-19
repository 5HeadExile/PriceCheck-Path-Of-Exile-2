namespace PriceCheckPoe2.ItemCheck.Parsing;

/// <summary>Редкость предмета PoE2 (из строки «Rarity:»).</summary>
public enum ItemRarity
{
    Unknown,
    Normal,
    Magic,
    Rare,
    Unique,
    Currency,
    Gem,
    Quest,
}

/// <summary>Вид аффикса — определяется по суффиксу строки мода в буфере.</summary>
public enum ModKind
{
    Explicit,
    Implicit,
    Rune,
    Enchant,
    Crafted,
    Fractured,
}

/// <summary>Одна строка мода и её вид.</summary>
public sealed record ItemMod(string Text, ModKind Kind);

/// <summary>
/// Разобранный предмет PoE2 из текста буфера обмена. На этапе I1 — только
/// структура (секции/поля/строки модов); сопоставление модов с trade stat id
/// будет в I2 (StatDatabase).
/// </summary>
public sealed class ParsedItem
{
    public string? ItemClass { get; init; }
    public ItemRarity Rarity { get; init; }
    public string RarityText { get; init; } = string.Empty;

    /// <summary>Имя (для rare/unique — собственное имя; иначе — название предмета).</summary>
    public string? Name { get; init; }

    /// <summary>База (для rare/unique — вторая строка заголовка; иначе совпадает с Name).</summary>
    public string? BaseType { get; init; }

    public int? ItemLevel { get; init; }
    public int? Quality { get; init; }
    public string? Sockets { get; init; }
    public string? StackSize { get; init; }

    public bool Corrupted { get; init; }
    public bool Unidentified { get; init; }
    public bool Mirrored { get; init; }

    public IReadOnlyList<ItemMod> Implicits { get; init; } = Array.Empty<ItemMod>();
    public IReadOnlyList<ItemMod> Explicits { get; init; } = Array.Empty<ItemMod>();
    public IReadOnlyList<ItemMod> Runes { get; init; } = Array.Empty<ItemMod>();
    public IReadOnlyList<ItemMod> Enchants { get; init; } = Array.Empty<ItemMod>();

    /// <summary>Исходный текст буфера (для отладки и повторного разбора).</summary>
    public string RawText { get; init; } = string.Empty;

    public bool IsGear =>
        Rarity is ItemRarity.Normal or ItemRarity.Magic or ItemRarity.Rare or ItemRarity.Unique;
}
