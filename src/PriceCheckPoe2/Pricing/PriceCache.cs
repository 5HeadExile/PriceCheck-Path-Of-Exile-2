namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Кэш цен поверх <see cref="IPriceSource"/>. Обновляет снапшот не чаще, чем
/// раз в заданный интервал (по умолчанию 30 минут).
/// </summary>
public sealed class PriceCache
{
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
                _prices = await _source.FetchAsync(league, cancellationToken).ConfigureAwait(false);
                _lastRefreshUtc = DateTime.UtcNow;
            }
        }
        finally
        {
            _gate.Release();
        }

        return _prices;
    }
}
