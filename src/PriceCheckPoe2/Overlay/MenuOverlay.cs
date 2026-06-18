using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Игровое меню, открываемое по хоткею. Состоит из двух форм:
/// <list type="bullet">
/// <item>затемняющий слой на весь виртуальный экран (полупрозрачный чёрный);</item>
/// <item>чёткое окно меню по центру с кнопками действий.</item>
/// </list>
/// Повторный вызов <see cref="Toggle"/>, Esc или клик по затемнению — закрывают.
/// Рассчитано на игру в режиме windowed-fullscreen.
/// </summary>
public sealed class MenuOverlay : IDisposable
{
    private readonly AppConfig _config;
    private Form? _dim;
    private Form? _menu;

    public event Action? SettingsRequested;
    public event Action? AddPylonRequested;
    public event Action? ClearPylonsRequested;
    public event Action? TogglePriceOverlayRequested;
    public event Action? RescanRequested;
    public event Action? ExitRequested;

    public bool IsOpen => _dim is not null;

    public MenuOverlay(AppConfig config) => _config = config;

    public void Toggle()
    {
        if (IsOpen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        if (IsOpen)
        {
            return;
        }

        var bounds = SystemInformation.VirtualScreen;

        _dim = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Bounds = bounds,
            BackColor = Color.Black,
            Opacity = Math.Clamp(_config.MenuDimOpacity, 0.0, 1.0),
            ShowInTaskbar = false,
            TopMost = true,
        };
        _dim.Click += (_, _) => Hide();
        _dim.KeyDown += OnKeyDown;
        _dim.KeyPreview = true;

        _menu = BuildMenuForm(bounds);

        _dim.Show();
        _menu.Show();
        _menu.Activate();
    }

    public void Hide()
    {
        _menu?.Close();
        _menu?.Dispose();
        _menu = null;

        _dim?.Close();
        _dim?.Dispose();
        _dim = null;
    }

    private Form BuildMenuForm(Rectangle screen)
    {
        var menu = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(320, 380),
            BackColor = Color.FromArgb(24, 24, 28),
            ShowInTaskbar = false,
            TopMost = true,
            KeyPreview = true,
        };
        menu.Location = new Point(
            screen.X + (screen.Width - menu.Width) / 2,
            screen.Y + (screen.Height - menu.Height) / 2);
        menu.KeyDown += OnKeyDown;

        var title = new Label
        {
            Text = "PriceCheck PoE2",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 56,
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 8, 24, 24),
        };

        layout.Controls.Add(MakeButton("Выделить область", () => AddPylonRequested?.Invoke()));
        layout.Controls.Add(MakeButton("Сбросить выделенное", () => ClearPylonsRequested?.Invoke()));
        layout.Controls.Add(MakeButton("Оверлей: пауза / возобновить", () => TogglePriceOverlayRequested?.Invoke()));
        layout.Controls.Add(MakeButton("Пересканировать сейчас", () => RescanRequested?.Invoke()));
        layout.Controls.Add(MakeButton("Настройки", () => SettingsRequested?.Invoke()));
        layout.Controls.Add(MakeButton("Закрыть меню", Hide));
        layout.Controls.Add(MakeButton("Выход", () => ExitRequested?.Invoke()));

        menu.Controls.Add(layout);
        menu.Controls.Add(title);
        return menu;
    }

    private Button MakeButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            Width = 272,
            Height = 36,
            Margin = new Padding(0, 6, 0, 0),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(40, 40, 48),
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        // Откладываем действие, чтобы обработчик клика успел вернуться до того,
        // как действие закроет/уничтожит форму меню (избегаем реентрантности).
        button.Click += (_, _) => button.BeginInvoke(onClick);
        return button;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
        }
    }

    public void Dispose() => Hide();
}
