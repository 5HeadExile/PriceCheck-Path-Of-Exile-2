using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Pricing;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Прозрачный click-through оверлей: рисует цены наград и итог по пилону поверх
/// игры, не перехватывая ввод.
/// TODO(M4/M5): позиционировать строки относительно области пилона и подсветить
/// лучший пилон по результату <see cref="PylonEvaluator"/>.
/// </summary>
public sealed class PriceOverlayForm : Form
{
    private IReadOnlyList<PylonValuation> _valuations = Array.Empty<PylonValuation>();

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

    public void Update(IReadOnlyList<PylonValuation> valuations)
    {
        _valuations = valuations;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var font = new Font("Segoe UI", 12F, FontStyle.Bold);
        using var brush = new SolidBrush(Color.Gold);

        var y = 40;
        foreach (var v in _valuations)
        {
            e.Graphics.DrawString(
                $"{v.PylonId}: {v.TotalExalted:0.0} ex",
                font, brush, 40, y);
            y += 28;
        }
    }
}
