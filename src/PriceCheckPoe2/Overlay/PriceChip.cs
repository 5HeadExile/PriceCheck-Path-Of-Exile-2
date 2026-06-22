using System.Drawing;
using System.Drawing.Drawing2D;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Отрисовка ценника-плашки «Тинт-стекло» (design handoff price_chip, вариант B).
/// Тело слегка окрашено в цвет тира (полупрозрачно поверх игры), рамка тонирована,
/// текст — осветлённый тир-цвет. Состояния: обычная / «?» (нет цены) / «лучшая».
/// Рассчитан на отрисовку поверх per-pixel alpha layered-окна.
/// </summary>
public static class PriceChip
{
    public const float Height = 21f;
    private const float PadX = 10f;
    private const float Radius = 6f;
    private const float SegGap = 5f;

    // Шрифты создаём один раз (плашек на экране много). Pixel, не пункты!
    private static readonly Font MainFont = new("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font UnitFont = new("Consolas", 9.5f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly StringFormat Fmt = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces,
        LineAlignment = StringAlignment.Center,
    };

    public enum Tier { Trivial, Cheap, Notable, Pricey, Top }

    /// <summary>Данные для отрисовки плашки.</summary>
    public sealed record Model(bool Unknown, bool Best, double Exalted, string MainText, string? UnitText);

    private static Tier TierFor(double ex) =>
        ex < 0.1 ? Tier.Trivial :
        ex < 1.0 ? Tier.Cheap :
        ex < 5.0 ? Tier.Notable :
        ex < 20.0 ? Tier.Pricey : Tier.Top;

    // (заливка, рамка, текст, базовый-для-суффикса) по тиру.
    private static (Color Fill, Color Border, Color Text, Color Base) Palette(Tier t) => t switch
    {
        Tier.Trivial => (Color.FromArgb(26, 124, 130, 138), Color.FromArgb(82, 124, 130, 138), Hex("#9CA2A8"), Color.FromArgb(124, 130, 138)),
        Tier.Cheap => (Color.FromArgb(33, 198, 202, 208), Color.FromArgb(102, 198, 202, 208), Hex("#E2E6EC"), Color.FromArgb(198, 202, 208)),
        Tier.Notable => (Color.FromArgb(41, 190, 162, 86), Color.FromArgb(140, 190, 162, 86), Hex("#E0CC80"), Color.FromArgb(190, 162, 86)),
        Tier.Pricey => (Color.FromArgb(41, 190, 130, 74), Color.FromArgb(140, 190, 130, 74), Hex("#E0A876"), Color.FromArgb(190, 130, 74)),
        _ => (Color.FromArgb(41, 190, 96, 110), Color.FromArgb(140, 190, 96, 110), Hex("#E29AA2"), Color.FromArgb(190, 96, 110)),
    };

    private static Color Hex(string h) => ColorTranslator.FromHtml(h);

    /// <summary>Ширина плашки под содержимое (высота фиксированная — <see cref="Height"/>).</summary>
    public static float MeasureWidth(Graphics g, Model m)
    {
        var w = PadX * 2;
        if (m.Unknown)
        {
            return w + TextW(g, "?", MainFont);
        }

        if (m.Best)
        {
            w += TextW(g, "★", MainFont) + SegGap;
        }

        w += TextW(g, m.MainText, MainFont);
        if (!string.IsNullOrEmpty(m.UnitText))
        {
            w += SegGap + TextW(g, m.UnitText, UnitFont);
        }

        return w;
    }

    private static float TextW(Graphics g, string s, Font f) => g.MeasureString(s, f, PointF.Empty, Fmt).Width;

    public static void Draw(Graphics g, RectangleF rect, Model m)
    {
        var tier = TierFor(m.Exalted);
        var pal = Palette(tier);

        using var path = Rounded(rect, Radius);

        if (m.Best)
        {
            DrawHalo(g, rect);
            using var grad = new LinearGradientBrush(
                new RectangleF(rect.X, rect.Y, rect.Width, rect.Height),
                Color.FromArgb(72, 214, 182, 104), Color.FromArgb(46, 156, 122, 48), LinearGradientMode.Vertical);
            g.FillPath(grad, path);
            using var hl = new Pen(Color.FromArgb(31, 255, 255, 255), 1f);
            g.DrawLine(hl, rect.X + Radius, rect.Y + 1, rect.Right - Radius, rect.Y + 1);
            using var gb = new Pen(Hex("#C9A24B"), 1f);
            g.DrawPath(gb, path);
        }
        else if (m.Unknown)
        {
            using var fill = new SolidBrush(Color.FromArgb(33, 181, 110, 96));
            g.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb(140, 181, 110, 96), 1f) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);
        }
        else
        {
            using var fill = new SolidBrush(pal.Fill);
            g.FillPath(fill, path);
            using var pen = new Pen(pal.Border, 1f);
            g.DrawPath(pen, path);
        }

        // Текст: сегменты слева направо, вертикально по центру.
        var x = rect.X + PadX;
        var seg = new RectangleF(x, rect.Y, rect.Width, rect.Height);

        if (m.Unknown)
        {
            DrawSeg(g, "?", MainFont, Hex("#D29486"), ref seg);
            return;
        }

        if (m.Best)
        {
            var gold = Hex("#FBEFCB");
            DrawSeg(g, "★", MainFont, gold, ref seg, SegGap);
            DrawSeg(g, m.MainText, MainFont, gold, ref seg);
            if (!string.IsNullOrEmpty(m.UnitText))
            {
                DrawSeg(g, m.UnitText!, UnitFont, Color.FromArgb(170, gold), ref seg, SegGap);
            }

            return;
        }

        DrawSeg(g, m.MainText, MainFont, pal.Text, ref seg);
        if (!string.IsNullOrEmpty(m.UnitText))
        {
            DrawSeg(g, m.UnitText!, UnitFont, Color.FromArgb(150, pal.Base), ref seg, SegGap);
        }
    }

    private static void DrawSeg(Graphics g, string text, Font font, Color color, ref RectangleF seg, float leadGap = 0)
    {
        seg.X += leadGap;
        seg.Width = g.MeasureString(text, font, PointF.Empty, Fmt).Width + 1;
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, seg, Fmt);
        seg.X += seg.Width;
    }

    // Гало «лучшей»: 3 раздутых контура убывающей alpha (не настоящий blur).
    private static void DrawHalo(Graphics g, RectangleF rect)
    {
        for (var i = 3; i >= 1; i--)
        {
            var glow = RectangleF.Inflate(rect, i, i);
            using var path = Rounded(glow, Radius + i);
            using var pen = new Pen(Color.FromArgb(102 / i, 214, 182, 104), 2f);
            g.DrawPath(pen, path);
        }
    }

    private static GraphicsPath Rounded(RectangleF r, float rad)
    {
        var d = rad * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
