using PriceCheckPoe2.ItemCheck.Parsing;

namespace PriceCheckPoe2.ItemCheck.Data;

/// <summary>Сопоставленный мод: stat id EE2, trade id (по виду аффикса) и значение.</summary>
public sealed record MatchedStat(
    string StatId,
    string? TradeId,
    ModKind Kind,
    string Text,
    double? Value,
    IReadOnlyList<double> Values,
    bool IsPseudo = false,
    int Better = StatFilterMath.PositiveRoll,
    bool Dp = false,
    bool Inverted = false);

/// <summary>Результат сопоставления предмета: реальные моды + вычисленные псевдо-моды.</summary>
public sealed class ItemStats
{
    public IReadOnlyList<MatchedStat> Mods { get; init; } = Array.Empty<MatchedStat>();
    public IReadOnlyList<MatchedStat> Pseudo { get; init; } = Array.Empty<MatchedStat>();
}
