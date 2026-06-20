namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>Фильтр по стату для запроса trade2 (trade id + диапазон значений).</summary>
public sealed record TradeFilter(string TradeId, double? Min = null, double? Max = null);

/// <summary>Результат POST /search: id запроса и хэши листингов.</summary>
public sealed record TradeSearchResult(string Id, IReadOnlyList<string> Hashes, int Total);

/// <summary>Статус продавца в листинге (из account.online).</summary>
public enum SellerStatus
{
    Offline,
    Online,
    Afk,
}

/// <summary>Один листинг с трейда (для отображения).</summary>
public sealed record TradeListing(
    string Account,
    double Amount,
    string Currency,
    string? Whisper,
    DateTime? Indexed = null,
    SellerStatus Status = SellerStatus.Offline,
    string? Ign = null);

/// <summary>
/// Сводка по цене (порт идеи EE2 results panel): берём доминирующую валюту среди
/// дешёвых листингов и считаем минимум и медиану в ней. Кросс-валютную конвертацию
/// не делаем — это требует курсов; показываем по преобладающей валюте.
/// </summary>
public sealed record TradePriceSummary(string Currency, double Min, double Median, int Count);
