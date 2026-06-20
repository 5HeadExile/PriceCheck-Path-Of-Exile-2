using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Базовая борд-лесс форма дизайн-системы: скруглённые углы, вертикальный
/// градиент фона, тонкая рамка, тень окна и перетаскивание за верхнюю полосу.
/// </summary>
public class RoundedForm : Form
{
    private const int CornerRadius = 8;
    private const int DragBarHeight = 46;

    protected RoundedForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        BackColor = Palette.WindowBottom;
        Font = Palette.Label();
        // Раскладка полностью ручная в пикселях — отключаем авто-масштаб WinForms
        // (по умолчанию AutoScaleMode.Font раздувал всё окно от размера шрифта).
        AutoScaleMode = AutoScaleMode.None;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRegion();
        Invalidate();
    }

    private void ApplyRegion()
    {
        using var path = Draw.RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Draw.FillVertical(g, r, CornerRadius, Palette.WindowTop, Palette.WindowBottom);
        Draw.Border(g, r, CornerRadius, Palette.BorderStrong);
        Draw.TopHighlight(g, r, CornerRadius, Color.FromArgb(10, 255, 255, 255));
    }

    // Перетаскивание окна за верхнюю полосу.
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Y <= DragBarHeight)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 0x2 /*HTCAPTION*/, 0);
        }
    }
}
