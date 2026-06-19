using System.Net;

namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>Лимит вида «hits:period:restrict» из заголовков X-Rate-Limit-*.</summary>
public sealed record RateRule(int Hits, int Period, int Restrict);

/// <summary>
/// Сериализует запросы к trade API и держится в рамках лимитов: читает политику
/// из заголовков <c>X-Rate-Limit-*</c>, выдерживает паузу перед запросом и делает
/// backoff по <c>Retry-After</c> при 429. Без агрессивного долбления — иначе
/// временный бан.
/// </summary>
public sealed class RateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DateTime> _history = new();
    private readonly TimeSpan _minInterval;
    private List<RateRule> _rules = new();

    public RateLimiter(TimeSpan? minInterval = null)
    {
        _minInterval = minInterval ?? TimeSpan.FromMilliseconds(400);
    }

    /// <summary>
    /// Отправляет запрос с соблюдением лимитов. <paramref name="makeRequest"/>
    /// создаёт новый <see cref="HttpRequestMessage"/> при каждом вызове (для ретрая).
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpClient http, Func<HttpRequestMessage> makeRequest, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DelayBeforeAsync(ct).ConfigureAwait(false);
            var response = await http.SendAsync(makeRequest(), ct).ConfigureAwait(false);
            Record(response);

            if (response.StatusCode == (HttpStatusCode)429)
            {
                var retry = RetryAfter(response) ?? TimeSpan.FromSeconds(10);
                response.Dispose();
                await Task.Delay(retry, ct).ConfigureAwait(false);
                await DelayBeforeAsync(ct).ConfigureAwait(false);
                response = await http.SendAsync(makeRequest(), ct).ConfigureAwait(false);
                Record(response);
            }

            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DelayBeforeAsync(CancellationToken ct)
    {
        var delay = ComputeDelay(_history, DateTime.UtcNow, _rules, _minInterval);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private void Record(HttpResponseMessage response)
    {
        _history.Add(DateTime.UtcNow);
        if (_history.Count > 100)
        {
            _history.RemoveRange(0, _history.Count - 100);
        }

        // Обновляем политику из заголовка лимитов (наиболее строгий из доступных).
        foreach (var name in new[] { "X-Rate-Limit-Ip", "X-Rate-Limit-Account", "X-Rate-Limit-Client" })
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                var parsed = ParsePolicy(string.Join(",", values));
                if (parsed.Count > 0)
                {
                    _rules = parsed;
                    break;
                }
            }
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    /// <summary>Разбирает строку политики, напр. «8:10:60,15:60:300».</summary>
    public static List<RateRule> ParsePolicy(string headerValue)
    {
        var rules = new List<RateRule>();
        foreach (var part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var seg = part.Split(':');
            if (seg.Length >= 2 && int.TryParse(seg[0], out var hits) && int.TryParse(seg[1], out var period))
            {
                var restrict = seg.Length >= 3 && int.TryParse(seg[2], out var r) ? r : 0;
                rules.Add(new RateRule(hits, period, restrict));
            }
        }

        return rules;
    }

    /// <summary>
    /// Сколько подождать перед следующим запросом: минимальный интервал плюс
    /// соблюдение каждого правила (не превышать hits за period секунд).
    /// </summary>
    public static TimeSpan ComputeDelay(
        IReadOnlyList<DateTime> history, DateTime now, IReadOnlyList<RateRule> rules, TimeSpan minInterval)
    {
        var wait = TimeSpan.Zero;
        if (history.Count > 0)
        {
            var sinceLast = now - history[^1];
            if (sinceLast < minInterval)
            {
                wait = minInterval - sinceLast;
            }
        }

        foreach (var rule in rules)
        {
            if (rule.Hits <= 0 || rule.Period <= 0)
            {
                continue;
            }

            var windowStart = now - TimeSpan.FromSeconds(rule.Period);
            var inWindow = history.Where(t => t > windowStart).ToList();
            if (inWindow.Count >= rule.Hits)
            {
                var until = inWindow.Min() + TimeSpan.FromSeconds(rule.Period) - now;
                if (until > wait)
                {
                    wait = until;
                }
            }
        }

        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
    }
}
