namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Источник цен. Абстракция позволяет заменить poe.ninja на офиц. Trade API PoE2
/// без изменения остального кода.
/// </summary>
public interface IPriceSource
{
    /// <summary>
    /// Возвращает снапшот цен по лиге: ключ — каноническое имя награды
    /// (lower-case), значение — цена.
    /// </summary>
    Task<IReadOnlyDictionary<string, RewardPrice>> FetchAsync(
        string league, CancellationToken cancellationToken = default);
}
