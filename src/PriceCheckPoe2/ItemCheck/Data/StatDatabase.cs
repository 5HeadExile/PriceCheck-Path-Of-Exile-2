using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PriceCheckPoe2.ItemCheck.Parsing;

namespace PriceCheckPoe2.ItemCheck.Data;

/// <summary>
/// База статов из данных EE2 (`stats.ndjson`). Сопоставляет строку мода предмета
/// с trade `stat id` и извлекает числовое значение.
/// <para>Подход: matcher-строки EE2 содержат `#` на месте переменных чисел и
/// литеральные константы (например «per 100»). Поэтому матчим регуляркой,
/// построенной из matcher-строки. Чтобы не строить тысячи регулярок на старте,
/// индексируем по «скелету» (только буквы) и компилируем регулярку лениво только
/// для строк-кандидатов конкретного мода.</para>
/// </summary>
public sealed class StatDatabase
{
    private sealed class MatcherInfo
    {
        public required string Pattern;   // готовый regex-паттерн с группами вместо #
        public int Specificity;           // длина литералов: более длинное = точнее
        public bool Negate;
        public double? FixedValue;        // для matcher с "value" (без #)
        public Regex? Compiled;           // лениво
    }

    private sealed class StatEntry
    {
        public required string Id;
        public required string Ref;
        public Dictionary<string, string> TradeByKind = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly Dictionary<string, List<(StatEntry Stat, MatcherInfo M)>> _bySkeleton = new();

    public static StatDatabase Load(string ndjsonPath) =>
        LoadFromLines(File.ReadLines(ndjsonPath));

    public static StatDatabase LoadFromLines(IEnumerable<string> lines)
    {
        var db = new StatDatabase();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JObject o;
            try
            {
                o = JObject.Parse(line);
            }
            catch
            {
                continue;
            }

            var id = (string?)o["id"];
            var refText = (string?)o["ref"];
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(refText))
            {
                continue;
            }

            var entry = new StatEntry { Id = id, Ref = refText };
            if (o["trade"]?["ids"] is JObject ids)
            {
                foreach (var prop in ids.Properties())
                {
                    if (prop.Value is JArray arr && arr.Count > 0 && (string?)arr[0] is { } first)
                    {
                        entry.TradeByKind[prop.Name] = first;
                    }
                }
            }

            if (o["matchers"] is not JArray matchers)
            {
                continue;
            }

            foreach (var m in matchers)
            {
                var s = (string?)m["string"];
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }

                var info = new MatcherInfo
                {
                    Pattern = BuildPattern(s),
                    Specificity = s.Replace("#", string.Empty).Length,
                    Negate = (bool?)m["negate"] ?? false,
                    FixedValue = (double?)m["value"],
                };

                var skeleton = Skeleton(s);
                if (!db._bySkeleton.TryGetValue(skeleton, out var list))
                {
                    list = new List<(StatEntry, MatcherInfo)>();
                    db._bySkeleton[skeleton] = list;
                }

                list.Add((entry, info));
            }
        }

        return db;
    }

    /// <summary>Сопоставляет одну строку мода с trade-статом или <c>null</c>.</summary>
    public MatchedStat? MatchLine(string text, ModKind kind)
    {
        var trimmed = text.Trim();
        var skeleton = Skeleton(trimmed);
        if (!_bySkeleton.TryGetValue(skeleton, out var candidates))
        {
            return null;
        }

        MatchedStat? best = null;
        var bestSpec = -1;
        foreach (var (stat, m) in candidates)
        {
            m.Compiled ??= new Regex(m.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var match = m.Compiled.Match(trimmed);
            if (!match.Success || m.Specificity <= bestSpec)
            {
                continue;
            }

            var nums = new List<double>();
            for (var g = 1; g < match.Groups.Count; g++)
            {
                if (match.Groups[g].Success &&
                    double.TryParse(match.Groups[g].Value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out var n))
                {
                    nums.Add(n);
                }
            }

            double? value = m.FixedValue ?? (nums.Count > 0 ? nums.Average() : null);
            if (m.Negate && value.HasValue)
            {
                value = -value;
            }

            best = new MatchedStat(stat.Id, PickTradeId(stat, kind), kind, trimmed, value, nums);
            bestSpec = m.Specificity;
        }

        return best;
    }

    /// <summary>Сопоставляет все моды предмета и добавляет псевдо-моды.</summary>
    public ItemStats Match(ParsedItem item)
    {
        var matched = new List<MatchedStat>();
        foreach (var (mod, kind) in EnumerateMods(item))
        {
            if (MatchLine(mod, kind) is { } ms)
            {
                matched.Add(ms);
            }
        }

        return new ItemStats { Mods = matched, Pseudo = PseudoRules.Compute(matched) };
    }

    private static IEnumerable<(string Text, ModKind Kind)> EnumerateMods(ParsedItem item)
    {
        foreach (var m in item.Implicits) yield return (m.Text, ModKind.Implicit);
        foreach (var m in item.Enchants) yield return (m.Text, ModKind.Enchant);
        foreach (var m in item.Runes) yield return (m.Text, ModKind.Rune);
        foreach (var m in item.Explicits) yield return (m.Text, m.Kind);
    }

    private static string? PickTradeId(StatEntry stat, ModKind kind)
    {
        var key = kind.ToString().ToLowerInvariant();
        if (stat.TradeByKind.TryGetValue(key, out var id))
        {
            return id;
        }

        return stat.TradeByKind.TryGetValue("explicit", out var ex)
            ? ex
            : stat.TradeByKind.Values.FirstOrDefault();
    }

    private static string BuildPattern(string matcher)
    {
        var escaped = Regex.Escape(matcher);
        var withGroups = escaped.Replace("\\#", "#").Replace("#", @"([+-]?\d+(?:\.\d+)?)");
        return "^" + withGroups + "$";
    }

    private static string Skeleton(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        var n = 0;
        foreach (var c in s)
        {
            if (char.IsLetter(c))
            {
                buf[n++] = char.ToLowerInvariant(c);
            }
        }

        return new string(buf[..n]);
    }
}
