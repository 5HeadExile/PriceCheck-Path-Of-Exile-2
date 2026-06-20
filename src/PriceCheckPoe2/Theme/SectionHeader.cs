using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Заголовок секции: моно UPPERCASE + затухающая линия справа. Активный — золотой,
/// приглушённый («Отладка») — серый с тегом «ДОПОЛНИТЕЛЬНО».
/// </summary>
public sealed class SectionHeader : Control
{
    private readonly string _caption;
    private readonly bool _muted;
    private readonly string? _badge;

    public SectionHeader(string caption, bool muted = false, string? badge = null)
    {
        _caption = caption.ToUpperInvariant();
        _muted = muted;
        _badge = badge;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 20;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var color = _muted ? Palette.TextFaint : Palette.Accent;
        using var font = Palette.Section();
        var size = TextRenderer.MeasureText(g, _caption, font);
        var midY = Height / 2;

        TextRenderer.DrawText(g, _caption, font, new Point(0, midY - size.Height / 2), color,
            TextFormatFlags.NoPrefix);

        var lineStart = size.Width + 10;

        // Бейдж «ДОПОЛНИТЕЛЬНО» для приглушённой секции.
        if (_badge is not null)
        {
            using var bf = new Font("Consolas", 7.5f, FontStyle.Regular);
            var bw = TextRenderer.MeasureText(g, _badge, bf).Width + 10;
            var br = new Rectangle(lineStart, midY - 8, bw, 16);
            Draw.Border(g, br, 3, Palette.Hex("#2A2A31"));
            Draw.CenterText(g, _badge, bf, Palette.TextFaint, br);
            lineStart = br.Right + 10;
        }

        // Линия справа.
        if (lineStart < Width - 4)
        {
            if (_muted)
            {
                using var pen = new Pen(Palette.Hex("#222229"), 1f);
                g.DrawLine(pen, lineStart, midY, Width - 2, midY);
            }
            else
            {
                using var brush = new LinearGradientBrush(
                    new Rectangle(lineStart, midY - 1, Width - lineStart, 2),
                    Palette.Hex("#3A3320"), Color.FromArgb(0, 58, 51, 32), LinearGradientMode.Horizontal);
                g.FillRectangle(brush, lineStart, midY, Width - lineStart - 2, 1);
            }
        }
    }
}
