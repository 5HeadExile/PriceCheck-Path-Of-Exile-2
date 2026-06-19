using System.Text.RegularExpressions;

namespace PriceCheckPoe2.ItemCheck.Parsing;

/// <summary>
/// Разбирает текст предмета PoE2 из буфера обмена (англ. клиент). Формат:
/// секции, разделённые строкой из дефисов («--------»); первая секция — заголовок
/// (Item Class / Rarity / имя / база), далее — свойства (Quality, Item Level,
/// Sockets…), реквайры, имплициты/эксплициты/руны и флаги (Corrupted и т.п.).
/// </summary>
public static class ItemTextParser
{
    private static readonly Regex Separator = new(@"^-{3,}$", RegexOptions.Compiled);
    private static readonly Regex RxItemClass = new(@"^Item Class:\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RxRarity = new(@"^Rarity:\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RxItemLevel = new(@"^Item Level:\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex RxQuality = new(@"^Quality:\s*\+?(\d+)%", RegexOptions.Compiled);
    private static readonly Regex RxSockets = new(@"^Sockets:\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RxStackSize = new(@"^Stack Size:\s*(.+)$", RegexOptions.Compiled);

    // Строка-свойство вида «Ключ:» (Requirements, Level, Str, Armour…) — не мод.
    private static readonly Regex RxProperty = new(@"^[A-Za-z][A-Za-z ]*:", RegexOptions.Compiled);

    // Суффикс вида « (implicit)», « (rune)» и т.п. в конце строки мода.
    private static readonly Regex RxModSuffix = new(
        @"\s*\((implicit|rune|enchant|crafted|fractured|scourge|veiled|desecrated|explicit)\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Corrupted", "Unidentified", "Mirrored", "Split", "Fractured Item", "Synthesised Item",
    };

    public static bool TryParse(string? clipboardText, out ParsedItem item)
    {
        item = new ParsedItem();
        if (string.IsNullOrWhiteSpace(clipboardText) || !clipboardText.Contains("Item Class:"))
        {
            return false;
        }

        var sections = SplitSections(clipboardText);
        if (sections.Count == 0)
        {
            return false;
        }

        var (itemClass, rarityText, names) = ParseHeader(sections[0]);
        var rarity = MapRarity(rarityText);
        var isGear = rarity is ItemRarity.Normal or ItemRarity.Magic or ItemRarity.Rare or ItemRarity.Unique;

        int? itemLevel = null, quality = null;
        string? sockets = null, stackSize = null;
        bool corrupted = false, unidentified = false, mirrored = false;
        var implicits = new List<ItemMod>();
        var explicits = new List<ItemMod>();
        var runes = new List<ItemMod>();
        var enchants = new List<ItemMod>();

        foreach (var section in sections.Skip(1))
        {
            foreach (var line in section)
            {
                if (RxItemLevel.Match(line) is { Success: true } lvl)
                {
                    itemLevel = int.Parse(lvl.Groups[1].Value);
                    continue;
                }

                if (RxQuality.Match(line) is { Success: true } q)
                {
                    quality = int.Parse(q.Groups[1].Value);
                    continue;
                }

                if (RxSockets.Match(line) is { Success: true } s)
                {
                    sockets = s.Groups[1].Value.Trim();
                    continue;
                }

                if (RxStackSize.Match(line) is { Success: true } st)
                {
                    stackSize = st.Groups[1].Value.Trim();
                    continue;
                }

                if (Flags.Contains(line.Trim()))
                {
                    var f = line.Trim();
                    corrupted |= f.Equals("Corrupted", StringComparison.OrdinalIgnoreCase);
                    unidentified |= f.Equals("Unidentified", StringComparison.OrdinalIgnoreCase);
                    mirrored |= f.Equals("Mirrored", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                // Прочие «Ключ:»-строки (Requirements, Level, Str…) — свойства, не моды.
                if (RxProperty.IsMatch(line))
                {
                    continue;
                }

                // Моды разбираем только у снаряжения; у валюты/гемов это описание.
                if (!isGear)
                {
                    continue;
                }

                var (text, kind) = SplitMod(line);
                switch (kind)
                {
                    case ModKind.Implicit: implicits.Add(new ItemMod(text, kind)); break;
                    case ModKind.Rune: runes.Add(new ItemMod(text, kind)); break;
                    case ModKind.Enchant: enchants.Add(new ItemMod(text, kind)); break;
                    default: explicits.Add(new ItemMod(text, kind)); break;
                }
            }
        }

        item = new ParsedItem
        {
            ItemClass = itemClass,
            Rarity = rarity,
            RarityText = rarityText ?? string.Empty,
            Name = names.Count > 0 ? names[0] : null,
            BaseType = names.Count >= 2 ? names[1] : names.FirstOrDefault(),
            ItemLevel = itemLevel,
            Quality = quality,
            Sockets = sockets,
            StackSize = stackSize,
            Corrupted = corrupted,
            Unidentified = unidentified,
            Mirrored = mirrored,
            Implicits = implicits,
            Explicits = explicits,
            Runes = runes,
            Enchants = enchants,
            RawText = clipboardText,
        };
        return true;
    }

    private static List<List<string>> SplitSections(string text)
    {
        var sections = new List<List<string>>();
        var current = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (Separator.IsMatch(line.Trim()))
            {
                if (current.Count > 0)
                {
                    sections.Add(current);
                    current = new List<string>();
                }
                continue;
            }

            if (line.Trim().Length > 0)
            {
                current.Add(line.Trim());
            }
        }

        if (current.Count > 0)
        {
            sections.Add(current);
        }

        return sections;
    }

    private static (string? ItemClass, string? Rarity, List<string> Names) ParseHeader(List<string> header)
    {
        string? itemClass = null, rarity = null;
        var names = new List<string>();
        foreach (var line in header)
        {
            if (RxItemClass.Match(line) is { Success: true } c)
            {
                itemClass = c.Groups[1].Value.Trim();
            }
            else if (RxRarity.Match(line) is { Success: true } r)
            {
                rarity = r.Groups[1].Value.Trim();
            }
            else if (!RxProperty.IsMatch(line))
            {
                names.Add(line);
            }
        }

        return (itemClass, rarity, names);
    }

    private static (string Text, ModKind Kind) SplitMod(string line)
    {
        var m = RxModSuffix.Match(line);
        if (!m.Success)
        {
            return (line.Trim(), ModKind.Explicit);
        }

        var text = line[..m.Index].Trim();
        var kind = m.Groups[1].Value.ToLowerInvariant() switch
        {
            "implicit" => ModKind.Implicit,
            "rune" => ModKind.Rune,
            "enchant" => ModKind.Enchant,
            "crafted" => ModKind.Crafted,
            "fractured" => ModKind.Fractured,
            _ => ModKind.Explicit,
        };
        return (text, kind);
    }

    private static ItemRarity MapRarity(string? rarity) => rarity?.Trim().ToLowerInvariant() switch
    {
        "normal" => ItemRarity.Normal,
        "magic" => ItemRarity.Magic,
        "rare" => ItemRarity.Rare,
        "unique" => ItemRarity.Unique,
        "currency" => ItemRarity.Currency,
        "gem" => ItemRarity.Gem,
        "quest" => ItemRarity.Quest,
        _ => ItemRarity.Unknown,
    };
}
