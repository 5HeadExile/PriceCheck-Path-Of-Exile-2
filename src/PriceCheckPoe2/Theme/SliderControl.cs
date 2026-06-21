using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Слайдер 0..100 (дизайн-система): тонкий трек, золотая заливка до позиции,
/// круглая ручка и моно-чип со значением «NN%» справа.
/// </summary>
public sealed class SliderControl : Control
{
    private int _value;
    private bool _drag;
    private static int ChipWidth => Ui.S(46);
    private static int HandleR => Ui.S(7);

    public event EventHandler? ValueChanged;

    public int Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (v == _value)
            {
                return;
            }

            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public SliderControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = Ui.S(24);
        Cursor = Cursors.Hand;
    }

    private Rectangle TrackArea => new(HandleR, 0, Width - ChipWidth - Ui.S(8) - HandleR * 2, Height);

    protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }

    private void SetFromX(int x)
    {
        var track = TrackArea;
        var ratio = (x - track.X) / (double)Math.Max(1, track.Width);
        Value = (int)Math.Round(Math.Clamp(ratio, 0, 1) * 100);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var track = TrackArea;
        var midY = Height / 2;
        var th = Ui.S(4);
        var trackRect = new Rectangle(track.X, midY - th / 2, track.Width, th);

        // Трек
        Draw.Fill(g, trackRect, Ui.S(2), Palette.BorderFaint);

        // Заполнение
        var fillW = (int)(track.Width * (_value / 100.0));
        if (fillW > 0)
        {
            var fillRect = new Rectangle(track.X, midY - th / 2, fillW, th);
            using var brush = new LinearGradientBrush(
                new Rectangle(track.X, midY - th / 2, Math.Max(1, track.Width), th),
                Palette.AccentDark, Palette.AccentLight, LinearGradientMode.Horizontal);
            using var path = Draw.RoundedRect(fillRect, Ui.S(2));
            g.FillPath(brush, path);
        }

        // Ручка
        var hx = track.X + fillW;
        var handle = new Rectangle(hx - HandleR, midY - HandleR, HandleR * 2, HandleR * 2);
        using (var hb = new LinearGradientBrush(handle, Palette.Hex("#F0E0B4"), Palette.Accent, LinearGradientMode.Vertical))
        {
            g.FillEllipse(hb, handle);
        }

        using (var hp = new Pen(Palette.AccentBorder, 1f))
        {
            g.DrawEllipse(hp, handle);
        }

        // Чип значения
        var chipH = Ui.S(22);
        var chip = new Rectangle(Width - ChipWidth, midY - chipH / 2, ChipWidth - 1, chipH);
        Draw.Fill(g, chip, Ui.S(4), Palette.InputBg);
        Draw.Border(g, chip, Ui.S(4), Palette.BorderQuiet);
        using var font = Palette.Mono();
        Draw.CenterText(g, $"{_value}%", font, Palette.Text, chip);
    }
}
