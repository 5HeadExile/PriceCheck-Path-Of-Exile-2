using System.Text;
using Newtonsoft.Json.Linq;

namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>
/// Клиент официального trade2 API PoE2: POST /search → GET /fetch (батч ≤10).
/// Гостевой режим (без логина) + <see cref="RateLimiter"/>. Парсинг ответов
/// вынесен в статические методы для юнит-тестов без сети.
/// </summary>
public sealed class TradeClient
{
    private const string Realm = "poe2";
    private const string SearchBase = "https://www.pathofexile.com/api/trade2/search/poe2/";
    private const string FetchBase = "https://www.pathofexile.com/api/trade2/fetch/";

    private readonly HttpClient _http;
    private readonly RateLimiter _limiter;
    private readonly string _league;

    public TradeClient(string league, HttpClient? http = null, RateLimiter? limiter = null)
    {
        _league = league;
        _limiter = limiter ?? new RateLimiter();
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            // Реалистичный UA — без него trade API часто отвечает Cloudflare-блоком.
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceCheckPoe2/0.1");
        }
    }

    public async Task<TradeSearchResult> SearchAsync(JObject body, CancellationToken ct = default)
    {
        var url = SearchBase + Uri.EscapeDataString(_league);
        var json = body.ToString(Newtonsoft.Json.Formatting.None);

        using var response = await _limiter.SendAsync(_http, () => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseSearch(text);
    }

    public async Task<IReadOnlyList<TradeListing>> FetchAsync(
        string queryId, IReadOnlyList<string> hashes, CancellationToken ct = default)
    {
        if (hashes.Count == 0)
        {
            return Array.Empty<TradeListing>();
        }

        var batch = string.Join(",", hashes.Take(10));
        var url = $"{FetchBase}{batch}?query={Uri.EscapeDataString(queryId)}&realm={Realm}";

        using var response = await _limiter.SendAsync(_http,
            () => new HttpRequestMessage(HttpMethod.Get, url), ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseFetch(text);
    }

    /// <summary>Поиск + получение первых <paramref name="max"/> листингов.</summary>
    public async Task<IReadOnlyList<TradeListing>> SearchAndFetchAsync(
        JObject body, int max = 10, CancellationToken ct = default)
    {
        var search = await SearchAsync(body, ct).ConfigureAwait(false);
        return await FetchAsync(search.Id, search.Hashes.Take(max).ToList(), ct).ConfigureAwait(false);
    }

    internal static TradeSearchResult ParseSearch(string json)
    {
        var o = JObject.Parse(json);
        var id = (string?)o["id"] ?? string.Empty;
        var hashes = (o["result"] as JArray)?.Select(x => (string?)x ?? string.Empty)
            .Where(s => s.Length > 0).ToList() ?? new List<string>();
        var total = (int?)o["total"] ?? hashes.Count;
        return new TradeSearchResult(id, hashes, total);
    }

    internal static IReadOnlyList<TradeListing> ParseFetch(string json)
    {
        var o = JObject.Parse(json);
        var listings = new List<TradeListing>();
        foreach (var r in (o["result"] as JArray) ?? new JArray())
        {
            if (r?["listing"] is not JObject listing)
            {
                continue;
            }

            var price = listing["price"];
            var amount = (double?)price?["amount"] ?? 0;
            var currency = (string?)price?["currency"] ?? string.Empty;
            var accountObj = listing["account"];
            var account = (string?)accountObj?["name"] ?? string.Empty;
            var whisper = (string?)listing["whisper"];
            var indexed = ParseIndexed((string?)listing["indexed"]);
            var status = ParseStatus(accountObj?["online"]);
            var ign = (string?)accountObj?["lastCharacterName"];
            listings.Add(new TradeListing(account, amount, currency, whisper, indexed, status, ign));
        }

        return listings;
    }

    private static DateTime? ParseIndexed(string? iso) =>
        DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var dt)
            ? dt
            : null;

    // account.online отсутствует → offline; есть и status=="afk" → afk; иначе online.
    private static SellerStatus ParseStatus(JToken? online)
    {
        if (online is not JObject)
        {
            return SellerStatus.Offline;
        }

        return (string?)online["status"] == "afk" ? SellerStatus.Afk : SellerStatus.Online;
    }

    /// <summary>
    /// Сводка по цене: доминирующая валюта среди листингов + её минимум и медиана.
    /// Листинги уже отсортированы по цене (sort.price=asc), поэтому медиана честная.
    /// </summary>
    public static TradePriceSummary? Summarize(IReadOnlyList<TradeListing> listings)
    {
        var priced = listings.Where(l => l.Amount > 0 && !string.IsNullOrEmpty(l.Currency)).ToList();
        if (priced.Count == 0)
        {
            return null;
        }

        // Доминирующая валюта — самая частая (при равенстве берём более дешёвую группу).
        var currency = priced
            .GroupBy(l => l.Currency)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(l => l.Amount))
            .First().Key;

        var amounts = priced.Where(l => l.Currency == currency).Select(l => l.Amount).OrderBy(a => a).ToList();
        return new TradePriceSummary(currency, amounts[0], Median(amounts), amounts.Count);
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
    }
}
