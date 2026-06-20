using System.Drawing;
using System.Drawing.Drawing2D;

namespace PriceCheckPoe2.Theme;

/// <summary>Хелперы отрисовки GDI+: скруглённые пути, рамки, градиенты, свечение.</summary>
public static class Draw
{
    /// <summary>Скруглённый прямоугольник (4 дуги + замыкание).</summary>
    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(r);
            path.CloseFigure();
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Заливка скруглённого прямоугольника сплошным цветом.</summary>
    public static void Fill(Graphics g, Rectangle r, int radius, Color color)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>Вертикальный градиент в скруглённом прямоугольнике.</summary>
    public static void FillVertical(Graphics g, Rectangle r, int radius, Color top, Color bottom)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new LinearGradientBrush(
            new Rectangle(r.X, r.Y, r.Width, r.Height), top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    /// <summary>Рамка по скруглённому прямоугольнику.</summary>
    public static void Border(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
    {
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>Внешнее свечение фокуса (золотая полупрозрачная рамка вокруг).</summary>
    public static void FocusGlow(Graphics g, Rectangle r, int radius)
    {
        var outer = Rectangle.Inflate(r, 2, 2);
        using var path = RoundedRect(outer, radius + 2);
        using var pen = new Pen(Palette.AccentGlow, 2f);
        g.DrawPath(pen, path);
    }

    /// <summary>Внутренний верхний блик (1px) для премиальной глубины.</summary>
    public static void TopHighlight(Graphics g, Rectangle r, int radius, Color color)
    {
        using var pen = new Pen(color, 1f);
        g.DrawLine(pen, r.X + radius, r.Y + 1, r.Right - radius, r.Y + 1);
    }

    /// <summary>Текст по центру прямоугольника.</summary>
    public static void CenterText(Graphics g, string text, Font font, Color color, Rectangle r)
    {
        TextRenderer.DrawText(g, text, font, r, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    /// <summary>
    /// Текст с межбуквенным трекингом (letter-spacing). GDI/TextRenderer не умеет
    /// трекинг, поэтому рисуем посимвольно, прибавляя <paramref name="tracking"/> px.
    /// Возвращает итоговую ширину.
    /// </summary>
    public static int TrackedText(Graphics g, string text, Font font, Color color, int x, int yCenter, float tracking)
    {
        var cx = x;
        foreach (var ch in text)
        {
            var s = ch.ToString();
            var w = TextRenderer.MeasureText(g, s, font, Size.Empty,
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            var h = font.Height;
            TextRenderer.DrawText(g, s, font, new Rectangle(cx, yCenter - h / 2, w + 2, h), color,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            cx += w + (int)Math.Round(tracking);
        }

        return cx - x;
    }

    /// <summary>Текст слева по вертикали по центру, с отступом.</summary>
    public static void LeftText(Graphics g, string text, Font font, Color color, Rectangle r, int padLeft)
    {
        var rect = new Rectangle(r.X + padLeft, r.Y, r.Width - padLeft, r.Height);
        TextRenderer.DrawText(g, text, font, rect, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
