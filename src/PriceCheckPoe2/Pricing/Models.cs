namespace PriceCheckPoe2.Pricing;

/// <summary>Цена одной награды в общепринятых валютах PoE2.</summary>
public sealed record RewardPrice(string Name, double ExaltedValue, double? DivineValue);

/// <summary>Распознанная награда: каноническое имя и количество в стаке.</summary>
public sealed record Reward(string Name, int Stack)
{
    public int Stack { get; init; } = Stack < 1 ? 1 : Stack;
}

/// <summary>Один пилон — набор наград, между которыми выбирает игрок.</summary>
public sealed record Pylon(string Id, IReadOnlyList<Reward> Rewards);

/// <summary>Результат оценки пилона: суммарная ценность и разбивка по наградам.</summary>
public sealed record PylonValuation(
    string PylonId,
    double TotalExalted,
    IReadOnlyList<(Reward Reward, RewardPrice? Price, double LineTotal)> Lines);
