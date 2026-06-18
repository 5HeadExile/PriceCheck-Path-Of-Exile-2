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
    // "5x Exalted Orb", "Exalted Orb x5", "5 Exalted Orb" → (5, "Exalted Orb").
    private static readonly Regex LeadingCount =
        new(@"^\s*(\d+)\s*[xX]?\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex TrailingCount =
        new(@"^(.+?)\s*[xX]?\s*(\d+)\s*$", RegexOptions.Compiled);

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
            var (stack, text) = SplitCount(line.Text);
            var canonical = parser.Parse(text);
            if (canonical is null)
            {
                continue;
            }

            priceTable.TryGetValue(canonical, out var price);
            var lineTotal = (price?.ExaltedValue ?? 0) * stack;
            total += lineTotal;

            // Координаты строки OCR → экранные координаты.
            var screen = new Rectangle(
                region.X + line.Bounds.X,
                region.Y + line.Bounds.Y,
                line.Bounds.Width,
                line.Bounds.Height);

            rewards.Add(new PricedReward(stack, canonical, price, lineTotal, screen));
        }

        return new PylonScanResult(id, total, rewards, region);
    }

    /// <summary>Извлекает количество из строки OCR, возвращает (стак, остаток).</summary>
    internal static (int Stack, string Text) SplitCount(string line)
    {
        var lead = LeadingCount.Match(line);
        if (lead.Success && int.TryParse(lead.Groups[1].Value, out var leadCount))
        {
            return (leadCount, lead.Groups[2].Value.Trim());
        }

        var trail = TrailingCount.Match(line);
        if (trail.Success && int.TryParse(trail.Groups[2].Value, out var trailCount))
        {
            return (trailCount, trail.Groups[1].Value.Trim());
        }

        return (1, line.Trim());
    }
}
