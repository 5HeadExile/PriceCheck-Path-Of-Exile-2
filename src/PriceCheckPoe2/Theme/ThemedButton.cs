using System.Drawing;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>Вариант кнопки из дизайн-системы.</summary>
public enum ButtonVariant
{
    /// <summary>Тихая поверхность, текст muted (вторичное действие).</summary>
    Normal,

    /// <summary>Золотая рамка + тёмный градиент, светлый текст (первичное действие).</summary>
    Primary,

    /// <summary>Ghost с красной рамкой (опасное действие — «Выход»).</summary>
    Danger,

    /// <summary>Сплошная золотая заливка, тёмный текст («Сохранить»).</summary>
    GoldFill,
}

/// <summary>
/// Кнопка дизайн-системы (GDI+). Поддерживает варианты, левый глиф-иконку
/// (Segoe MDL2) и опциональный правый тег-статус. Состояния hover/focus/active
/// отрисовываются по токенам из <see cref="Palette"/>.
/// </summary>
public sealed class ThemedButton : Control
{
    private bool _hover;
    private bool _pressed;
    private readonly int _radius = 6;

    public ButtonVariant Variant { get; set; } = ButtonVariant.Normal;

    /// <summary>Символ-глиф (Segoe MDL2 Assets), напр. "". Пусто — без иконки.</summary>
    public string? Glyph { get; set; }

    /// <summary>Правый тег-статус, напр. «ВКЛ». Пусто — нет тега.</summary>
    public string? Tag2 { get; set; }

    public ThemedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Height = 34;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Focus(); Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        var (bg, bgBottom, border, textColor, glyphColor) = Resolve();

        // Фон
        if (Variant == ButtonVariant.Primary)
        {
            Draw.FillVertical(g, r, _radius, bg, bgBottom);
        }
        else if (Variant == ButtonVariant.GoldFill)
        {
            Draw.FillVertical(g, r, _radius, Palette.AccentLight, Palette.Accent);
        }
        else if (bg != Color.Transparent)
        {
            Draw.Fill(g, r, _radius, bg);
        }

        // Рамка
        Draw.Border(g, r, _radius, border);

        // Внутренний блик у золотых/первичных
        if (Variant == ButtonVariant.Primary)
        {
            Draw.TopHighlight(g, r, _radius, Color.FromArgb(31, 214, 182, 104));
        }
        else if (Variant == ButtonVariant.GoldFill)
        {
            Draw.TopHighlight(g, r, _radius, Color.FromArgb(64, 255, 255, 255));
        }

        // Фокус-свечение (клавиатура)
        if (Focused)
        {
            Draw.Border(g, r, _radius, Palette.Accent);
            Draw.FocusGlow(g, r, _radius);
        }

        // Иконка-глиф слева
        var textLeft = 12;
        if (!string.IsNullOrEmpty(Glyph))
        {
            using var iconFont = Palette.Icon(11f);
            var iconRect = new Rectangle(10, 0, 20, Height);
            TextRenderer.DrawText(g, Glyph, iconFont, iconRect, glyphColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            textLeft = 34;
        }

        // Правый тег-статус
        var textRightPad = 12;
        if (!string.IsNullOrEmpty(Tag2))
        {
            using var tagFont = Palette.MonoSmall();
            var tw = TextRenderer.MeasureText(g, Tag2, tagFont).Width + 12;
            var tagRect = new Rectangle(Width - tw - 10, (Height - 18) / 2, tw, 18);
            Draw.Fill(g, tagRect, 4, Palette.Surface);
            Draw.Border(g, tagRect, 4, Palette.AccentBorder);
            Draw.CenterText(g, Tag2, tagFont, Palette.Accent, tagRect);
            textRightPad = tw + 16;
        }

        // Текст
        using var font = Palette.Action();
        var labelRect = new Rectangle(textLeft, 0, Width - textLeft - textRightPad, Height);
        TextRenderer.DrawText(g, Text, font, labelRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private (Color bg, Color bgBottom, Color border, Color text, Color glyph) Resolve()
    {
        // active имеет приоритет над hover.
        switch (Variant)
        {
            case ButtonVariant.Primary:
                return _pressed
                    ? (Palette.SurfaceActive, Palette.SurfaceActive, Palette.AccentBorder, Palette.TextBright, Palette.AccentLighter)
                    : (Palette.Hex("#241F14"), Palette.Hex("#1C1810"), Palette.AccentBorder, Palette.TextBright, Palette.AccentLighter);

            case ButtonVariant.GoldFill:
                return (Palette.AccentLight, Palette.Accent, Palette.AccentBorder, Palette.OnAccent, Palette.OnAccent);

            case ButtonVariant.Danger:
                return _hover
                    ? (Palette.DangerHover, Palette.DangerHover, Palette.DangerBorder, Palette.Danger, Palette.Danger)
                    : (Color.Transparent, Color.Transparent, Palette.DangerBorder, Palette.Danger, Palette.Danger);

            default:
                if (_pressed)
                {
                    return (Palette.SurfaceActive, Palette.SurfaceActive, Palette.BorderQuiet, Palette.TextMuted, Palette.TextMuted);
                }

                return _hover
                    ? (Palette.SurfaceHover, Palette.SurfaceHover, Palette.BorderStrong, Palette.Text, Palette.AccentLighter)
                    : (Palette.Surface, Palette.Surface, Palette.BorderQuiet, Palette.TextMuted, Palette.TextFaint);
        }
    }
}
