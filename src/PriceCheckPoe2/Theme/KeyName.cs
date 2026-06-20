using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Перевод между WinForms <see cref="Keys"/> и именами SharpHook <c>KeyCode</c>
/// (вида <c>VcF2</c>), которые хранятся в конфиге и парсятся в HotkeyManager.
/// </summary>
public static class KeyName
{
    /// <summary>Имя SharpHook (<c>VcF2</c>, <c>VcA</c>, <c>Vc1</c>) или null, если не поддержано.</summary>
    public static string? FromWinForms(Keys key)
    {
        // F1..F24
        if (key is >= Keys.F1 and <= Keys.F24)
        {
            return "Vc" + key; // Keys.F2 → "VcF2"
        }

        // A..Z
        if (key is >= Keys.A and <= Keys.Z)
        {
            return "Vc" + key; // Keys.A → "VcA"
        }

        // 0..9 (верхний ряд)
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return "Vc" + (char)('0' + (key - Keys.D0)); // Keys.D1 → "Vc1"
        }

        return key switch
        {
            Keys.Insert => "VcInsert",
            Keys.Delete => "VcDelete",
            Keys.Home => "VcHome",
            Keys.End => "VcEnd",
            Keys.PageUp => "VcPageUp",
            Keys.PageDown => "VcPageDown",
            Keys.Oemtilde => "VcBackQuote",
            _ => null,
        };
    }

    /// <summary>Человекочитаемая подпись для keycap: <c>VcF2</c> → <c>F2</c>.</summary>
    public static string Display(string? vcName)
    {
        if (string.IsNullOrWhiteSpace(vcName))
        {
            return "—";
        }

        return vcName.StartsWith("Vc", StringComparison.OrdinalIgnoreCase)
            ? vcName[2..]
            : vcName;
    }
}
