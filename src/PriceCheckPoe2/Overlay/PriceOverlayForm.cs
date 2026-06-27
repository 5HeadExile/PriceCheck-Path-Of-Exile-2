using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PriceCheckPoe2.Scanning;
using PriceCheckPoe2.Theme;

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
    private const double DivineThreshold = 1.0;

    /// <summary>Необязательная ручная подстройка размера плашек поверх авто-масштаба (1.0 = авто).</summary>
    public double ChipScale { get; set; } = 1.0;

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

    private bool _capturable;

    /// <summary>
    /// Разрешить попадание оверлея в захват экрана (для скриншота плашек). В обычном
    /// режиме оверлей исключён из захвата (<see cref="WDA_EXCLUDEFROMCAPTURE"/>), иначе
    /// наш TopMost-оверлей попадает в CopyFromScreen детектора/OCR → плашки меняют
    /// яркость правой части → детектор решает, что панель закрылась → петля мерцания.
    /// «Режим скриншота» включает захват И замораживает детектор (см. RegionMonitor),
    /// поэтому мерцания нет.
    /// </summary>
    public bool Capturable
    {
        get => _capturable;
        set
        {
            _capturable = value;
            ApplyCaptureAffinity();
            if (Visible)
            {
                _alpha = 255; // в режиме скриншота показываем сразу, без fade
                Render();
            }
        }
    }

    private static readonly Font BadgeFont = new("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);

    /// <summary>Видимый индикатор «режим скриншота включён» — подтверждение нажатия F6.</summary>
    private void DrawScreenshotBadge(Graphics g)
    {
        var primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, Width, Height);
        const string text = "●  Скриншот-режим — нажми F6, чтобы выйти";
        var sz = g.MeasureString(text, BadgeFont);
        float padX = 14, padY = 8, rad = 8;
        var rect = new RectangleF(
            primary.X - Bounds.X + 24,
            primary.Y - Bounds.Y + 24,
            sz.Width + padX * 2,
            sz.Height + padY * 2);

        using var path = new GraphicsPath();
        var d = rad * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        using (var plate = new SolidBrush(Color.FromArgb(235, 18, 18, 22)))
        {
            g.FillPath(plate, path);
        }

        using (var border = new Pen(Color.FromArgb(220, 214, 182, 104), 1.5f))
        {
            g.DrawPath(border, path);
        }

        using (var dot = new SolidBrush(Color.FromArgb(230, 230, 90, 90)))
        {
            g.DrawString("●", BadgeFont, dot, rect.X + padX, rect.Y + padY);
        }

        var dotW = g.MeasureString("●", BadgeFont).Width;
        using var fg = new SolidBrush(Color.FromArgb(235, 235, 228, 210));
        g.DrawString(text[1..], BadgeFont, fg, rect.X + padX + dotW, rect.Y + padY);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyCaptureAffinity();
    }

    private void ApplyCaptureAffinity()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            SetWindowDisplayAffinity(Handle, _capturable ? WDA_NONE : WDA_EXCLUDEFROMCAPTURE);
        }
        catch
        {
            // старая Windows без поддержки — не критично
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
            if (_capturable)
            {
                DrawScreenshotBadge(g);
            }
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

        // Авто-масштаб: размер плашки выводим из высоты строки текста, которую измерил
        // OCR. Берём НИЗКИЙ перцентиль (40-й) высот — он стабильно попадает в «чистый»
        // текст и игнорирует завышенные bbox-артефакты (OCR иногда прихватывает полосу
        // иконок над именем или склеивает строки → высота 50-60px вместо ~25). Высокий
        // перцентиль на таких панелях раздувал плашки вдвое. Так оверлей сам подгоняется
        // под любое разрешение/UI-scale. ChipScale — необязательная подстройка (1.0).
        var heights = _results.SelectMany(r => r.Rewards)
            .Select(x => x.ScreenBounds.Height).Where(h => h > 0).OrderBy(h => h).ToList();
        float em = heights.Count > 0
            ? heights[Math.Min(heights.Count - 1, (int)(heights.Count * 0.40))]
            : 20f;
        em = (float)(Math.Clamp(em, 10, 48) * ChipScale);

        var chipH = PriceChip.HeightFor(em);
        var textGap = em * 0.45f;                  // зазор между текстом награды и плашкой
        var stackGap = Math.Max(2f, em * 0.18f);   // мин. зазор между плашками (с запасом под гало «лучшей»)
        var edge = Ui.S(EdgeMargin);

        var placed = new List<RectangleF>();

        foreach (var result in _results)
        {
            if (_debug)
            {
                using var pen = new Pen(Color.DeepSkyBlue, 2);
                g.DrawRectangle(pen, ToLocal(result.Region));
            }

            if (result.Rewards.Count == 0)
            {
                continue;
            }

            // Общий левый край колонки = правее самой длинной строки + отступ.
            // Ширину для клампа меряем БЕЗ выделения «лучшей» (ToModel(r,false)) —
            // чтобы появление/смена лучшей не сдвигала весь столбец.
            float columnLeft = result.Rewards.Max(r => ToLocal(r.ScreenBounds).Right) + textGap;
            float maxColumnLeft = Width - edge - result.Rewards.Max(r => PriceChip.MeasureWidth(g, ToModel(r, false), em));
            columnLeft = Math.Max(edge, Math.Min(columnLeft, maxColumnLeft));

            foreach (var reward in result.Rewards)
            {
                var row = ToLocal(reward.ScreenBounds);
                if (_debug)
                {
                    using var lp = new Pen(Color.FromArgb(120, 0, 255, 0), 1);
                    g.DrawRectangle(lp, row);
                }

                var model = ToModel(reward, ReferenceEquals(reward, best));
                var w = PriceChip.MeasureWidth(g, model, em);
                var top = row.Y + (row.Height - chipH) / 2f;

                var rect = new RectangleF(columnLeft, top, w, chipH);

                // Анти-наложение: держим между плашками зазор stackGap (с запасом под
                // гало «лучшей»), пересекающиеся сдвигаем вниз. Высота плашек одинакова
                // для всех (в т.ч. «лучшей»), поэтому выделение не двигает соседей.
                var guard = 0;
                var test = RectangleF.Inflate(rect, 0, stackGap);
                while (placed.Any(p => p.IntersectsWith(test)) && guard++ < 64)
                {
                    rect.Y = placed.Where(p => p.IntersectsWith(test)).Max(p => p.Bottom) + stackGap;
                    test = RectangleF.Inflate(rect, 0, stackGap);
                }

                placed.Add(rect);
                PriceChip.Draw(g, rect, model, em);
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

    private const uint WDA_NONE = 0x0;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
    [DllImport("user32.dll", ExactSpelling = true)] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

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
