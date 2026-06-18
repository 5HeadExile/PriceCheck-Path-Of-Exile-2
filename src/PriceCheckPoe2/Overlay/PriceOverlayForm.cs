using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Scanning;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Прозрачный click-through оверлей: рисует суммарную ценность каждого пилона
/// у его области и подсвечивает лучший выбор. В debug-режиме рисует рамки
/// откалиброванных областей.
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

        // Лучший пилон — с максимальной суммарной ценностью.
        var bestId = _results
            .OrderByDescending(r => r.Valuation.TotalExalted)
            .FirstOrDefault()?.Valuation.PylonId;

        using var titleFont = new Font("Segoe UI", 13F, FontStyle.Bold);
        using var lineFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        using var best = new SolidBrush(Color.Lime);
        using var normal = new SolidBrush(Color.Gold);
        using var dim = new SolidBrush(Color.Gainsboro);
        using var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        using var debugPen = new Pen(Color.DeepSkyBlue, 2);

        foreach (var result in _results)
        {
            var v = result.Valuation;
            var p = ToLocal(result.Region.Location);
            var isBest = v.PylonId == bestId;

            if (_debug)
            {
                var r = result.Region;
                e.Graphics.DrawRectangle(debugPen, p.X, p.Y, r.Width, r.Height);
            }

            var header = isBest
                ? $"★ {v.PylonId}: {v.TotalExalted:0.0} ex"
                : $"{v.PylonId}: {v.TotalExalted:0.0} ex";

            // Тень для читаемости поверх любого фона.
            e.Graphics.DrawString(header, titleFont, shadow, p.X + 2, p.Y - 26);
            e.Graphics.DrawString(header, titleFont, isBest ? best : normal, p.X, p.Y - 28);

            var y = p.Y + result.Region.Height + 2;
            foreach (var (reward, price, lineTotal) in v.Lines)
            {
                var text = price is null
                    ? $"{reward.Stack}x {reward.Name}: ?"
                    : $"{reward.Stack}x {reward.Name}: {lineTotal:0.0} ex";
                e.Graphics.DrawString(text, lineFont, dim, p.X, y);
                y += 18;
            }
        }
    }

    private Point ToLocal(Point screen) =>
        new(screen.X - Bounds.X, screen.Y - Bounds.Y);
}
