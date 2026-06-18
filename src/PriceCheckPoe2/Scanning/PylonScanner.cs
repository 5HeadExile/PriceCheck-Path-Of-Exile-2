using System.Drawing;
using System.Text.RegularExpressions;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Ocr;
using PriceCheckPoe2.Pricing;

namespace PriceCheckPoe2.Scanning;

/// <summary>Оценка одного пилона вместе с его областью на экране.</summary>
public sealed record PylonScanResult(PylonValuation Valuation, Rectangle Region);

/// <summary>
/// Связывает весь пайплайн: захват области пилона → OCR → разбор строк в награды
/// (с количеством в стаке) → цены из кэша → суммарная EV-оценка. Поддерживает
/// несколько областей-пилонов за один проход (цены берутся один раз).
/// </summary>
public sealed class PylonScanner
{
    // "5x Exalted Orb", "Exalted Orb x5", "5 Exalted Orb" → (5, "Exalted Orb").
    private static readonly Regex LeadingCount =
        new(@"^\s*(\d+)\s*[xX]?\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex TrailingCount =
        new(@"^(.+?)\s*[xX]?\s*(\d+)\s*$", RegexOptions.Compiled);

    private readonly OcrEngine _ocr;
    private readonly RewardParser _parser;
    private readonly PriceCache _prices;
    private readonly AppConfig _config;

    public PylonScanner(OcrEngine ocr, RewardParser parser, PriceCache prices, AppConfig config)
    {
        _ocr = ocr;
        _parser = parser;
        _prices = prices;
        _config = config;
    }

    /// <summary>Сканирует и оценивает все переданные области-пилоны.</summary>
    public async Task<IReadOnlyList<PylonScanResult>> ScanAllAsync(
        IReadOnlyList<(string Id, Rectangle Region)> pylons,
        CancellationToken cancellationToken = default)
    {
        var priceTable = await _prices.GetAsync(_config.League, cancellationToken).ConfigureAwait(false);
        var evaluator = new PylonEvaluator(priceTable);

        var results = new List<PylonScanResult>(pylons.Count);
        foreach (var (id, region) in pylons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rewards = ReadRewards(region);
            var valuation = evaluator.Evaluate(new Pylon(id, rewards));
            results.Add(new PylonScanResult(valuation, region));
        }

        return results;
    }

    /// <summary>Захватывает область, распознаёт и парсит награды.</summary>
    private IReadOnlyList<Reward> ReadRewards(Rectangle region)
    {
        using var bitmap = ScreenCapturer.Capture(region);
        var lines = _ocr.ReadLines(bitmap);

        var rewards = new List<Reward>();
        foreach (var line in lines)
        {
            var (stack, text) = SplitCount(line);
            var canonical = _parser.Parse(text);
            if (canonical is not null)
            {
                rewards.Add(new Reward(canonical, stack));
            }
        }

        return rewards;
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
