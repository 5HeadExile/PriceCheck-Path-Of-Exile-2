namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>Один стат-фильтр запроса (trade id + диапазон/опция).</summary>
public sealed record StatQuery(string Id, double? Min = null, double? Max = null, string? Option = null);

/// <summary>
/// Контекст для построения trade2-запроса — портирован из логики EE2
/// (createTradeRequest, MIT, см. ItemCheck/Data/ee2/ATTRIBUTION.md). Описывает
/// все поддерживаемые группы фильтров; неуказанные (null) в тело не попадают.
/// </summary>
public sealed class TradeQueryContext
{
    public string ListingType { get; init; } = "online";

    public string? Name { get; init; }
    public string? Type { get; init; }

    // type_filters
    public string? CategoryId { get; init; }
    public string? RarityOption { get; init; }
    public int? ItemLevelMin { get; init; }
    public int? ItemLevelMax { get; init; }
    public int? QualityMin { get; init; }

    // req_filters
    public int? RequireLevelMax { get; init; }

    // misc_filters
    public int? GemLevelMin { get; init; }
    public int? GemLevelMax { get; init; }
    public int? SocketsMin { get; init; }
    public bool? Corrupted { get; init; }
    public bool? Mirrored { get; init; }
    public bool? Identified { get; init; }

    public IReadOnlyList<StatQuery> Stats { get; init; } = Array.Empty<StatQuery>();
}
