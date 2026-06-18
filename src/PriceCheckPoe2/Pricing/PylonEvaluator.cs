namespace PriceCheckPoe2.Pricing;

/// <summary>
/// Считает суммарную ценность пилонов и подсказывает лучший выбор.
/// Это ключевое улучшение относительно референса, который оценивал отдельные
/// позиции, но не суммарную выгоду выбора.
/// </summary>
public sealed class PylonEvaluator
{
    private readonly IReadOnlyDictionary<string, RewardPrice> _prices;

    public PylonEvaluator(IReadOnlyDictionary<string, RewardPrice> prices) => _prices = prices;

    /// <summary>Оценивает один пилон: цена×стак по каждой награде и сумма.</summary>
    public PylonValuation Evaluate(Pylon pylon)
    {
        var lines = new List<(Reward, RewardPrice?, double)>(pylon.Rewards.Count);
        double total = 0;

        foreach (var reward in pylon.Rewards)
        {
            _prices.TryGetValue(reward.Name, out var price);
            var lineTotal = (price?.ExaltedValue ?? 0) * reward.Stack;
            total += lineTotal;
            lines.Add((reward, price, lineTotal));
        }

        return new PylonValuation(pylon.Id, total, lines);
    }

    /// <summary>
    /// Оценивает все пилоны и возвращает их по убыванию ценности —
    /// первый элемент и есть рекомендованный выбор.
    /// </summary>
    public IReadOnlyList<PylonValuation> Rank(IEnumerable<Pylon> pylons) =>
        pylons.Select(Evaluate)
              .OrderByDescending(v => v.TotalExalted)
              .ToList();
}
