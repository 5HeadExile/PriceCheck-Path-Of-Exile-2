using PriceCheckPoe2.ItemCheck.Parsing;

namespace PriceCheckPoe2.ItemCheck.Data;

/// <summary>
/// Вычисляет псевдо-моды (суммарные показатели) из сопоставленных модов — как в
/// Awakened PoE Trade: суммарные сопротивления, жизнь, мана, атрибуты. Это
/// улучшает поиск (искать по «total elemental resistance», а не по трём строкам).
/// Trade-id псевдостатов помечаем; по умолчанию в запрос они не добавляются
/// (выбор за пользователем), чтобы неточный id не ломал поиск.
/// </summary>
public static class PseudoRules
{
    public static IReadOnlyList<MatchedStat> Compute(IEnumerable<MatchedStat> mods)
    {
        double fire = 0, cold = 0, light = 0, chaos = 0, life = 0, mana = 0;
        double str = 0, dex = 0, intel = 0, allAttr = 0;

        foreach (var m in mods)
        {
            var v = m.Value ?? 0;
            if (v == 0)
            {
                continue;
            }

            var t = m.Text.ToLowerInvariant();

            if (t.Contains("resistance"))
            {
                if (t.Contains("all elemental"))
                {
                    fire += v; cold += v; light += v;
                }
                else
                {
                    if (t.Contains("fire")) fire += v;
                    if (t.Contains("cold")) cold += v;
                    if (t.Contains("lightning")) light += v;
                    if (t.Contains("chaos")) chaos += v;
                }

                continue;
            }

            if (t.Contains("per") || t.Contains("while") || t.Contains("if you"))
            {
                continue; // условные/масштабируемые моды в псевдо не учитываем
            }

            if (t.Contains("to maximum life")) life += v;
            else if (t.Contains("to maximum mana")) mana += v;
            else if (t.Contains("to all attributes")) allAttr += v;
            else if (t.Contains("to strength")) str += v;
            else if (t.Contains("to dexterity")) dex += v;
            else if (t.Contains("to intelligence")) intel += v;
        }

        var result = new List<MatchedStat>();
        var ele = fire + cold + light;
        if (ele > 0)
        {
            result.Add(Pseudo("pseudo.pseudo_total_elemental_resistance", "+#% total Elemental Resistance", ele));
        }

        if (ele + chaos > 0 && chaos > 0)
        {
            result.Add(Pseudo("pseudo.pseudo_total_resistance", "+#% total Resistance", ele + chaos));
        }

        if (life > 0)
        {
            result.Add(Pseudo("pseudo.pseudo_total_life", "+# total maximum Life", life));
        }

        if (mana > 0)
        {
            result.Add(Pseudo("pseudo.pseudo_total_mana", "+# total maximum Mana", mana));
        }

        var totalAttr = str + dex + intel + (allAttr * 3);
        if (totalAttr > 0)
        {
            result.Add(Pseudo("pseudo.pseudo_total_attributes", "+# total Attributes", totalAttr));
        }

        return result;
    }

    private static MatchedStat Pseudo(string tradeId, string text, double value) =>
        new(tradeId, tradeId, ModKind.Explicit, text.Replace("#", value.ToString("0.##")),
            value, new[] { value }, IsPseudo: true);
}
