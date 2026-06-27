using System.Drawing;
using System.Drawing.Drawing2D;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Отрисовка ценника-плашки «Тинт-стекло», адаптированная под светлый пергамент
/// панели пилона: под тир-тинт кладём почти-непрозрачную тёмную подложку (читается
/// на любом фоне). <b>Авто-масштаб:</b> все размеры выражены как доли высоты текста
/// награды (<c>em</c>), которую измерил OCR — поэтому плашка автоматически подгоняется
/// под любое разрешение/игровой UI-scale без ручных настроек. Состояния: обычная /
/// «?» (нет цены) / «лучшая». Рисуется поверх layered-окна.
/// </summary>
public static class PriceChip
{
    // Все метрики — доли высоты строки текста (em). Откалибровано по 1440p, где
    // OCR даёт высоту имени ≈25px, а прежний хороший вид плашки — высота ≈28.75px
    // (×1.15) и шрифт 15px (×0.60). Доли сохраняют тот вид на любом разрешении.
    public static float HeightFor(float em) => em * 1.15f;
    private static float PadX(float em) => em * 0.55f;
    private static float Radius(float em) => em * 0.30f;
    private static float SegGap(float em) => em * 0.25f;
    private static float MainPx(float em) => em * 0.60f;
    private static float UnitPx(float em) => em * 0.50f;
    private static float Line(float em) => Math.Max(1f, em * 0.05f);

    // Тёмная подложка для читаемости на любом фоне (в т.ч. светлом пергаменте).
    private static readonly Color Plate = Color.FromArgb(228, 20, 20, 24);

    // Кэш моноширинных шрифтов по размеру (округляем до 0.5px), чтобы не создавать
    // Font на каждую плашку/кадр.
    private static readonly Dictionary<float, Font> Fonts = new();

    private static Font Mono(float px)
    {
        var key = MathF.Round(Math.Max(6f, px) * 2f) / 2f;
        if (!Fonts.TryGetValue(key, out var f))
        {
            f = new Font("Consolas", key, FontStyle.Bold, GraphicsUnit.Pixel);
            Fonts[key] = f;
        }

        return f;
    }

    private static readonly StringFormat Fmt = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces,
        LineAlignment = StringAlignment.Center,
    };

    public enum Tier { Trivial, Cheap, Notable, Pricey, Top }

    public sealed record Model(bool Unknown, bool Best, double Exalted, string MainText, string? UnitText);

    // Градация по цене в exalted. Пороги «от-и-до» под реальный разброс наград пилонов
    // (0..сотни ex): <1 трив / 1..5 дёшево / 5..20 заметно / 20..75 дорого / ≥75 топ.
    private static Tier TierFor(double ex) =>
        ex < 1.0 ? Tier.Trivial :
        ex < 5.0 ? Tier.Cheap :
        ex < 20.0 ? Tier.Notable :
        ex < 75.0 ? Tier.Pricey : Tier.Top;

    // База тира (RGB) и цвет текста (осветлённый базовый).
    private static (Color Base, Color Text) Pal(Tier t) => t switch
    {
        Tier.Trivial => (Color.FromArgb(124, 130, 138), Hex("#9CA2A8")),
        Tier.Cheap => (Color.FromArgb(198, 202, 208), Hex("#E2E6EC")),
        Tier.Notable => (Color.FromArgb(190, 162, 86), Hex("#E0CC80")),
        Tier.Pricey => (Color.FromArgb(190, 130, 74), Hex("#E0A876")),
        _ => (Color.FromArgb(190, 96, 110), Hex("#E29AA2")),
    };

    // Альфа тир-тинта поверх подложки (тривиальные тише, ценные ярче).
    private static int FillAlpha(Tier t) => t == Tier.Trivial ? 55 : t == Tier.Cheap ? 70 : 95;
    private static int BorderAlpha(Tier t) => t == Tier.Trivial ? 150 : 210;

    private static Color Hex(string h) => ColorTranslator.FromHtml(h);

    public static float MeasureWidth(Graphics g, Model m, float em)
    {
        var main = Mono(MainPx(em));
        var unit = Mono(UnitPx(em));
        var w = PadX(em) * 2;
        if (m.Unknown)
        {
            return w + TextW(g, "?", main);
        }

        if (m.Best)
        {
            w += TextW(g, "★", main) + SegGap(em);
        }

        w += TextW(g, m.MainText, main);
        if (!string.IsNullOrEmpty(m.UnitText))
        {
            w += SegGap(em) + TextW(g, m.UnitText, unit);
        }

        return w;
    }

    private static float TextW(Graphics g, string s, Font f) => g.MeasureString(s, f, PointF.Empty, Fmt).Width;

    public static void Draw(Graphics g, RectangleF rect, Model m, float em)
    {
        using var path = Rounded(rect, Radius(em));

        if (m.Best)
        {
            DrawHalo(g, rect, em);
            using (var plate = new SolidBrush(Color.FromArgb(220, 26, 22, 12)))
            {
                g.FillPath(plate, path);
            }

            using (var grad = new LinearGradientBrush(
                new RectangleF(rect.X, rect.Y, rect.Width, rect.Height),
                Color.FromArgb(165, 214, 182, 104), Color.FromArgb(120, 156, 122, 48), LinearGradientMode.Vertical))
            {
                g.FillPath(grad, path);
            }

            using (var hl = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
            {
                g.DrawLine(hl, rect.X + Radius(em), rect.Y + 1, rect.Right - Radius(em), rect.Y + 1);
            }

            using (var gb = new Pen(Hex("#C9A24B"), Line(em) * 1.2f))
            {
                g.DrawPath(gb, path);
            }
        }
        else if (m.Unknown)
        {
            using var plate = new SolidBrush(Plate);
            g.FillPath(plate, path);
            using var tint = new SolidBrush(Color.FromArgb(80, 181, 110, 96));
            g.FillPath(tint, path);
            using var pen = new Pen(Color.FromArgb(200, 181, 110, 96), Line(em)) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);
        }
        else
        {
            var tier = TierFor(m.Exalted);
            var (baseC, _) = Pal(tier);
            using var plate = new SolidBrush(Plate);
            g.FillPath(plate, path);
            using var tint = new SolidBrush(Color.FromArgb(FillAlpha(tier), baseC));
            g.FillPath(tint, path);
            using var pen = new Pen(Color.FromArgb(BorderAlpha(tier), baseC), Line(em));
            g.DrawPath(pen, path);
        }

        DrawText(g, rect, m, em);
    }

    private static void DrawText(Graphics g, RectangleF rect, Model m, float em)
    {
        var main = Mono(MainPx(em));
        var unit = Mono(UnitPx(em));
        var seg = new RectangleF(rect.X + PadX(em), rect.Y, rect.Width, rect.Height);

        if (m.Unknown)
        {
            DrawSeg(g, "?", main, Hex("#D29486"), ref seg);
            return;
        }

        if (m.Best)
        {
            var gold = Hex("#FBEFCB");
            DrawSeg(g, "★", main, gold, ref seg, SegGap(em));
            DrawSeg(g, m.MainText, main, gold, ref seg);
            if (!string.IsNullOrEmpty(m.UnitText))
            {
                DrawSeg(g, m.UnitText!, unit, Color.FromArgb(180, gold), ref seg, SegGap(em));
            }

            return;
        }

        var (baseC, text) = Pal(TierFor(m.Exalted));
        DrawSeg(g, m.MainText, main, text, ref seg);
        if (!string.IsNullOrEmpty(m.UnitText))
        {
            DrawSeg(g, m.UnitText!, unit, Color.FromArgb(170, baseC), ref seg, SegGap(em));
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

    // Гало «лучшей»: 2 компактных контура убывающей alpha. Намеренно МАЛЕНЬКОЕ
    // (≤0.12·em), чтобы не вылезать на соседние плашки — раскладка держит между
    // плашками зазор больше этого, поэтому «лучшая» выделяется, но не двигает остальные.
    private static void DrawHalo(Graphics g, RectangleF rect, float em)
    {
        for (var i = 2; i >= 1; i--)
        {
            var d = em * 0.06f * i;
            var glow = RectangleF.Inflate(rect, d, d);
            using var path = Rounded(glow, Radius(em) + d);
            using var pen = new Pen(Color.FromArgb(90 / i, 214, 182, 104), Line(em) * 1.4f);
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
