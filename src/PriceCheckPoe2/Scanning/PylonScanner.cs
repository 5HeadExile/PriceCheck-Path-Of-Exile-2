using System.Drawing;
using System.Text.RegularExpressions;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Ocr;
using PriceCheckPoe2.Pricing;

namespace PriceCheckPoe2.Scanning;

/// <summary>Награда с ценой и её прямоугольником на экране (для отрисовки рядом).</summary>
public sealed record PricedReward(
    int Stack,
    string Name,
    RewardPrice? Price,
    double LineTotal,
    Rectangle ScreenBounds);

/// <summary>Результат оценки одной области-пилона: награды, сумма и сама область.</summary>
public sealed record PylonScanResult(
    string PylonId,
    double TotalExalted,
    IReadOnlyList<PricedReward> Rewards,
    Rectangle Region);

/// <summary>
/// Связывает весь пайплайн: захват области → OCR (со строками и координатами) →
/// разбор количества и имени → цена из кэша → суммарная EV. Канонические имена
/// берутся из живого прайс-листа poe.ninja, чтобы совпадать с источником цен;
/// при пустом прайсе используется запасной парсер (из reward-aliases.json).
/// </summary>
public sealed class PylonScanner
{
    // Количество в начале: "3x Foo", "3 Foo", "3xFoo" (склеено). x опционален.
    private static readonly Regex LeadingCount =
        new(@"^\s*(\d+)\s*[xX×]?\s*(.+)$", RegexOptions.Compiled);
    // В конце — только с явным x: "Foo x12". Без x не трогаем, иначе схватит
    // число из "(Level 19)".
    private static readonly Regex TrailingCount =
        new(@"^(.+?)\s*[xX×]\s*(\d+)\s*$", RegexOptions.Compiled);
    // Тег-префикс награды: "Skill: ...", "Support: ..." → часть после двоеточия.
    private static readonly Regex TagPrefix =
        new(@"^\s*\p{L}[\p{L} ]{1,14}:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex KnownRewardTag =
        new(@"^\s*(skill|support)\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly OcrEngine _ocr;
    private readonly PriceCache _prices;
    private readonly AppConfig _config;
    private readonly RewardParser? _fallbackParser;

    public PylonScanner(OcrEngine ocr, PriceCache prices, AppConfig config, RewardParser? fallbackParser = null)
    {
        _ocr = ocr;
        _prices = prices;
        _config = config;
        _fallbackParser = fallbackParser;
    }

    /// <summary>Сканирует и оценивает все переданные области-пилоны.</summary>
    public async Task<IReadOnlyList<PylonScanResult>> ScanAllAsync(
        IReadOnlyList<(string Id, Rectangle Region)> pylons,
        CancellationToken cancellationToken = default)
    {
        var priceTable = await _prices.GetAsync(_config.League, cancellationToken).ConfigureAwait(false);
        var parser = priceTable.Count > 0
            ? RewardParser.FromNames(priceTable.Keys)
            : _fallbackParser ?? RewardParser.FromNames(Array.Empty<string>());

        var results = new List<PylonScanResult>(pylons.Count);
        foreach (var (id, region) in pylons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(ScanRegion(id, region, parser, priceTable));
        }

        return results;
    }

    private PylonScanResult ScanRegion(
        string id,
        Rectangle region,
        RewardParser parser,
        IReadOnlyDictionary<string, RewardPrice> priceTable)
    {
        using var bitmap = ScreenCapturer.Capture(region);
        var lines = _ocr.ReadLines(bitmap);

        var rewards = new List<PricedReward>();
        double total = 0;

        foreach (var line in lines)
        {
            var (stack, afterCount) = SplitCount(line.Text);
            var name = StripTag(afterCount);            // снимаем тег "Skill:/Support:"
            var canonical = parser.Parse(name);

            RewardPrice? price = null;
            double lineTotal = 0;
            string displayName;

            if (canonical is not null)
            {
                priceTable.TryGetValue(canonical, out price);
                lineTotal = (price?.ExaltedValue ?? 0) * stack;
                total += lineTotal;
                displayName = canonical;
            }
            else if (KnownRewardTag.IsMatch(afterCount))
            {
                // Скилл/саппорт распознан по тегу, но цены на poe.ninja для него нет.
                displayName = name;
            }
            else
            {
                continue;
            }

            // Координаты строки OCR → экранные координаты.
            var screen = new Rectangle(
                region.X + line.Bounds.X,
                region.Y + line.Bounds.Y,
                line.Bounds.Width,
                line.Bounds.Height);

            rewards.Add(new PricedReward(stack, displayName, price, lineTotal, screen));
        }

        return new PylonScanResult(id, total, rewards, region);
    }

    /// <summary>Извлекает количество из строки OCR, возвращает (стак, остаток).</summary>
    internal static (int Stack, string Text) SplitCount(string line)
    {
        var text = line.Trim();

        var lead = LeadingCount.Match(text);
        if (lead.Success && int.TryParse(lead.Groups[1].Value, out var leadCount)
            && lead.Groups[2].Value.Any(char.IsLetter))
        {
            return (leadCount, lead.Groups[2].Value.Trim());
        }

        var trail = TrailingCount.Match(text);
        if (trail.Success && int.TryParse(trail.Groups[2].Value, out var trailCount)
            && trail.Groups[1].Value.Any(char.IsLetter))
        {
            return (trailCount, trail.Groups[1].Value.Trim());
        }

        return (1, text);
    }

    /// <summary>Снимает тег-префикс ("Skill: ", "Support: ") — берём часть после двоеточия.</summary>
    internal static string StripTag(string text)
    {
        var m = TagPrefix.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : text.Trim();
    }
}
