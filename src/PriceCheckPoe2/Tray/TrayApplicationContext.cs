using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Overlay;

namespace PriceCheckPoe2.Tray;

/// <summary>
/// Корень жизненного цикла приложения. Держит трей-иконку, глобальные хоткеи и
/// игровое меню. Главного окна нет — всё поднимается по запросу.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeys;
    private readonly MenuOverlay _menu;

    // Невидимый control для перевода событий хука в UI-поток WinForms.
    private readonly Control _marshal;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();

        _marshal = new Control();
        _ = _marshal.Handle; // принудительно создаём хэндл в UI-потоке

        _menu = new MenuOverlay(_config);
        _menu.SettingsRequested += OpenSettings;
        _menu.CalibrateRequested += StartCalibration;
        _menu.TogglePriceOverlayRequested += TogglePriceOverlay;
        _menu.ExitRequested += ExitApp;

        _hotkeys = new HotkeyManager(_config);
        _hotkeys.MenuToggleRequested += () => OnUi(() => _menu.Toggle());
        _hotkeys.RecalibrateRequested += () => OnUi(StartCalibration);
        _hotkeys.DebugToggleRequested += () => OnUi(ToggleDebug);
        _hotkeys.Start();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PriceCheck PoE2",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => _menu.Toggle();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть меню", null, (_, _) => _menu.Toggle());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApp());
        return menu;
    }

    /// <summary>Выполняет действие в UI-потоке (события хука приходят из фона).</summary>
    private void OnUi(Action action)
    {
        if (_marshal.InvokeRequired)
        {
            _marshal.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void OpenSettings()
    {
        // TODO(M6): открыть WPF-окно настроек (MahApps.Metro).
        _menu.Hide();
        MessageBox.Show("Окно настроек будет здесь (M6).", "PriceCheck PoE2");
    }

    private void StartCalibration()
    {
        // TODO(M3): запустить CalibrationOverlay (drag-select области пилона).
        _menu.Hide();
        MessageBox.Show("Калибровка области (M3).", "PriceCheck PoE2");
    }

    private void TogglePriceOverlay()
    {
        // TODO(M4/M5): включить/выключить click-through оверлей цен.
        MessageBox.Show("Оверлей цен (M4/M5).", "PriceCheck PoE2");
    }

    private void ToggleDebug()
    {
        // TODO(M3): показать debug-боксы распознавания.
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeys.Dispose();
            _menu.Dispose();
            _trayIcon.Dispose();
            _marshal.Dispose();
        }

        base.Dispose(disposing);
    }
}
