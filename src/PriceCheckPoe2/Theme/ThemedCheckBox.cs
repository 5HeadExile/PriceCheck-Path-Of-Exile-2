using System.Drawing;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>Чекбокс дизайн-системы: квадрат 16px, золотая заливка + галочка во включённом виде.</summary>
public sealed class ThemedCheckBox : Control
{
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set { _checked = value; CheckedChanged?.Invoke(this, EventArgs.Empty); Invalidate(); }
    }

    public ThemedCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = Ui.S(22);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseDown(MouseEventArgs e) { Checked = !Checked; base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bs = Ui.S(16);
        var box = new Rectangle(0, (Height - bs) / 2, bs, bs);
        if (_checked)
        {
            Draw.FillVertical(g, box, Ui.S(4), Palette.AccentLight, Palette.Accent);
            Draw.Border(g, box, Ui.S(4), Palette.AccentBorder);
            using var font = new Font("Segoe UI Semibold", Ui.Sf(11f), FontStyle.Regular, GraphicsUnit.Pixel);
            Draw.CenterText(g, "✓", font, Palette.OnAccent, box);
        }
        else
        {
            Draw.Fill(g, box, Ui.S(4), Palette.FieldMutedBg);
            Draw.Border(g, box, Ui.S(4), Palette.BorderStrong);
        }

        var label = _checked ? "вкл" : "выкл";
        using var lf = Palette.Label();
        var lx = Ui.S(24);
        var lr = new Rectangle(lx, 0, Width - lx, Height);
        TextRenderer.DrawText(g, label, lf, lr, Palette.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
