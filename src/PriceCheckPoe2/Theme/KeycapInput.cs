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
    private bool _hover;

    /// <summary>Имя клавиши SharpHook (<c>VcF2</c>). Пусто — не задано.</summary>
    public string KeyCodeName { get; private set; } = string.Empty;

    public KeycapInput()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(56, 28);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public void SetKey(string vcName)
    {
        KeyCodeName = vcName ?? string.Empty;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

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

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Draw.Fill(g, r, 5, Palette.Hex("#16161B"));
        Draw.Border(g, r, 5, _capturing ? Palette.Accent : (_hover ? Palette.BorderStrong : Palette.BorderStrong));

        // Нижний внутренний блик — эффект физической клавиши.
        using (var pen = new Pen(Color.FromArgb(102, 0, 0, 0), 2f))
        {
            g.DrawLine(pen, r.X + 5, r.Bottom - 1, r.Right - 5, r.Bottom - 1);
        }

        if (_capturing)
        {
            Draw.FocusGlow(g, r, 5);
        }

        var text = _capturing ? "…" : KeyName.Display(KeyCodeName);
        using var font = Palette.Mono();
        Draw.CenterText(g, text, font, _capturing ? Palette.Accent : Palette.Text, r);
    }
}
