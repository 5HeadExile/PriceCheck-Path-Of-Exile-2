using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>Тон заголовка секции.</summary>
public enum SectionTone
{
    /// <summary>Золотой активный (окно настроек).</summary>
    Gold,

    /// <summary>Приглушённый (секции меню и «Отладка»).</summary>
    Faint,
}

/// <summary>
/// Заголовок секции: моно UPPERCASE 9px с трекингом +1.5 и линией справа.
/// Золотой — затухающая золотая линия; приглушённый — тихая сплошная.
/// </summary>
public sealed class SectionHeader : Control
{
    private readonly string _caption;
    private readonly SectionTone _tone;
    private readonly string? _badge;

    public SectionHeader(string caption, SectionTone tone = SectionTone.Gold, string? badge = null)
    {
        _caption = caption.ToUpperInvariant();
        _tone = tone;
        _badge = badge;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 18;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var color = _tone == SectionTone.Gold ? Palette.Accent : Palette.TextFaint;
        var midY = Height / 2;
        using var font = Palette.Section();
        var textW = Draw.TrackedText(g, _caption, font, color, 0, midY, 1.5f);

        var lineStart = textW + 12;

        if (_badge is not null)
        {
            using var bf = Palette.Badge();
            var bw = TextRenderer.MeasureText(g, _badge, bf).Width + 10;
            var br = new Rectangle(lineStart, midY - 8, bw, 15);
            Draw.Border(g, br, 3, Palette.Hex("#2A2A31"));
            Draw.CenterText(g, _badge, bf, Palette.Hex("#5A564E"), br);
            lineStart = br.Right + 10;
        }

        if (lineStart < Width - 4)
        {
            if (_tone == SectionTone.Gold)
            {
                using var brush = new LinearGradientBrush(
                    new Rectangle(lineStart, midY - 1, Width - lineStart, 2),
                    Palette.Hex("#3A3320"), Color.FromArgb(0, 58, 51, 32), LinearGradientMode.Horizontal);
                g.FillRectangle(brush, lineStart, midY, Width - lineStart - 2, 1);
            }
            else
            {
                using var pen = new Pen(Palette.Hex("#222229"), 1f);
                g.DrawLine(pen, lineStart, midY, Width - 2, midY);
            }
        }
    }
}
