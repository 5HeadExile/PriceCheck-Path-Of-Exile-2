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
    private const int Pad = 12;
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
        Size = new Size(320, 372);

        var w = ClientSize.Width - Pad * 2; // 296
        int y = 54;

        Controls.Add(new SectionHeader("Область", SectionTone.Faint) { Location = new Point(Pad, y), Size = new Size(w, 18) });
        y += 22;

        var add = Button("Выделить область", ButtonVariant.Primary, "", () => AddPylon?.Invoke());
        add.SetBounds(Pad, y, w, 34);
        Controls.Add(add);
        y += 40;

        var clear = Button("Сбросить выделенное", ButtonVariant.Normal, "", () => ClearPylons?.Invoke());
        clear.SetBounds(Pad, y, w, 34);
        clear.Enabled = hasSelection;
        Controls.Add(clear);
        y += 40 + 10;

        Controls.Add(new SectionHeader("Сканирование", SectionTone.Faint) { Location = new Point(Pad, y), Size = new Size(w, 18) });
        y += 22;

        var toggle = Button(
            "Оверлей: пауза / возобновить",
            ButtonVariant.Normal,
            _overlayActive ? "" : "",
            () => TogglePriceOverlay?.Invoke());
        toggle.Tag2 = _overlayActive ? "ВКЛ" : "ВЫКЛ";
        toggle.SetBounds(Pad, y, w, 34);
        Controls.Add(toggle);
        y += 40;

        var rescan = Button("Пересканировать сейчас", ButtonVariant.Normal, "", () => Rescan?.Invoke());
        rescan.SetBounds(Pad, y, w, 34);
        Controls.Add(rescan);
        y += 34 + 12;

        // Разделитель
        var divider = new Panel { BackColor = Palette.BorderFaint, Size = new Size(w, 1), Location = new Point(Pad, y) };
        Controls.Add(divider);
        y += 13;

        var half = (w - 6) / 2;
        var settings = Button("Настройки", ButtonVariant.Normal, "", () => OpenSettings?.Invoke());
        settings.SetBounds(Pad, y, half, 32);
        Controls.Add(settings);

        var close = Button("Закрыть", ButtonVariant.Normal, "", () => CloseMenu?.Invoke());
        close.SetBounds(Pad + half + 6, y, w - half - 6, 32);
        Controls.Add(close);
        y += 38;

        var exit = Button("Выход", ButtonVariant.Danger, "", () => Exit?.Invoke());
        exit.SetBounds(Pad, y, w, 32);
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

        // Брендовый бар (44h)
        DrawDiamond(g, new Point(18, 22));

        using var brand = Palette.Brand();
        var bw = Draw.TrackedText(g, "PRICECHECK", brand, Palette.Text, 34, 22, 1.5f);

        using var sub = Palette.MonoSmall();
        TextRenderer.DrawText(g, "PoE2", sub, new Rectangle(34 + bw + 8, 0, 60, 44), Palette.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // Точка статуса справа
        var dotColor = _overlayActive ? Palette.Success : Palette.TextFaint;
        if (_overlayActive)
        {
            using var glow = new SolidBrush(Color.FromArgb(60, Palette.Success));
            g.FillEllipse(glow, ClientSize.Width - 26, 18, 12, 12);
        }

        using (var dot = new SolidBrush(dotColor))
        {
            g.FillEllipse(dot, ClientSize.Width - 23, 21, 7, 7);
        }

        // Нижняя линия бара
        using var pen = new Pen(Palette.BorderFaint, 1f);
        g.DrawLine(pen, 1, 44, ClientSize.Width - 2, 44);
    }

    private static void DrawDiamond(Graphics g, Point center)
    {
        var state = g.Save();
        g.TranslateTransform(center.X, center.Y);
        g.RotateTransform(45);
        var rect = new Rectangle(-5, -5, 10, 10);
        using var brush = new LinearGradientBrush(rect, Palette.AccentLighter, Palette.AccentDark, LinearGradientMode.ForwardDiagonal);
        g.FillRectangle(brush, rect);
        g.Restore(state);
    }
}
