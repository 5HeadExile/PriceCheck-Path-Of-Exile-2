using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Scanning;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Прозрачный click-through оверлей: рисует цену напротив каждой награды
/// (по координатам строки OCR) и компактный итог по каждому пилону. Лучший
/// пилон подсвечивается. В debug-режиме рисует рамки области и строк.
/// </summary>
public sealed class PriceOverlayForm : Form
{
    private IReadOnlyList<PylonScanResult> _results = Array.Empty<PylonScanResult>();
    private bool _debug;

    public PriceOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // делает фон полностью прозрачным
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var bestId = _results
            .OrderByDescending(r => r.TotalExalted)
            .FirstOrDefault()?.PylonId;

        using var totalFont = new Font("Segoe UI", 12F, FontStyle.Bold);
        using var lineFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var bestBrush = new SolidBrush(Color.Lime);
        using var totalBrush = new SolidBrush(Color.Gold);
        using var priceBrush = new SolidBrush(Color.Gold);
        using var unknownBrush = new SolidBrush(Color.OrangeRed);
        using var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
        using var regionPen = new Pen(Color.DeepSkyBlue, 2);
        using var linePen = new Pen(Color.FromArgb(120, 0, 255, 0), 1);

        foreach (var result in _results)
        {
            var isBest = result.PylonId == bestId;

            if (_debug)
            {
                var rr = ToLocal(result.Region);
                e.Graphics.DrawRectangle(regionPen, rr);
            }

            // Итог по пилону — над областью.
            var origin = ToLocal(result.Region.Location);
            var header = isBest
                ? $"★ {result.TotalExalted:0.#} ex"
                : $"{result.TotalExalted:0.#} ex";
            DrawWithShadow(e.Graphics, header, totalFont,
                isBest ? bestBrush : totalBrush, shadow, origin.X, origin.Y - 26);

            // Цена напротив каждой награды.
            foreach (var reward in result.Rewards)
            {
                var b = ToLocal(reward.ScreenBounds);
                if (_debug)
                {
                    e.Graphics.DrawRectangle(linePen, b);
                }

                var text = reward.Price is null
                    ? "?"
                    : $"{reward.LineTotal:0.##} ex";
                var brush = reward.Price is null ? unknownBrush : priceBrush;

                // Справа от строки, по центру её высоты.
                var x = b.Right + 8;
                var y = b.Y + Math.Max(0, (b.Height - lineFont.Height) / 2);
                DrawWithShadow(e.Graphics, text, lineFont, brush, shadow, x, y);
            }
        }
    }

    private static void DrawWithShadow(
        Graphics g, string text, Font font, Brush brush, Brush shadow, int x, int y)
    {
        g.DrawString(text, font, shadow, x + 1, y + 1);
        g.DrawString(text, font, brush, x, y);
    }

    private Point ToLocal(Point screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y);

    private Rectangle ToLocal(Rectangle screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y, screen.Width, screen.Height);
}
