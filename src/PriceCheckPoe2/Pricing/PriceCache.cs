namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Кэш цен поверх <see cref="IPriceSource"/>. Обновляет снапшот не чаще, чем
/// раз в заданный интервал (по умолчанию 30 минут).
/// </summary>
public sealed class PriceCache
{
    // Если фетч вернул пусто (сеть недоступна/все категории упали) — не «замораживаем»
    // пустой снапшот на весь TTL, а повторяем попытку через короткий интервал.
    private static readonly TimeSpan EmptyRetry = TimeSpan.FromSeconds(30);

    private readonly IPriceSource _source;
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyDictionary<string, RewardPrice> _prices =
        new Dictionary<string, RewardPrice>();
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public PriceCache(IPriceSource source, TimeSpan ttl)
    {
        _source = source;
        _ttl = ttl;
    }

    public bool IsStale => DateTime.UtcNow - _lastRefreshUtc > _ttl;

    public async Task<IReadOnlyDictionary<string, RewardPrice>> GetAsync(
        string league, CancellationToken cancellationToken = default)
    {
        if (!IsStale)
        {
            return _prices;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsStale)
            {
                var fetched = await _source.FetchAsync(league, cancellationToken).ConfigureAwait(false);
                if (fetched.Count > 0)
                {
                    _prices = fetched;
                    _lastRefreshUtc = DateTime.UtcNow;
                }
                else
                {
                    // Пусто: сохраняем прежние цены (если были) и пробуем снова через
                    // EmptyRetry, а не через полный TTL — иначе оффлайн на старте
                    // оставит цены пустыми на весь период обновления.
                    _lastRefreshUtc = DateTime.UtcNow - _ttl + EmptyRetry;
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        return _prices;
    }
}
