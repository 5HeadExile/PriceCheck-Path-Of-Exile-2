using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Ocr;
using PriceCheckPoe2.Overlay;
using PriceCheckPoe2.Pricing;
using PriceCheckPoe2.Scanning;
using PriceCheckPoe2.Settings;

namespace PriceCheckPoe2.Tray;

/// <summary>
/// Корень жизненного цикла приложения. Держит трей-иконку, глобальные хоткеи,
/// игровое меню и оверлей цен. Главного окна нет — всё поднимается по запросу.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeys;
    private readonly MenuOverlay _menu;

    // Невидимый control для перевода событий хука в UI-поток WinForms.
    private readonly Control _marshal;

    // Пайплайн сканирования и оверлей цен создаются лениво (нужен tessdata).
    private PriceOverlayForm? _priceOverlay;
    private OcrEngine? _ocr;
    private RewardParser? _parser;
    private PriceCache? _priceCache;
    private PylonScanner? _scanner;

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
        _menu.Hide();
        using var form = new SettingsForm(_config);
        form.ShowDialog();
        // Хоткеи перечитываются HotkeyManager'ом из _config при каждом нажатии,
        // поэтому отдельная перерегистрация не нужна.
    }

    private void StartCalibration()
    {
        _menu.Hide();
        using var overlay = new CalibrationOverlay();
        if (overlay.ShowDialog() == DialogResult.OK && overlay.Result is { } profile)
        {
            profile.Name = "default";
            _config.Profiles.RemoveAll(p => p.Name == profile.Name);
            _config.Profiles.Add(profile);
            _config.ActiveProfile = profile.Name;
            _config.Save();
        }
    }

    private void TogglePriceOverlay()
    {
        if (_priceOverlay is { Visible: true })
        {
            _priceOverlay.Hide();
            return;
        }

        var region = ActiveRegion();
        if (region is null)
        {
            MessageBox.Show("Сначала откалибруйте область пилона (меню → Калибровка).",
                "PriceCheck PoE2");
            return;
        }

        _priceOverlay ??= new PriceOverlayForm();
        _priceOverlay.Show();
        _ = ScanAndRenderAsync(region.Value);
    }

    private async Task ScanAndRenderAsync(Rectangle region)
    {
        try
        {
            EnsureScanner();
            var valuations = await _scanner!.ScanAsync(region).ConfigureAwait(true);
            _priceOverlay?.Update(valuations);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось оценить пилон: {ex.Message}", "PriceCheck PoE2");
        }
    }

    private void EnsureScanner()
    {
        if (_scanner is not null)
        {
            return;
        }

        _ocr = new OcrEngine();
        var aliasesPath = Path.Combine(AppContext.BaseDirectory, "Data", "reward-aliases.json");
        _parser = RewardParser.FromFile(aliasesPath);
        _priceCache = new PriceCache(
            new PoeNinjaClient(_config),
            TimeSpan.FromMinutes(_config.PriceRefreshMinutes));
        _scanner = new PylonScanner(_ocr, _parser, _priceCache, _config);
    }

    private Rectangle? ActiveRegion()
    {
        var profile = _config.Profiles.FirstOrDefault(p => p.Name == _config.ActiveProfile)
                      ?? _config.Profiles.FirstOrDefault();
        return profile is null
            ? null
            : new Rectangle(profile.X, profile.Y, profile.Width, profile.Height);
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
            _priceOverlay?.Dispose();
            _ocr?.Dispose();
            _trayIcon.Dispose();
            _marshal.Dispose();
        }

        base.Dispose(disposing);
    }
}
