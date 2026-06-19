namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>Фильтр по стату для запроса trade2 (trade id + диапазон значений).</summary>
public sealed record TradeFilter(string TradeId, double? Min = null, double? Max = null);

/// <summary>Результат POST /search: id запроса и хэши листингов.</summary>
public sealed record TradeSearchResult(string Id, IReadOnlyList<string> Hashes, int Total);

/// <summary>Один листинг с трейда (для отображения).</summary>
public sealed record TradeListing(string Account, double Amount, string Currency, string? Whisper);
