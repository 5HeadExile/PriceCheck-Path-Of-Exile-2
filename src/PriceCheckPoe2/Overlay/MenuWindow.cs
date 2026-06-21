using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PriceCheckPoe2.Theme;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Окно игрового меню (дизайн-система). Брендовый бар сверху, секции «Область» и
/// «Сканирование», ряд «Настройки/Закрыть» и опасная «Выход». Все действия —
/// через события, которые подключает <see cref="MenuOverlay"/>.
/// </summary>
public sealed class MenuWindow : RoundedForm
{
    private static int Pad => Ui.S(12);
    private readonly bool _overlayActive;

    public event Action? AddPylon;
    public event Action? ClearPylons;
    public event Action? TogglePriceOverlay;
    public event Action? Rescan;
    public event Action? OpenSettings;
    public event Action? CloseMenu;
    public event Action? Exit;

    public MenuWindow(bool overlayActive, bool hasSelection)
    {
        _overlayActive = overlayActive;
        TopMost = true;
        Size = new Size(Ui.S(320), Ui.S(372));

        var pad = Pad;
        var w = ClientSize.Width - pad * 2;
        int rowH = Ui.S(34), botH = Ui.S(32), headH = Ui.S(18), gap = Ui.S(6);
        int y = Ui.S(54);

        Controls.Add(new SectionHeader("Область", SectionTone.Faint) { Location = new Point(pad, y), Size = new Size(w, headH) });
        y += Ui.S(22);

        var add = Button("Выделить область", ButtonVariant.Primary, "", () => AddPylon?.Invoke());
        add.SetBounds(pad, y, w, rowH);
        Controls.Add(add);
        y += rowH + gap;

        var clear = Button("Сбросить выделенное", ButtonVariant.Normal, "", () => ClearPylons?.Invoke());
        clear.SetBounds(pad, y, w, rowH);
        clear.Enabled = hasSelection;
        Controls.Add(clear);
        y += rowH + Ui.S(16);

        Controls.Add(new SectionHeader("Сканирование", SectionTone.Faint) { Location = new Point(pad, y), Size = new Size(w, headH) });
        y += Ui.S(22);

        var toggle = Button(
            "Оверлей: пауза",
            ButtonVariant.Normal,
            _overlayActive ? "" : "",
            () => TogglePriceOverlay?.Invoke());
        toggle.Tag2 = _overlayActive ? "ВКЛ" : "ВЫКЛ";
        toggle.SetBounds(pad, y, w, rowH);
        Controls.Add(toggle);
        y += rowH + gap;

        var rescan = Button("Пересканировать сейчас", ButtonVariant.Normal, "", () => Rescan?.Invoke());
        rescan.SetBounds(pad, y, w, rowH);
        Controls.Add(rescan);
        y += rowH + Ui.S(12);

        // Разделитель
        var divider = new Panel { BackColor = Palette.BorderFaint, Size = new Size(w, 1), Location = new Point(pad, y) };
        Controls.Add(divider);
        y += Ui.S(13);

        var half = (w - gap) / 2;
        var settings = Button("Настройки", ButtonVariant.Normal, "", () => OpenSettings?.Invoke());
        settings.SetBounds(pad, y, half, botH);
        Controls.Add(settings);

        var close = Button("Закрыть", ButtonVariant.Normal, "", () => CloseMenu?.Invoke());
        close.SetBounds(pad + half + gap, y, w - half - gap, botH);
        Controls.Add(close);
        y += botH + gap;

        var exit = Button("Выход", ButtonVariant.Danger, "", () => Exit?.Invoke());
        exit.SetBounds(pad, y, w, botH);
        Controls.Add(exit);
    }

    private static ThemedButton Button(string text, ButtonVariant variant, string glyph, Action onClick)
    {
        var b = new ThemedButton { Text = text, Variant = variant, Glyph = glyph };
        // Откладываем действие, чтобы обработчик клика успел вернуться до того,
        // как действие закроет/уничтожит форму меню (избегаем реентрантности).
        b.Click += (_, _) => b.BeginInvoke(onClick);
        return b;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); // фон, рамка, скругление
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Брендовый бар
        var barH = Ui.S(44);
        DrawDiamond(g, new Point(Ui.S(18), barH / 2), Ui.S(10));

        using var brand = Palette.Brand();
        var bw = Draw.TrackedText(g, "PRICECHECK", brand, Palette.Text, Ui.S(34), barH / 2, Ui.Sf(1.5));

        using var sub = Palette.MonoSmall();
        TextRenderer.DrawText(g, "PoE2", sub, new Rectangle(Ui.S(34) + bw + Ui.S(8), 0, Ui.S(60), barH), Palette.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // Точка статуса справа
        var dotColor = _overlayActive ? Palette.Success : Palette.TextFaint;
        var ds = Ui.S(7);
        var dx = ClientSize.Width - Ui.S(23);
        var dy = barH / 2 - ds / 2;
        if (_overlayActive)
        {
            using var glow = new SolidBrush(Color.FromArgb(60, Palette.Success));
            g.FillEllipse(glow, dx - Ui.S(3), dy - Ui.S(3), ds + Ui.S(6), ds + Ui.S(6));
        }

        using (var dot = new SolidBrush(dotColor))
        {
            g.FillEllipse(dot, dx, dy, ds, ds);
        }

        // Нижняя линия бара
        using var pen = new Pen(Palette.BorderFaint, 1f);
        g.DrawLine(pen, 1, barH, ClientSize.Width - 2, barH);
    }

    private static void DrawDiamond(Graphics g, Point center, int size)
    {
        var state = g.Save();
        g.TranslateTransform(center.X, center.Y);
        g.RotateTransform(45);
        var rect = new Rectangle(-size / 2, -size / 2, size, size);
        using var brush = new LinearGradientBrush(rect, Palette.AccentLighter, Palette.AccentDark, LinearGradientMode.ForwardDiagonal);
        g.FillRectangle(brush, rect);
        g.Restore(state);
    }
}
