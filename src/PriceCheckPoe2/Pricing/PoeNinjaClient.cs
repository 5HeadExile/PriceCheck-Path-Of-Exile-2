using System.Net.Http;
using Newtonsoft.Json.Linq;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Клиент poe.ninja для экономики PoE2 (currency exchange). Схема ответа
/// (проверена на лиге Runes of Aldur):
/// <code>
/// {
///   "core":  { "rates": { "exalted": 195.5, "chaos": 8.86 }, "primary": "divine" },
///   "lines": [ { "id": "exalted", "primaryValue": 0.005114, ... } ],
///   "items": [ { "id": "exalted", "name": "Exalted Orb", ... } ]
/// }
/// </code>
/// <c>primaryValue</c> выражен в основной валюте (<c>core.primary</c>, обычно
/// divine). Переводим в exalted: <c>primaryValue * core.rates.exalted</c>.
/// Имя предмета берём из <c>items</c> по <c>id</c>.
/// </summary>
public sealed class PoeNinjaClient : IPriceSource, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly IReadOnlyList<string> _overviews;

    public PoeNinjaClient(AppConfig config, HttpClient? http = null)
    {
        _baseUrl = config.PriceApiBaseUrl;
        _overviews = config.PriceOverviews;
        var owned = http is null;
        _http = http ?? new HttpClient();
        if (owned)
        {
            // Свой клиент — ограничиваем таймаут (дефолтные 100 с подвесили бы фоновый
            // цикл/удерживали бы кэш-семафор). Внешний клиент настраивает вызывающий.
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PriceCheckPoe2/0.1 (+https://github.com/5HeadExile)");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }
    }

    public async Task<IReadOnlyDictionary<string, RewardPrice>> FetchAsync(
        string league, CancellationToken cancellationToken = default)
    {
        var prices = new Dictionary<string, RewardPrice>(StringComparer.OrdinalIgnoreCase);

        foreach (var overview in _overviews)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var uri = BuildRequestUri(league, overview);
                var json = await _http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
                MergeOverview(json, prices);
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException or UriFormatException
                && !cancellationToken.IsCancellationRequested)
            {
                // Категория недоступна/таймаут/кривой URL — остальные важнее, не валим
                // весь фетч. Реальная отмена (наш токен) пробрасывается выше.
            }
        }

        return prices;
    }

    private Uri BuildRequestUri(string league, string overview)
    {
        var query = $"?league={Uri.EscapeDataString(league)}&type={Uri.EscapeDataString(overview)}";
        return new Uri(_baseUrl + query);
    }

    /// <summary>Разбирает ответ poe.ninja PoE2 и добавляет цены в словарь.</summary>
    private static void MergeOverview(string json, IDictionary<string, RewardPrice> into)
    {
        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch
        {
            return;
        }

        var lines = root["lines"] as JArray;
        var items = root["items"] as JArray;
        if (lines is null || items is null)
        {
            return;
        }

        // id → отображаемое имя.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var id = (string?)item["id"];
            var name = (string?)item["name"];
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                names[id] = name.Trim();
            }
        }

        var exaltedPerPrimary = ExaltedFactor(root["core"]);

        foreach (var line in lines)
        {
            var id = (string?)line["id"];
            if (string.IsNullOrWhiteSpace(id) || !names.TryGetValue(id, out var name))
            {
                continue;
            }

            var primary = (double?)line["primaryValue"] ?? 0.0;
            var exalted = primary * exaltedPerPrimary;
            into[name] = new RewardPrice(name, exalted, primary);
        }
    }

    /// <summary>
    /// Сколько exalted в одной единице основной валюты. Если основная валюта уже
    /// exalted — множитель 1; иначе берём <c>core.rates.exalted</c>.
    /// </summary>
    private static double ExaltedFactor(JToken? core)
    {
        if (core is null)
        {
            return 1.0;
        }

        var primary = (string?)core["primary"];
        if (string.Equals(primary, "exalted", StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        return (double?)core["rates"]?["exalted"] ?? 1.0;
    }

    public void Dispose() => _http.Dispose();
}
