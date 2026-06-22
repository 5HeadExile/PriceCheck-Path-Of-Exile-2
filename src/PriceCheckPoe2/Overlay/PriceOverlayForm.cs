using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PriceCheckPoe2.Scanning;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Прозрачный click-through оверлей цен. Рисует ценник-плашку («Тинт-стекло»)
/// напротив каждой награды. Окно — **per-pixel alpha layered window**
/// (`UpdateLayeredWindow`), чтобы полупрозрачная заливка плашки смешивалась с
/// игрой, а не с фоном формы. Плавно проявляется (fade-in по альфе блендинга).
/// </summary>
public sealed class PriceOverlayForm : Form
{
    private const int EdgeMargin = 8;
    private const int Gap = 10;
    private const double DivineThreshold = 1.0;

    private IReadOnlyList<PylonScanResult> _results = Array.Empty<PylonScanResult>();
    private bool _debug;
    private byte _alpha;
    private Bitmap? _lastBmp;
    private readonly System.Windows.Forms.Timer _fadeTimer;

    public PriceOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        ShowInTaskbar = false;
        TopMost = true;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += FadeTick;
        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                _alpha = 0;
                Render();
                _fadeTimer.Start();
            }
            else
            {
                _fadeTimer.Stop();
            }
        };
    }

    /// <summary>Click-through + layered: окно не перехватывает мышь, рисуется через UpdateLayeredWindow.</summary>
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
            if (Visible)
            {
                Render();
            }
        }
    }

    public void Update(IReadOnlyList<PylonScanResult> results)
    {
        _results = results;
        if (Visible)
        {
            Render();
        }
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        _alpha = (byte)Math.Min(255, _alpha + 40);
        if (_lastBmp is not null)
        {
            ApplyLayered(_lastBmp, _alpha);
        }

        if (_alpha >= 255)
        {
            _fadeTimer.Stop();
        }
    }

    /// <summary>Рисует все плашки в ARGB-битмап и публикует через UpdateLayeredWindow.</summary>
    private void Render()
    {
        if (Width <= 0 || Height <= 0 || !IsHandleCreated)
        {
            return;
        }

        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Серое сглаживание (не ClearType): на ARGB-поверхности субпиксельный
            // ClearType не даёт корректную альфу.
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            DrawChips(g);
        }

        ApplyLayered(bmp, _alpha);
        _lastBmp?.Dispose();
        _lastBmp = bmp;
    }

    private void DrawChips(Graphics g)
    {
        var best = _results
            .SelectMany(r => r.Rewards)
            .Where(x => x.Price is not null)
            .OrderByDescending(x => x.LineTotal)
            .FirstOrDefault();

        var placed = new List<RectangleF>();

        foreach (var result in _results)
        {
            if (_debug)
            {
                using var pen = new Pen(Color.DeepSkyBlue, 2);
                g.DrawRectangle(pen, ToLocal(result.Region));
            }

            foreach (var reward in result.Rewards)
            {
                var row = ToLocal(reward.ScreenBounds);
                if (_debug)
                {
                    using var lp = new Pen(Color.FromArgb(120, 0, 255, 0), 1);
                    g.DrawRectangle(lp, row);
                }

                var model = ToModel(reward, ReferenceEquals(reward, best));
                var w = PriceChip.MeasureWidth(g, model);

                // Ставим плашку сразу после названия, не давая вылезти за экран.
                var desiredLeft = row.Right + Gap;
                var maxLeft = Width - w - EdgeMargin;
                var left = Math.Min(desiredLeft, maxLeft);
                left = Math.Max(left, EdgeMargin);
                var top = row.Y + (row.Height - PriceChip.Height) / 2f;

                var rect = new RectangleF(left, top, w, PriceChip.Height);

                // Анти-наложение: пересекающиеся плашки сдвигаем вниз.
                var guard = 0;
                while (placed.Any(p => p.IntersectsWith(rect)) && guard++ < 64)
                {
                    var lowest = placed.Where(p => p.IntersectsWith(rect)).Max(p => p.Bottom);
                    rect.Y = lowest + 2;
                }

                placed.Add(rect);
                PriceChip.Draw(g, rect, model);
            }
        }
    }

    private static PriceChip.Model ToModel(PricedReward reward, bool best)
    {
        if (reward.Price is null)
        {
            return new PriceChip.Model(Unknown: true, Best: false, Exalted: 0, MainText: "?", UnitText: null);
        }

        var exTotal = reward.LineTotal;
        var divTotal = (reward.Price.DivineValue ?? 0) * reward.Stack;
        var useDiv = divTotal >= DivineThreshold;
        var main = useDiv ? $"{divTotal:0.##} div" : $"{exTotal:0.##} ex";

        string? unit = null;
        if (reward.Stack > 1)
        {
            var u = useDiv ? (reward.Price.DivineValue ?? 0) : reward.Price.ExaltedValue;
            unit = $"{u:0.##}/шт";
        }

        return new PriceChip.Model(Unknown: false, Best: best, Exalted: exTotal, MainText: main, UnitText: unit);
    }

    private Rectangle ToLocal(Rectangle screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y, screen.Width, screen.Height);

    // --- per-pixel alpha layered window ---

    private void ApplyLayered(Bitmap bmp, byte alpha)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        var old = SelectObject(memDc, hBitmap);
        try
        {
            var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var src = new POINT { x = 0, y = 0 };
            var dst = new POINT { x = Bounds.X, y = Bounds.Y };
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = alpha,
                AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private const int ULW_ALPHA = 0x02;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] private struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc,
        ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _lastBmp?.Dispose();
        }

        base.Dispose(disposing);
    }
}
