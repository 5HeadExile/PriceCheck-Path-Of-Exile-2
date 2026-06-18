using Newtonsoft.Json.Linq;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Клиент poe.ninja для экономики PoE2. URL и категории берутся из
/// <see cref="AppConfig"/>, т.к. путь PoE2 не задокументирован и может меняться
/// между лигами (проверять через DevTools браузера на poe.ninja/poe2/economy).
/// Парсинг устойчив к нескольким формам ответа (overview-стиль PoE1 и
/// currency-exchange PoE2).
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
        _http = http ?? new HttpClient();
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

            var uri = BuildRequestUri(league, overview);
            try
            {
                var json = await _http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
                MergeOverview(json, prices);
            }
            catch (HttpRequestException)
            {
                // Категория недоступна/пустая — остальные важнее, не валим всё.
            }
        }

        return prices;
    }

    private Uri BuildRequestUri(string league, string overview)
    {
        var query = $"?leagueName={Uri.EscapeDataString(league)}&overviewName={Uri.EscapeDataString(overview)}";
        return new Uri(_baseUrl + query);
    }

    /// <summary>
    /// Достаёт пары имя→цена из ответа. Терпим к расположению массива
    /// (<c>lines</c>, <c>items</c>, <c>entries</c> или корневой массив) и к
    /// набору полей цены (<c>exaltedValue</c>/<c>chaosValue</c>/<c>value</c>).
    /// </summary>
    private static void MergeOverview(string json, IDictionary<string, RewardPrice> into)
    {
        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch
        {
            return;
        }

        var lines = FirstArray(root, "lines", "items", "entries") ?? root as JArray;
        if (lines is null)
        {
            return;
        }

        foreach (var line in lines)
        {
            var name = FirstString(line, "name", "currencyTypeName", "itemName", "text");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var exalted = FirstDouble(line, "exaltedValue", "chaosValue", "value", "receive");
            var divine = (double?)line["divineValue"];
            into[name.Trim()] = new RewardPrice(name.Trim(), exalted ?? 0.0, divine);
        }
    }

    private static JArray? FirstArray(JToken root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root[key] is JArray arr)
            {
                return arr;
            }
        }

        return null;
    }

    private static string? FirstString(JToken token, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = (string?)token[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static double? FirstDouble(JToken token, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (token[key] is { } t && t.Type is JTokenType.Float or JTokenType.Integer)
            {
                return (double)t;
            }
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}
