using Newtonsoft.Json;

namespace PriceCheckPoe2.Ocr;

/// <summary>
/// Приводит «грязный» текст OCR к каноническим именам наград. Сначала пытается
/// найти точный алиас, затем — нечёткое совпадение по расстоянию Левенштейна.
/// При низкой уверенности возвращает <c>null</c>, чтобы оверлей показал «?»
/// вместо неверной цены.
/// </summary>
public sealed class RewardParser
{
    private readonly Dictionary<string, string> _aliasToCanonical;
    // Канонические имена вместе с предвычисленной нормализованной формой — чтобы
    // не нормализовать их заново на каждой строке OCR в нечётком поиске.
    private readonly List<(string Canonical, string Normalized)> _canonicalNames;
    private readonly int _maxDistance;

    public RewardParser(
        IReadOnlyDictionary<string, IReadOnlyList<string>> canonicalToAliases,
        int maxDistance = 3)
    {
        _maxDistance = maxDistance;
        _aliasToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _canonicalNames = new List<(string, string)>();

        foreach (var (canonical, aliases) in canonicalToAliases)
        {
            _canonicalNames.Add((canonical, Normalize(canonical)));
            _aliasToCanonical[Normalize(canonical)] = canonical;
            foreach (var alias in aliases)
            {
                _aliasToCanonical[Normalize(alias)] = canonical;
            }
        }
    }

    /// <summary>Загружает парсер из JSON вида { "Canonical": ["alias", ...] }.</summary>
    public static RewardParser FromJson(string json, int maxDistance = 3)
    {
        var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                  ?? new Dictionary<string, List<string>>();
        var map = raw.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value);
        return new RewardParser(map, maxDistance);
    }

    public static RewardParser FromFile(string path, int maxDistance = 3) =>
        FromJson(File.ReadAllText(path), maxDistance);

    /// <summary>
    /// Строит парсер из набора канонических имён (без алиасов) — например, из
    /// живого прайс-листа poe.ninja, чтобы имена всегда совпадали с источником цен.
    /// </summary>
    public static RewardParser FromNames(IEnumerable<string> names, int maxDistance = 4)
    {
        var map = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, _ => (IReadOnlyList<string>)Array.Empty<string>());
        return new RewardParser(map, maxDistance);
    }

    /// <summary>
    /// Возвращает каноническое имя для строки OCR или <c>null</c>, если
    /// уверенного совпадения нет.
    /// </summary>
    public string? Parse(string ocrLine)
    {
        var normalized = Normalize(ocrLine);
        if (normalized.Length == 0)
        {
            return null;
        }

        if (_aliasToCanonical.TryGetValue(normalized, out var exact))
        {
            return exact;
        }

        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var (canonical, canonicalNorm) in _canonicalNames)
        {
            var distance = Levenshtein(normalized, canonicalNorm);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = canonical;
            }
        }

        return bestDistance <= _maxDistance ? best : null;
    }

    // Оставляем только буквы и цифры: убирает пробелы, апострофы и мусор OCR
    // (например, «Jeweller's» и «Jewellers» становятся одинаковыми).
    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    internal static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
