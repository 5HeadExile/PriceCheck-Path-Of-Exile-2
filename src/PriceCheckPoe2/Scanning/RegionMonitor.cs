using System.Drawing;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Scanning;

/// <summary>
/// Фоновый цикл, который сам следит за откалиброванными областями: как только
/// панель пилона открывается (яркость с гистерезисом) — сканирует её и показывает
/// цены; когда закрывается — прячет оверлей. Повторный OCR запускается только при
/// открытии или изменении содержимого (по сигнатуре), а не каждый тик — это
/// «лучше», чем безусловный поллинг референса.
/// <para>Confirm-gate: яркость лишь решает, стоит ли запускать OCR; оверлей
/// показывается, только если OCR реально распознал список наград (≥ ConfirmRewards
/// с ценой). Поэтому случайная яркость при беге по карте не даёт ложных показов.</para>
/// </summary>
public sealed class RegionMonitor : IDisposable
{
    // Порог яркости открытия/закрытия и мёртвая зона между ними (анти-мерцание).
    private const int OpenBrightness = 100;
    private const int CloseBrightness = 80;
    private const int OpenStreak = 2;   // светлых кадров подряд, чтобы открыть
    private const int CloseStreak = 2;  // тёмных кадров подряд, чтобы закрыть (быстрее прячем)
    private const int OpenCycleMs = 150;
    private const int IdleCycleMs = 250;
    // Насколько должна измениться сигнатура, чтобы пересканировать ОТКРЫТУЮ панель.
    // Высокий порог намеренно: переключение пилонов и так идёт через тёмный→светлый
    // (сбрасывает Scanned → форсит рескан на открытии), поэтому рескан «на лету» нужен
    // редко. Низкий порог ловил мерцание игрового фона сквозь полупрозрачную книгу →
    // постоянные много-проходные OCR-сканы блокировали цикл (тормоза + залипание цен).
    private const long SignatureEpsilon = 8000;
    // Не чаще, чем раз в N мс запускаем OCR (бережёт CPU при беге по карте).
    private const int MinOcrIntervalMs = 350;
    // Confirm-gate: показываем область, только если OCR распознал столько известных
    // наград. Случайная яркость в мире не читается как список наград → ложных нет.
    private const int ConfirmRewards = 2;

    private sealed class RegionState
    {
        public int BrightStreak;
        public int DarkStreak;
        public bool IsOpen;
        public long LastSignature;
        public long ScannedSignature = long.MinValue;
        public bool Scanned;
    }

    private readonly AppConfig _config;
    private readonly PylonScanner _scanner;
    private readonly ListDetector _detector = new();
    private readonly Action<IReadOnlyList<PylonScanResult>> _onResults;
    private readonly Action _onHide;
    private readonly Dictionary<string, RegionState> _states = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTime _lastOcrUtc = DateTime.MinValue;

    /// <summary>Пауза: оверлей скрыт и сканирование не идёт (по кнопке в меню).</summary>
    public bool Paused { get; set; }

    /// <summary>
    /// Заморозка: цикл ничего не делает (не семплит, не сканит, не прячет) — оверлей
    /// остаётся как был показан. Для «режима скриншота»: при разрешённом захвате окна
    /// детектор не должен видеть свои же плашки и мерцать.
    /// </summary>
    public bool Frozen { get; set; }

    public bool IsRunning => _loop is { IsCompleted: false };

    public RegionMonitor(
        AppConfig config,
        PylonScanner scanner,
        Action<IReadOnlyList<PylonScanResult>> onResults,
        Action onHide)
    {
        _config = config;
        _scanner = scanner;
        _onResults = onResults;
        _onHide = onHide;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    /// <summary>Сбросить кэш сканов — следующее открытие пересканирует области.</summary>
    public void ForceRescan()
    {
        foreach (var s in _states.Values)
        {
            s.Scanned = false;
            s.ScannedSignature = long.MinValue;
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var overlayVisible = false;

        while (!ct.IsCancellationRequested)
        {
            // Заморозка: не трогаем экран и оверлей — он остаётся как был (режим скриншота).
            if (Frozen)
            {
                try
                {
                    await Task.Delay(IdleCycleMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            var anyOpen = false;
            try
            {
                if (Paused)
                {
                    if (overlayVisible)
                    {
                        _onHide();
                        overlayVisible = false;
                    }
                }
                else
                {
                    var regions = _config.PylonRegions.ToList();
                    PruneStates(regions);

                    var openRegions = new List<(string Id, Rectangle Region)>();
                    var needScan = false;

                    foreach (var profile in regions)
                    {
                        var rect = new Rectangle(profile.X, profile.Y, profile.Width, profile.Height);
                        if (rect.Width < 4 || rect.Height < 4)
                        {
                            continue;
                        }

                        var st = State(profile.Name);
                        int brightness;
                        long signature;
                        using (var bmp = ScreenCapturer.Capture(rect))
                        {
                            (brightness, signature) = _detector.Sample(bmp);
                        }

                        st.LastSignature = signature;
                        ApplyHysteresis(st, brightness);

                        if (!st.IsOpen)
                        {
                            st.Scanned = false;
                            continue;
                        }

                        anyOpen = true;
                        openRegions.Add((profile.Name, rect));

                        if (!st.Scanned || Math.Abs(signature - st.ScannedSignature) > SignatureEpsilon)
                        {
                            needScan = true;
                        }
                    }

                    if (openRegions.Count == 0)
                    {
                        if (overlayVisible)
                        {
                            _onHide();
                            overlayVisible = false;
                        }
                    }
                    else if (needScan && (DateTime.UtcNow - _lastOcrUtc).TotalMilliseconds >= MinOcrIntervalMs)
                    {
                        _lastOcrUtc = DateTime.UtcNow;
                        var results = await _scanner.ScanAllAsync(openRegions, ct).ConfigureAwait(false);
                        foreach (var (id, _) in openRegions)
                        {
                            var st = State(id);
                            // Если цены не загрузились (фетч poe.ninja не прошёл) — НЕ помечаем
                            // как отсканированную, чтобы следующий тик пересканировал и
                            // повторил попытку (PriceCache сам троттлит фетч до раза в 30с).
                            // Иначе пустой результат «замёрз» бы до смены содержимого панели.
                            var pricesOk = results.FirstOrDefault(r => r.PylonId == id)?.PricesLoaded ?? false;
                            st.Scanned = pricesOk;
                            if (pricesOk)
                            {
                                st.ScannedSignature = st.LastSignature;
                            }
                        }

                        // Confirm-gate: показываем область, если есть надёжный признак, что
                        // это реальная панель наград:
                        //   • ХОТЯ БЫ ОДНА награда с ценой (совпала с живым прайсом poe.ninja —
                        //     после ужесточения матчинга это сильный сигнал, шум так не матчится), ИЛИ
                        //   • ≥ ConfirmRewards правдоподобных наград (все строки в Rewards уже
                        //     прошли priced/тег/LooksLikeName, мусор иконок/мира отсеян).
                        // Прежний строгий «≥2 с ценой» прятал панель целиком, если одна строка
                        // не прочиталась (баг A): напр. 2 валюты/руны, где OCR потерял одну.
                        var confirmed = results
                            .Where(r => r.Rewards.Any(x => x.Price is not null)
                                     || r.Rewards.Count >= ConfirmRewards)
                            .ToList();

                        if (confirmed.Count > 0)
                        {
                            _onResults(confirmed);
                            overlayVisible = true;
                        }
                        else if (overlayVisible)
                        {
                            _onHide();
                            overlayVisible = false;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Транзиентная ошибка кадра/захвата — продолжаем со следующего тика,
                // но оставляем след для диагностики (виден в отладчике/DebugView).
                System.Diagnostics.Debug.WriteLine($"[RegionMonitor] tick error: {ex}");
            }

            try
            {
                await Task.Delay(anyOpen ? OpenCycleMs : IdleCycleMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _onHide();
    }

    private static void ApplyHysteresis(RegionState st, int brightness)
    {
        if (brightness > OpenBrightness)
        {
            st.BrightStreak++;
            st.DarkStreak = 0;
        }
        else if (brightness < CloseBrightness)
        {
            st.DarkStreak++;
            st.BrightStreak = 0;
        }
        else
        {
            st.BrightStreak = 0;
            st.DarkStreak = 0;
        }

        if (!st.IsOpen && st.BrightStreak >= OpenStreak)
        {
            st.IsOpen = true;
        }
        else if (st.IsOpen && st.DarkStreak >= CloseStreak)
        {
            st.IsOpen = false;
        }
    }

    private void PruneStates(IEnumerable<CalibrationProfile> regions)
    {
        var names = regions.Select(r => r.Name).ToHashSet();
        foreach (var key in _states.Keys.Where(k => !names.Contains(k)).ToList())
        {
            _states.Remove(key);
        }
    }

    private RegionState State(string name)
    {
        if (!_states.TryGetValue(name, out var state))
        {
            state = new RegionState();
            _states[name] = state;
        }

        return state;
    }

    public void Dispose()
    {
        Stop();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }

        _cts?.Dispose();
    }
}
