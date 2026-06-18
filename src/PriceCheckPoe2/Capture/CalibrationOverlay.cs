using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Capture;

/// <summary>
/// Полноэкранная форма для разового drag-select области пилона. Пользователь
/// тянет рамку мышью; результат возвращается как <see cref="CalibrationProfile"/>.
/// TODO(M3): дорисовать подсказку и привязку к активному профилю.
/// </summary>
public sealed class CalibrationOverlay : Form
{
    private Point _start;
    private Rectangle _selection;
    private bool _dragging;

    public CalibrationProfile? Result { get; private set; }

    public CalibrationOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.35;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _start = e.Location;
        _dragging = true;
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            _selection = Rectangle.FromLTRB(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        if (_selection is { Width: > 4, Height: > 4 })
        {
            Result = new CalibrationProfile
            {
                X = Bounds.X + _selection.X,
                Y = Bounds.Y + _selection.Y,
                Width = _selection.Width,
                Height = _selection.Height,
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_selection is { Width: > 0, Height: > 0 })
        {
            using var pen = new Pen(Color.Lime, 2);
            e.Graphics.DrawRectangle(pen, _selection);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        base.OnKeyDown(e);
    }
}
