namespace PriceCheckPoe2.ItemCheck.Data;

/// <summary>
/// Порт математики поиска статов из EE2 (`filters/util.ts` + `create-stat-filters.ts`
/// + `pathofexile-trade.ts getMinMax`, MIT). Из значения ролла и направления
/// «лучше» строит границы поиска (min/max) под trade2 с учётом инверсии стата.
/// </summary>
public static class StatFilterMath
{
    /// <summary>Окно поиска по умолчанию в процентах от ролла (EE2 searchStatRange).</summary>
    public const int DefaultSearchPercent = 10;

    /// <summary>StatBetter: выше ролл — лучше (life, resist, damage).</summary>
    public const int PositiveRoll = 1;

    /// <summary>StatBetter: ниже ролл — лучше (requirements, reload time).</summary>
    public const int NegativeRoll = -1;

    /// <summary>StatBetter: ролл несравним (опции, аллокации) — не строим окно.</summary>
    public const int NotComparable = 0;

    /// <summary>Кол-во знаков после запятой как в EE2 `decimalPlaces`.</summary>
    private static int DecimalPlaces(double value, bool dp)
    {
        if (!dp || Math.Abs(value) >= 10)
        {
            return 0;
        }

        return Math.Abs(value) < 2.3 ? 2 : 1;
    }

    /// <summary>EE2 `percentRoll`: value + |value|*p/100, округление floor/ceil по dp.</summary>
    public static double PercentRoll(double value, double percent, bool ceil, bool dp)
    {
        var res = value + (Math.Abs(value) * percent / 100.0);
        var rounding = Math.Pow(10, DecimalPlaces(value, dp));
        var scaled = (res + double.Epsilon) * rounding;
        return (ceil ? Math.Ceiling(scaled) : Math.Floor(scaled)) / rounding;
    }

    /// <summary>
    /// Граница поиска под trade2 для одного стата. Возвращает (min, max) уже после
    /// EE2 `getMinMax` (инверсия) — то, что кладём в фильтр. Для несравнимых статов
    /// (<see cref="NotComparable"/>) окно не строится.
    /// </summary>
    public static (double? Min, double? Max) FilterBounds(
        double value, int better, bool dp, bool inverted, int percent = DefaultSearchPercent)
    {
        double? min = null, max = null;
        switch (better)
        {
            case PositiveRoll:
                min = PercentRoll(value, -percent, ceil: false, dp);
                break;
            case NegativeRoll:
                max = PercentRoll(value, +percent, ceil: true, dp);
                break;
            // NotComparable: окно не нужно — поиск по опции/значению вне диапазона.
        }

        return ApplyTradeInvert(min, max, inverted);
    }

    /// <summary>EE2 `getMinMax`: при инверсии меняем знак и местами min/max.</summary>
    private static (double? Min, double? Max) ApplyTradeInvert(double? min, double? max, bool inverted)
    {
        if (!inverted)
        {
            return (min, max);
        }

        double? a = min.HasValue ? -min.Value : null;
        double? b = max.HasValue ? -max.Value : null;
        return (b, a);
    }
}
