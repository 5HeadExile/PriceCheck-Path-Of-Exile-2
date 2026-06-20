using System.Threading;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.ItemCheck.Data;
using PriceCheckPoe2.ItemCheck.Input;
using PriceCheckPoe2.ItemCheck.Parsing;
using PriceCheckPoe2.ItemCheck.Trade;
using PriceCheckPoe2.ItemCheck.UI;
using PriceCheckPoe2.Pricing;

namespace PriceCheckPoe2.ItemCheck;

/// <summary>
/// Оркестратор price-check: по хоткею копирует предмет, парсит, сопоставляет
/// статы (EE2), показывает WPF-окно и по запросу ищет листинги в trade2. Также
/// даёт быструю оценку через poe.ninja (I5). Полностью отделён от пилонов.
/// Пишет диагностику в <c>itemcheck_log.txt</c> рядом с exe.
/// </summary>
public sealed class ItemCheckService
{
    private readonly AppConfig _config;
    private readonly Action<string>? _notify;
    private readonly ClipboardItemReader _reader = new();
    private readonly Lazy<StatDatabase?> _stats;
    private readonly HttpClient _http = new();
    private readonly TradeClient _trade;
    private readonly PriceCache _ninja;
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "itemcheck_log.txt");

    public ItemCheckService(AppConfig config, Action<string>? notify = null)
    {
        _config = config;
        _notify = notify;
        _stats = new Lazy<StatDatabase?>(LoadStats);
        _trade = new TradeClient(config.League, _http);
        _ninja = new PriceCache(new PoeNinjaClient(config), TimeSpan.FromMinutes(config.PriceRefreshMinutes));
    }

    private StatDatabase? LoadStats()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ItemCheck", "Data", "ee2", "stats.ndjson");
        if (!File.Exists(path))
        {
            Log($"stats.ndjson НЕ найден: {path}");
            return null;
        }

        var db = StatDatabase.Load(path);
        Log("stats.ndjson загружен");
        return db;
    }

    /// <summary>Вызывать в UI/STA-потоке (буфер обмена требует STA).</summary>
    public void Trigger()
    {
        try
        {
            Log("hotkey: triggered");
            var sendCopy = _config.ItemCheckSendCopy;
            // Пассивный режим (хоткей = Ctrl+C): игра копирует сама, инъекцию не шлём
            // и не спамим подсказками при обычном копировании вне игры.
            var passive = !sendCopy;

            string? text;
            try
            {
                text = _reader.CopyAndRead(sendCopy);
            }
            catch (Exception ex)
            {
                Log("clipboard error: " + ex.Message);
                text = null;
            }

            Log($"clipboard length = {text?.Length ?? -1} (sendCopy={sendCopy})");
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!passive)
                {
                    _notify?.Invoke("Буфер пуст — наведи курсор на предмет в игре.");
                }

                return;
            }

            if (!ItemTextParser.TryParse(text, out var item))
            {
                Log("parse failed; head: " + text[..Math.Min(60, text.Length)].Replace('\n', ' '));
                if (!passive)
                {
                    _notify?.Invoke("Не распознан как предмет PoE2 (английский клиент?).");
                }

                return;
            }

            var stats = _stats.Value?.Match(item) ?? new ItemStats();
            Log($"parsed: name='{item.Name}' base='{item.BaseType}' rarity={item.Rarity} " +
                $"impl={item.Implicits.Count} expl={item.Explicits.Count} rune={item.Runes.Count} ench={item.Enchants.Count} " +
                $"matched_mods={stats.Mods.Count} pseudo={stats.Pseudo.Count}");
            foreach (var m in stats.Mods)
            {
                Log($"  mod[{m.Kind}] '{m.Text}' -> {(m.TradeId ?? "NO MATCH")}");
            }

            ShowWindow(item, stats);
        }
        catch (Exception ex)
        {
            Log("Trigger error: " + ex);
            _notify?.Invoke("Ошибка price-check: " + ex.Message);
        }
    }

    private void ShowWindow(ParsedItem item, ItemStats stats)
    {
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ItemCheckWindow(item, stats);
                window.SearchRequested += filters => _ = SearchAsync(window, item, filters);
                window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                window.Show();
                window.Activate();
                _ = EstimateNinjaAsync(item, window);
                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception ex)
            {
                Log("window error: " + ex);
                _notify?.Invoke("Не удалось открыть окно: " + ex.Message);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private async Task SearchAsync(ItemCheckWindow window, ParsedItem item, IReadOnlyList<TradeFilter> filters)
    {
        try
        {
            var body = TradeQueryBuilder.Build(BuildContext(item, filters));
            var search = await _trade.SearchAsync(body).ConfigureAwait(false);

            var url = $"https://www.pathofexile.com/trade2/search/poe2/{Uri.EscapeDataString(_config.League)}/{search.Id}";
            window.SetTradeUrl(url);

            var listings = await _trade.FetchAsync(search.Id, search.Hashes.Take(10).ToList()).ConfigureAwait(false);
            window.ShowListings(listings);
            window.SetStatus($"Найдено: {search.Total}");
            Log($"trade ok: total={search.Total} shown={listings.Count} filters={filters.Count} id={search.Id}");
        }
        catch (Exception ex)
        {
            window.SetStatus("Ошибка трейда: " + ex.Message);
            Log("trade error: " + ex.Message);
        }
    }

    /// <summary>
    /// Строит контекст trade2-запроса под предмет (логика EE2): уникал — по имени+базе;
    /// снаряжение — по категории слота + статам; валюта/гемы — по имени-типу. Для
    /// не-уникального снаряжения исключаем corrupted/mirrored (если предмет сам не
    /// корраптнут).
    /// </summary>
    private static TradeQueryContext BuildContext(ParsedItem item, IReadOnlyList<TradeFilter> filters)
    {
        var stats = filters.Select(f => new StatQuery(f.TradeId, f.Min, f.Max)).ToList();
        var nonUnique = item.Rarity is ItemRarity.Normal or ItemRarity.Magic or ItemRarity.Rare;

        string? name = null, type = null, category = null;
        if (item.Rarity == ItemRarity.Unique)
        {
            name = item.Name;
            type = item.BaseType;
        }
        else if (item.IsGear)
        {
            category = TradeCategories.ForClass(item.ItemClass);
            if (category is null)
            {
                type = item.BaseType; // класс не в карте — ищем по базе
            }
        }
        else
        {
            type = item.Name ?? item.BaseType; // валюта/гемы/омены
        }

        return new TradeQueryContext
        {
            Name = name,
            Type = type,
            CategoryId = category,
            Corrupted = item.Corrupted ? true : nonUnique ? false : (bool?)null,
            Mirrored = nonUnique ? false : null,
            Stats = stats,
        };
    }

    private async Task EstimateNinjaAsync(ParsedItem item, ItemCheckWindow window)
    {
        try
        {
            if (string.IsNullOrEmpty(item.Name))
            {
                return;
            }

            var prices = await _ninja.GetAsync(_config.League).ConfigureAwait(false);
            if (prices.TryGetValue(item.Name, out var p))
            {
                window.SetNinjaEstimate($"poe.ninja: ~{p.ExaltedValue:0.##} ex");
            }
        }
        catch
        {
            // оценка poe.ninja необязательна
        }
    }

    private void Log(string message)
    {
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // лог не критичен
        }
    }
}
