using System.Drawing;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Дизайн-токены «Воронёная сталь и сдержанное золото» (из design handoff).
/// Цвета и шрифты в одном месте, чтобы UI был консистентным и легко правился.
/// </summary>
public static class Palette
{
    public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    // Поверхности / окно
    public static readonly Color Scrim = Color.FromArgb(153, 10, 10, 12); // #0A0A0C @60%
    public static readonly Color WindowTop = Hex("#1A1A20");
    public static readonly Color WindowBottom = Hex("#141418");
    public static readonly Color Surface = Hex("#1F1F25");
    public static readonly Color SurfaceHover = Hex("#27272F");
    public static readonly Color SurfaceActive = Hex("#121216");
    public static readonly Color InputBg = Hex("#121217");
    public static readonly Color FieldMutedBg = Hex("#101015");

    // Текст
    public static readonly Color Text = Hex("#E6E1D6");
    public static readonly Color TextBright = Hex("#F0E9D8");
    public static readonly Color TextMuted = Hex("#8C887E");
    public static readonly Color TextFaint = Hex("#6E6A60");

    // Золото-акцент
    public static readonly Color Accent = Hex("#C9A24B");
    public static readonly Color AccentLight = Hex("#D6B668");
    public static readonly Color AccentLighter = Hex("#E0C078");
    public static readonly Color AccentDark = Hex("#9C7A30");
    public static readonly Color AccentBorder = Hex("#6E5A2E");
    public static readonly Color AccentGlow = Color.FromArgb(56, 201, 162, 75); // rgba(201,162,75,.22)

    // Семантика
    public static readonly Color Danger = Hex("#B5594F");
    public static readonly Color DangerBorder = Hex("#4A2A26");
    public static readonly Color DangerHover = Color.FromArgb(26, 181, 89, 79); // rgba(181,89,79,.10)
    public static readonly Color Success = Hex("#4FB286");

    // Рамки
    public static readonly Color BorderStrong = Hex("#3A3A42");
    public static readonly Color BorderQuiet = Hex("#2E2E36");
    public static readonly Color BorderFaint = Hex("#26262C");

    // Тёмный текст на золотой заливке
    public static readonly Color OnAccent = Hex("#1A1408");

    // Шрифты
    public static Font Title() => new("Segoe UI", 11.5f, FontStyle.Bold);
    public static Font Action() => new("Segoe UI", 10.5f, FontStyle.Bold);
    public static Font Label() => new("Segoe UI", 10f, FontStyle.Regular);
    public static Font Section() => new("Consolas", 8.5f, FontStyle.Regular);
    public static Font Mono() => new("Consolas", 10f, FontStyle.Regular);
    public static Font MonoSmall() => new("Consolas", 8.5f, FontStyle.Regular);
    public static Font Icon(float size = 12f) => new("Segoe MDL2 Assets", size, FontStyle.Regular);
}
