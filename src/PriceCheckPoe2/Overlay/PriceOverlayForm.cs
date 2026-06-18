using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PriceCheckPoe2.Scanning;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Прозрачный click-through оверлей. Рисует цену напротив каждой награды на
/// тёмной плашке (читаемо на любом фоне), цвет — по тиру ценности, лучшая
/// награда подсвечена. Крупные суммы — в divine, мелкие — в exalted. При
/// появлении плавно проявляется (fade-in).
/// </summary>
public sealed class PriceOverlayForm : Form
{
    private const int EdgeMargin = 8;
    private const int Gap = 10;
    private const double DivineThreshold = 1.0; // от скольких div показываем в divine
    private const double TargetOpacity = 1.0;

    private IReadOnlyList<PylonScanResult> _results = Array.Empty<PylonScanResult>();
    private bool _debug;

    private readonly System.Windows.Forms.Timer _fadeTimer;

    public PriceOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // фон полностью прозрачный
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += FadeTick;
        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                Opacity = 0;
                _fadeTimer.Start();
            }
            else
            {
                _fadeTimer.Stop();
            }
        };
    }

    /// <summary>Click-through: окно не перехватывает мышь.</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;
            const int WS_EX_TOOLWINDOW = 0x80;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public bool DebugMode
    {
        get => _debug;
        set
        {
            _debug = value;
            Invalidate();
        }
    }

    public void Update(IReadOnlyList<PylonScanResult> results)
    {
        _results = results;
        Invalidate();
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        var next = Opacity + 0.12;
        if (next >= TargetOpacity)
        {
            Opacity = TargetOpacity;
            _fadeTimer.Stop();
        }
        else
        {
            Opacity = next;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Лучшая награда по ценности — для подсветки.
        var best = _results
            .SelectMany(r => r.Rewards)
            .Where(x => x.Price is not null)
            .OrderByDescending(x => x.LineTotal)
            .FirstOrDefault();

        using var priceFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var totalFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);

        foreach (var result in _results)
        {
            if (_debug)
            {
                using var pen = new Pen(Color.DeepSkyBlue, 2);
                g.DrawRectangle(pen, ToLocal(result.Region));
            }

            DrawTotalChip(g, result, totalFont);

            foreach (var reward in result.Rewards)
            {
                var b = ToLocal(reward.ScreenBounds);
                if (_debug)
                {
                    using var lp = new Pen(Color.FromArgb(120, 0, 255, 0), 1);
                    g.DrawRectangle(lp, b);
                }

                var isBest = best is not null
                    && ReferenceEquals(reward, best);

                DrawPriceChip(g, reward, priceFont, b, isBest);
            }
        }
    }

    private void DrawPriceChip(Graphics g, PricedReward reward, Font font, Rectangle row, bool best)
    {
        var (text, color) = PriceLabel(reward);
        if (best)
        {
            text = "★ " + text;
        }

        var size = g.MeasureString(text, font);
        var w = (int)size.Width + 16;
        var h = (int)size.Height + 6;

        // Ставим плашку сразу после названия, но не даём вылезти за экран.
        var desiredLeft = row.Right + Gap;
        var maxLeft = Width - w - EdgeMargin;
        var left = Math.Min(desiredLeft, maxLeft);
        left = Math.Max(left, EdgeMargin);
        var top = row.Y + (row.Height - h) / 2;

        var rect = new Rectangle(left, top, w, h);

        using var path = Rounded(rect, 8);
        // Непрозрачный нейтральный фон: без примеси magenta-ключа (раньше плашки
        // отдавали в фиолетовый) — ценностный цвет читается чётко.
        using var bg = new SolidBrush(Color.FromArgb(255, 24, 24, 28));
        g.FillPath(bg, path);
        // Лучшую награду выделяем только мягкой золотой рамкой (сдержанно).
        using var border = new Pen(
            best ? Color.FromArgb(255, 214, 182, 104) : Color.FromArgb(255, 58, 58, 66),
            best ? 2f : 1f);
        g.DrawPath(border, path);

        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, left + 8, top + 3);
    }

    private void DrawTotalChip(Graphics g, PylonScanResult result, Font font)
    {
        double ex = 0, div = 0;
        foreach (var r in result.Rewards)
        {
            ex += r.LineTotal;
            div += (r.Price?.DivineValue ?? 0) * r.Stack;
        }

        var text = "Σ " + FormatValue(ex, div);
        var size = g.MeasureString(text, font);
        var w = (int)size.Width + 18;
        var h = (int)size.Height + 8;

        var origin = ToLocal(result.Region.Location);
        var left = Math.Max(origin.X, EdgeMargin);
        var top = Math.Max(origin.Y - h - 6, EdgeMargin);
        var rect = new Rectangle(left, top, w, h);

        using var path = Rounded(rect, 9);
        using var bg = new SolidBrush(Color.FromArgb(255, 22, 22, 28));
        g.FillPath(bg, path);
        using var border = new Pen(Color.FromArgb(255, 72, 76, 86), 1.5f);
        g.DrawPath(border, path);
        using var brush = new SolidBrush(Color.FromArgb(214, 218, 224));
        g.DrawString(text, font, brush, left + 9, top + 4);
    }

    /// <summary>Текст и цвет цены: «?» для нераспознанного, иначе сумма + тир-цвет.</summary>
    private static (string Text, Color Color) PriceLabel(PricedReward reward)
    {
        if (reward.Price is null)
        {
            return ("?", Color.FromArgb(181, 110, 96));
        }

        var ex = reward.LineTotal;
        var div = (reward.Price.DivineValue ?? 0) * reward.Stack;
        return (FormatValue(ex, div), TierColor(ex));
    }

    private static string FormatValue(double exalted, double divine) =>
        divine >= DivineThreshold
            ? $"{divine:0.##} div"
            : $"{exalted:0.##} ex";

    // Сдержанная палитра по ценности (в exalted): приглушённые тона, читаемые
    // на нейтральном тёмном фоне плашки.
    private static Color TierColor(double exalted) => exalted switch
    {
        < 0.1 => Color.FromArgb(124, 130, 138),   // тривиальное — приглушённо-серое
        < 1.0 => Color.FromArgb(198, 202, 208),   // дешёвое — мягкое белое
        < 5.0 => Color.FromArgb(190, 162, 86),    // среднее — приглушённое золото
        < 20.0 => Color.FromArgb(190, 130, 74),   // дорогое — приглушённая медь
        _ => Color.FromArgb(190, 96, 110),         // топ — приглушённый терракот
    };

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Point ToLocal(Point screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y);

    private Rectangle ToLocal(Rectangle screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y, screen.Width, screen.Height);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
