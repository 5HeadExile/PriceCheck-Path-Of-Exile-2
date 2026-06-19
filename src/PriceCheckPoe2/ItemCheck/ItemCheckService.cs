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
/// </summary>
public sealed class ItemCheckService
{
    private readonly AppConfig _config;
    private readonly ClipboardItemReader _reader = new();
    private readonly Lazy<StatDatabase?> _stats;
    private readonly HttpClient _http = new();
    private readonly TradeClient _trade;
    private readonly PriceCache _ninja;

    public ItemCheckService(AppConfig config)
    {
        _config = config;
        _stats = new Lazy<StatDatabase?>(LoadStats);
        _trade = new TradeClient(config.League, _http);
        _ninja = new PriceCache(new PoeNinjaClient(config), TimeSpan.FromMinutes(config.PriceRefreshMinutes));
    }

    private static StatDatabase? LoadStats()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ItemCheck", "Data", "ee2", "stats.ndjson");
        return File.Exists(path) ? StatDatabase.Load(path) : null;
    }

    /// <summary>Вызывать в UI/STA-потоке (буфер обмена требует STA).</summary>
    public void Trigger()
    {
        string? text;
        try
        {
            text = _reader.CopyAndRead();
        }
        catch
        {
            text = null;
        }

        if (string.IsNullOrWhiteSpace(text) || !ItemTextParser.TryParse(text, out var item))
        {
            return;
        }

        var stats = _stats.Value?.Match(item) ?? new ItemStats();
        ShowWindow(item, stats);
    }

    private void ShowWindow(ParsedItem item, ItemStats stats)
    {
        var thread = new Thread(() =>
        {
            var window = new ItemCheckWindow(item, stats);
            window.SearchRequested += filters => _ = SearchAsync(window, filters);
            window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
            window.Show();
            _ = EstimateNinjaAsync(item, window);
            System.Windows.Threading.Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private async Task SearchAsync(ItemCheckWindow window, IReadOnlyList<TradeFilter> filters)
    {
        try
        {
            var body = TradeQueryBuilder.Build(filters);
            var listings = await _trade.SearchAndFetchAsync(body).ConfigureAwait(false);
            window.ShowListings(listings);
            window.SetStatus("Готово");
        }
        catch (Exception ex)
        {
            window.SetStatus("Ошибка трейда: " + ex.Message);
        }
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
}
