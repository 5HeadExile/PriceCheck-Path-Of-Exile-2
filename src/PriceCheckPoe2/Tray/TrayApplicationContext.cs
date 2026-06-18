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
        _menu.AddPylonRequested += AddPylonRegion;
        _menu.ClearPylonsRequested += ClearPylons;
        _menu.TogglePriceOverlayRequested += TogglePriceOverlay;
        _menu.RescanRequested += Rescan;
        _menu.ExitRequested += ExitApp;

        _hotkeys = new HotkeyManager(_config);
        _hotkeys.MenuToggleRequested += () => OnUi(() => _menu.Toggle());
        _hotkeys.RecalibrateRequested += () => OnUi(AddPylonRegion);
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
        // поэтому отдельная перерегистрация не нужна. Параметры OCR подхватятся
        // при следующем создании сканера.
        _scanner = null;
        _ocr?.Dispose();
        _ocr = null;
    }

    private void AddPylonRegion()
    {
        _menu.Hide();
        using var overlay = new CalibrationOverlay();
        if (overlay.ShowDialog() == DialogResult.OK && overlay.Result is { } region)
        {
            region.Name = $"pylon-{_config.PylonRegions.Count + 1}";
            _config.PylonRegions.Add(region);
            _config.Save();
        }
    }

    private void ClearPylons()
    {
        _config.PylonRegions.Clear();
        _config.Save();
        _priceOverlay?.Update(Array.Empty<PylonScanResult>());
    }

    private void TogglePriceOverlay()
    {
        if (_priceOverlay is { Visible: true })
        {
            _priceOverlay.Hide();
            return;
        }

        ShowOverlayAndScan();
    }

    private void Rescan()
    {
        _menu.Hide();
        ShowOverlayAndScan();
    }

    private void ShowOverlayAndScan()
    {
        var regions = PylonRegions();
        if (regions.Count == 0)
        {
            MessageBox.Show("Сначала добавьте область пилона (меню → Добавить пилон).",
                "PriceCheck PoE2");
            return;
        }

        _priceOverlay ??= new PriceOverlayForm();
        _priceOverlay.Show();
        _ = ScanAndRenderAsync(regions);
    }

    private async Task ScanAndRenderAsync(IReadOnlyList<(string, Rectangle)> regions)
    {
        try
        {
            EnsureScanner();
            var results = await _scanner!.ScanAllAsync(regions).ConfigureAwait(true);
            _priceOverlay?.Update(results);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось оценить пилоны: {ex.Message}", "PriceCheck PoE2");
        }
    }

    private void EnsureScanner()
    {
        if (_scanner is not null)
        {
            return;
        }

        _ocr = new OcrEngine(threshold: _config.OcrThreshold, saveDebug: _config.SaveOcrDebugImages);
        // Запасной парсер из статичного файла — на случай недоступности цен.
        var aliasesPath = Path.Combine(AppContext.BaseDirectory, "Data", "reward-aliases.json");
        _parser = File.Exists(aliasesPath) ? RewardParser.FromFile(aliasesPath) : null;
        _priceCache = new PriceCache(
            new PoeNinjaClient(_config),
            TimeSpan.FromMinutes(_config.PriceRefreshMinutes));
        _scanner = new PylonScanner(_ocr, _priceCache, _config, _parser);
    }

    private IReadOnlyList<(string Id, Rectangle Region)> PylonRegions() =>
        _config.PylonRegions
            .Select(p => (p.Name, new Rectangle(p.X, p.Y, p.Width, p.Height)))
            .ToList();

    private void ToggleDebug()
    {
        if (_priceOverlay is not null)
        {
            _priceOverlay.DebugMode = !_priceOverlay.DebugMode;
        }
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
