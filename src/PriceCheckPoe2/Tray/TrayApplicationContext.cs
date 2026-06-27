using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Ocr;
using PriceCheckPoe2.Overlay;
using PriceCheckPoe2.Pricing;
using PriceCheckPoe2.Scanning;
using PriceCheckPoe2.Settings;

namespace PriceCheckPoe2.Tray;

/// <summary>
/// Корень жизненного цикла приложения. Держит трей-иконку, глобальные хоткеи,
/// игровое меню и оверлей цен. Фоновый <see cref="RegionMonitor"/> сам следит за
/// откалиброванными областями: открыл пилон → цены показались, закрыл → скрылись.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeys;
    private readonly MenuOverlay _menu;

    // Невидимый control для перевода фоновых событий (хук, монитор) в UI-поток.
    private readonly Control _marshal;

    private PriceOverlayForm? _priceOverlay;
    private OcrEngine? _ocr;
    private RewardParser? _parser;
    private PriceCache? _priceCache;
    private PylonScanner? _scanner;
    private RegionMonitor? _monitor;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();

        _marshal = new Control();
        _ = _marshal.Handle; // принудительно создаём хэндл в UI-потоке

        _menu = new MenuOverlay(_config);
        // Тег ВКЛ/ВЫКЛ в меню отражает текущее состояние авто-оверлея.
        _menu.IsOverlayActive = () => _monitor is { Paused: false };
        _menu.SettingsRequested += OpenSettings;
        _menu.AddPylonRequested += AddPylonRegion;
        _menu.ClearPylonsRequested += ClearPylons;
        _menu.TogglePriceOverlayRequested += ToggleMonitor;
        _menu.RescanRequested += Rescan;
        _menu.ExitRequested += ExitApp;

        _hotkeys = new HotkeyManager(_config);
        _hotkeys.MenuToggleRequested += () => OnUi(() => _menu.Toggle());
        _hotkeys.RecalibrateRequested += () => OnUi(AddPylonRegion);
        _hotkeys.DebugToggleRequested += () => OnUi(ToggleDebug);
        _hotkeys.ScreenshotToggleRequested += () => OnUi(ToggleScreenshotMode);
        _hotkeys.Start();

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "PriceCheck PoE2",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => _menu.Toggle();

        // Запускаем фоновый мониторинг сразу — он сам покажет цены, когда панель
        // окажется открытой (в т.ч. сразу после калибровки области).
        EnsurePipeline();
    }

    /// <summary>Иконка приложения для трея: грузим из .ico рядом с exe в малом размере
    /// (чёткость в трее), с откатом на иконку exe и системную.</summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "PriceCheck.ico");
            if (File.Exists(path))
            {
                return new Icon(path, SystemInformation.SmallIconSize);
            }

            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
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

    /// <summary>Выполняет действие в UI-потоке (фоновые события приходят не из него).</summary>
    private void OnUi(Action action)
    {
        if (_marshal.IsDisposed)
        {
            return;
        }

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
        // Лига/URL/порог OCR могли измениться — пересобираем пайплайн.
        RebuildPipeline();
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

            // Монитор сам обнаружит открытую панель и оценит её; сбрасываем кэш,
            // чтобы только что добавленная область точно пересканировалась.
            EnsurePipeline();
            _monitor?.ForceRescan();
        }
    }

    private void ClearPylons()
    {
        _config.PylonRegions.Clear();
        _config.Save();
        _monitor?.ForceRescan();
        _priceOverlay?.Hide();
    }

    /// <summary>Пауза/возобновление авто-оверлея.</summary>
    private void ToggleMonitor()
    {
        EnsurePipeline();
        if (_monitor is not null)
        {
            _monitor.Paused = !_monitor.Paused;
        }
    }

    private void Rescan()
    {
        _menu.Hide();
        EnsurePipeline();
        if (_monitor is not null)
        {
            _monitor.Paused = false;
            _monitor.ForceRescan();
        }
    }

    private void ShowResults(IReadOnlyList<PylonScanResult> results)
    {
        if (_priceOverlay is null || _priceOverlay.IsDisposed)
        {
            return;
        }

        if (!_priceOverlay.Visible)
        {
            _priceOverlay.Show();
        }

        _priceOverlay.Update(results);
        _priceOverlay.TopMost = true; // держим поверх игры
    }

    private void HideOverlay() => _priceOverlay?.Hide();

    /// <summary>Создаёт пайплайн и запускает мониторинг, если ещё не запущен.</summary>
    private void EnsurePipeline()
    {
        if (_monitor is not null)
        {
            return;
        }

        try
        {
            _ocr = new OcrEngine(threshold: _config.OcrThreshold, saveDebug: _config.SaveOcrDebugImages);
            // Запасной парсер из статичного файла — на случай недоступности цен.
            var aliasesPath = Path.Combine(AppContext.BaseDirectory, "Data", "reward-aliases.json");
            _parser = File.Exists(aliasesPath) ? RewardParser.FromFile(aliasesPath) : null;
            _priceCache = new PriceCache(
                new PoeNinjaClient(_config),
                TimeSpan.FromMinutes(_config.PriceRefreshMinutes));
            _scanner = new PylonScanner(_ocr, _priceCache, _config, _parser);

            _priceOverlay ??= new PriceOverlayForm();
            _priceOverlay.ChipScale = _config.PriceChipScale;
            _ = _priceOverlay.Handle; // создаём хэндл в UI-потоке

            _monitor = new RegionMonitor(
                _config,
                _scanner,
                results => OnUi(() => ShowResults(results)),
                () => OnUi(HideOverlay));
            _monitor.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить оценку наград: {ex.Message}", "PriceCheck PoE2");
        }
    }

    private void RebuildPipeline()
    {
        _monitor?.Dispose();
        _monitor = null;
        _ocr?.Dispose();
        _ocr = null;
        _scanner = null;
        _priceCache = null;
        EnsurePipeline();
    }

    private void ToggleDebug()
    {
        EnsurePipeline();
        if (_priceOverlay is not null)
        {
            _priceOverlay.DebugMode = !_priceOverlay.DebugMode;
        }
    }

    private bool _screenshotMode;

    /// <summary>
    /// Режим скриншота: размораживает захват оверлея (плашки попадут в скриншот) и
    /// замораживает детектор (чтобы он не видел свои плашки и не мерцал). Повторное
    /// нажатие возвращает обычную работу.
    /// </summary>
    private void ToggleScreenshotMode()
    {
        EnsurePipeline();

        // Входим в режим только когда цены реально на экране — иначе заморозка
        // детектора заблокировала бы оценку следующего пилона.
        if (!_screenshotMode && _priceOverlay is not { Visible: true })
        {
            _trayIcon.BalloonTipTitle = "PriceCheck PoE2";
            _trayIcon.BalloonTipText = "Открой пилон (чтобы появились цены), затем нажми F6.";
            _trayIcon.ShowBalloonTip(2500);
            return;
        }

        _screenshotMode = !_screenshotMode;

        if (_monitor is not null)
        {
            _monitor.Frozen = _screenshotMode;
        }

        if (_priceOverlay is not null)
        {
            _priceOverlay.Capturable = _screenshotMode;
        }

        _trayIcon.BalloonTipTitle = "PriceCheck PoE2";
        _trayIcon.BalloonTipText = _screenshotMode
            ? "Режим скриншота ВКЛ: плашки видны для захвата, детектор заморожен. F6 — выключить."
            : "Режим скриншота ВЫКЛ: обычная работа.";
        _trayIcon.ShowBalloonTip(2500);
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
            _monitor?.Dispose();
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
