using System.Drawing;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Поле-«клавиша» для хоткея. Показывает текущую клавишу (напр. «F2»). Клик →
/// режим захвата: следующее нажатие записывается как хоткей (имя SharpHook Vc*).
/// Хранит и отдаёт Vc-имя для конфига.
/// </summary>
public sealed class KeycapInput : Control
{
    private bool _capturing;

    /// <summary>Имя клавиши SharpHook (<c>VcF2</c>). Пусто — не задано.</summary>
    public string KeyCodeName { get; private set; } = string.Empty;

    public KeycapInput()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(Ui.S(56), Ui.S(28));
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public void SetKey(string vcName)
    {
        KeyCodeName = vcName ?? string.Empty;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _capturing = true;
        Focus();
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnLostFocus(EventArgs e) { _capturing = false; Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_capturing)
        {
            var name = KeyName.FromWinForms(e.KeyCode);
            if (name is not null)
            {
                KeyCodeName = name;
            }

            _capturing = false;
            e.Handled = true;
            Invalidate();
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rad = Ui.S(5);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Draw.Fill(g, r, rad, Palette.Hex("#16161B"));
        Draw.Border(g, r, rad, _capturing ? Palette.Accent : Palette.BorderStrong);

        // Нижний внутренний блик — эффект физической клавиши.
        using (var pen = new Pen(Color.FromArgb(102, 0, 0, 0), Ui.Sf(2)))
        {
            g.DrawLine(pen, r.X + rad, r.Bottom - 1, r.Right - rad, r.Bottom - 1);
        }

        if (_capturing)
        {
            Draw.FocusGlow(g, r, rad);
        }

        var text = _capturing ? "…" : KeyName.Display(KeyCodeName);
        using var font = Palette.Mono12();
        Draw.CenterText(g, text, font, _capturing ? Palette.Accent : Palette.Text, r);
    }
}
