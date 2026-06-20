using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Theme;

namespace PriceCheckPoe2.Overlay;

/// <summary>
/// Игровое меню, открываемое по хоткею. Состоит из двух форм:
/// затемняющий слой на весь виртуальный экран + чёткое окно меню по центру
/// (дизайн-система «Воронёная сталь и сдержанное золото»). Повторный
/// <see cref="Toggle"/>, Esc или клик по затемнению — закрывают.
/// </summary>
public sealed class MenuOverlay : IDisposable
{
    private readonly AppConfig _config;
    private Form? _dim;
    private MenuWindow? _menu;

    public event Action? SettingsRequested;
    public event Action? AddPylonRequested;
    public event Action? ClearPylonsRequested;
    public event Action? TogglePriceOverlayRequested;
    public event Action? RescanRequested;
    public event Action? ExitRequested;

    /// <summary>Активен ли авто-оверлей (для тега ВКЛ/ВЫКЛ). Задаётся снаружи.</summary>
    public Func<bool>? IsOverlayActive { get; set; }

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

        _menu = new MenuWindow(
            overlayActive: IsOverlayActive?.Invoke() ?? false,
            hasSelection: _config.PylonRegions.Count > 0);
        _menu.StartPosition = FormStartPosition.Manual;
        _menu.Location = new Point(
            bounds.X + (bounds.Width - _menu.Width) / 2,
            bounds.Y + (bounds.Height - _menu.Height) / 2);
        _menu.KeyPreview = true;
        _menu.KeyDown += OnKeyDown;

        _menu.AddPylon += () => AddPylonRequested?.Invoke();
        _menu.ClearPylons += () => ClearPylonsRequested?.Invoke();
        _menu.TogglePriceOverlay += () => TogglePriceOverlayRequested?.Invoke();
        _menu.Rescan += () => RescanRequested?.Invoke();
        _menu.OpenSettings += () => SettingsRequested?.Invoke();
        _menu.CloseMenu += Hide;
        _menu.Exit += () => ExitRequested?.Invoke();

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

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
        }
    }

    public void Dispose() => Hide();
}
