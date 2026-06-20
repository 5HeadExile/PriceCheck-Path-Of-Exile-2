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
        Height = 22;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseDown(MouseEventArgs e) { Checked = !Checked; base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var box = new Rectangle(0, (Height - 16) / 2, 16, 16);
        if (_checked)
        {
            Draw.FillVertical(g, box, 4, Palette.AccentLight, Palette.Accent);
            Draw.Border(g, box, 4, Palette.AccentBorder);
            using var font = new Font("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
            Draw.CenterText(g, "✓", font, Palette.OnAccent, box);
        }
        else
        {
            Draw.Fill(g, box, 4, Palette.FieldMutedBg);
            Draw.Border(g, box, 4, Palette.BorderStrong);
        }

        var label = _checked ? "вкл" : "выкл";
        using var lf = Palette.Label();
        var lr = new Rectangle(24, 0, Width - 24, Height);
        TextRenderer.DrawText(g, label, lf, lr, Palette.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
