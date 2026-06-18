using Newtonsoft.Json.Linq;

namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Клиент poe.ninja для экономики PoE2.
/// TODO(M4): подтвердить точный endpoint PoE2 (инспекцией запросов на
/// poe.ninja/poe2/economy) и заполнить <see cref="BuildRequestUri"/> и парсинг.
/// Структура намеренно похожа на реакцию референса: тянем снапшот категорий,
/// собираем словарь имя→цена.
/// </summary>
public sealed class PoeNinjaClient : IPriceSource, IDisposable
{
    private readonly HttpClient _http;

    // Категории экономики PoE2, которые встречаются в наградах пилонов.
    // Уточнить/дополнить под реальные типы наград (M3/M4).
    private static readonly string[] Categories =
    {
        "Currency", "Fragments", "Runes", "Essences", "Catalysts",
    };

    public PoeNinjaClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PriceCheckPoe2/0.1");
    }

    public async Task<IReadOnlyDictionary<string, RewardPrice>> FetchAsync(
        string league, CancellationToken cancellationToken = default)
    {
        var prices = new Dictionary<string, RewardPrice>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in Categories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uri = BuildRequestUri(league, category);
            try
            {
                var json = await _http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
                MergeCategory(json, prices);
            }
            catch (HttpRequestException)
            {
                // Категория недоступна/пустая — пропускаем, остальные важнее.
            }
        }

        return prices;
    }

    /// <summary>
    /// TODO(M4): заменить на реальный URL PoE2. Плейсхолдер показывает форму.
    /// </summary>
    private static Uri BuildRequestUri(string league, string category) =>
        new($"https://poe.ninja/api/data/poe2/economy?league={Uri.EscapeDataString(league)}&type={category}");

    /// <summary>
    /// Разбор ответа. Реальная схема будет уточнена (M4) — здесь устойчивый
    /// парсинг массива {name, exaltedValue, divineValue}.
    /// </summary>
    private static void MergeCategory(string json, IDictionary<string, RewardPrice> into)
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

        var lines = root["lines"] as JArray ?? root as JArray;
        if (lines is null)
        {
            return;
        }

        foreach (var line in lines)
        {
            var name = (string?)line["name"] ?? (string?)line["currencyTypeName"];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var exalted = (double?)line["exaltedValue"] ?? (double?)line["chaosValue"] ?? 0.0;
            var divine = (double?)line["divineValue"];
            into[name.Trim()] = new RewardPrice(name.Trim(), exalted, divine);
        }
    }

    public void Dispose() => _http.Dispose();
}
