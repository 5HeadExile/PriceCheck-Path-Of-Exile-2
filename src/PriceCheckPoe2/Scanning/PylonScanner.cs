using System.Drawing;
using System.Text.RegularExpressions;
using PriceCheckPoe2.Capture;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Ocr;
using PriceCheckPoe2.Pricing;

namespace PriceCheckPoe2.Scanning;

/// <summary>Награда с ценой и её прямоугольником на экране (для отрисовки рядом).</summary>
/// <param name="HasCount">Строка имела явное количество («1x»/«3x») — сильный
/// признак строки-награды (даже если цены нет). Используется в confirm-gate.</param>
public sealed record PricedReward(
    int Stack,
    string Name,
    RewardPrice? Price,
    double LineTotal,
    Rectangle ScreenBounds,
    bool HasCount = false);

/// <summary>Результат оценки одной области-пилона: награды, сумма и сама область.</summary>
/// <param name="PricesLoaded">Прайс-таблица была непустой на момент скана. Если false
/// (фетч poe.ninja не прошёл) — все награды без цены вынужденно, и монитор должен
/// пересканировать, а не «замораживать» пустой результат.</param>
public sealed record PylonScanResult(
    string PylonId,
    double TotalExalted,
    IReadOnlyList<PricedReward> Rewards,
    Rectangle Region,
    bool PricesLoaded = true);

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

    /// <summary>
    /// Офлайн-скан готового кадра пилона (PNG) с живыми ценами — для проверки
    /// оценщика без захвата экрана. Возвращает результат и трассу решений по
    /// каждой строке OCR (keep/drop/dedup/title), как в scan_log.txt.
    /// </summary>
    public async Task<(PylonScanResult Result, IReadOnlyList<string> Trace, int PriceCount)> ScanImageAsync(
        string id, Bitmap bitmap, CancellationToken cancellationToken = default)
    {
        var priceTable = await _prices.GetAsync(_config.League, cancellationToken).ConfigureAwait(false);
        var parser = priceTable.Count > 0
            ? RewardParser.FromNames(priceTable.Keys)
            : _fallbackParser ?? RewardParser.FromNames(Array.Empty<string>());

        var region = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var trace = new List<string>();
        var result = ScanBitmap(id, bitmap, region, parser, priceTable, trace);
        return (result, trace, priceTable.Count);
    }

    private PylonScanResult ScanRegion(
        string id,
        Rectangle region,
        RewardParser parser,
        IReadOnlyDictionary<string, RewardPrice> priceTable)
    {
        using var bitmap = ScreenCapturer.Capture(region);
        if (_config.SaveOcrDebugImages)
        {
            TrySaveRaw(id, bitmap);
        }

        return ScanBitmap(id, bitmap, region, parser, priceTable,
            _config.SaveOcrDebugImages ? new List<string>() : null);
    }

    /// <summary>Сохраняет сырой кадр области (до предобработки) — фикстура для офлайн-стенда (--scan).</summary>
    private static void TrySaveRaw(string id, Bitmap bitmap)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"region-raw-{id}-{DateTime.Now:HHmmss-fff}.png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch
        {
            // не критично
        }
    }

    /// <summary>
    /// Ядро оценки: OCR кадра → разбор количества/имени → цена из таблицы → дедуп.
    /// Кадр приходит снаружи (захват экрана или PNG), <paramref name="region"/>
    /// задаёт смещение строк в экранные координаты (для офлайна — (0,0,W,H)).
    /// </summary>
    private PylonScanResult ScanBitmap(
        string id,
        Bitmap bitmap,
        Rectangle region,
        RewardParser parser,
        IReadOnlyDictionary<string, RewardPrice> priceTable,
        List<string>? log)
    {
        // Много-проходный OCR: панели бывают разной яркости, поэтому бинаризуем
        // несколькими порогами и объединяем строки — дедуп ниже оставит на каждую
        // строку вариант с ценой. Тёмная панель читается низким порогом, яркая —
        // высоким; так не теряем строки из-за единственного неподходящего порога.
        var thresholds = _config.OcrThresholds is { Count: > 0 } set
            ? set
            : new List<int> { _config.OcrThreshold };
        var lines = new List<OcrLine>();
        foreach (var th in thresholds)
        {
            lines.AddRange(_ocr.ReadLines(bitmap, th));
        }

        // Сортируем по вертикали — чтобы дедуп по перекрытию шёл сверху вниз и
        // строки одной награды (из разных проходов) гарантированно сопоставлялись.
        lines.Sort((a, b) => a.Bounds.Y.CompareTo(b.Bounds.Y));

        var rewards = new List<PricedReward>();
        double total = 0;

        foreach (var line in lines)
        {
            var (stack, afterCount) = SplitCount(line.Text);
            var hasCount = HasExplicitCount(line.Text);
            var name = StripTag(afterCount);            // снимаем тег "Skill:/Support:"
            var canonical = parser.Parse(name);

            RewardPrice? price = null;
            double lineTotal = 0;
            string displayName;

            if (IsPanelTitle(name))
            {
                log?.Add($"  title-skip: '{line.Text}'");
                continue;
            }

            if (canonical is not null)
            {
                priceTable.TryGetValue(canonical, out price);
                lineTotal = (price?.ExaltedValue ?? 0) * stack;
                displayName = canonical;
            }
            else if (KnownRewardTag.IsMatch(afterCount) || LooksLikeName(name))
            {
                // Строка-награда распознана (явный тег Skill/Support или текст похож
                // на настоящее название), но цены на poe.ninja нет → покажем её с «?»,
                // не выбрасываем. Раньше тут был обход по голому «Nx» (hasCount), но
                // мусор от строки рун-иконок иногда тащит случайное число → давал
                // ложную «?»-плашку. Теперь имя обязано пройти LooksLikeName; реальные
                // награды с «Nx» (если без цены) и так проходят его по словам.
                displayName = name;
            }
            else
            {
                log?.Add($"  drop: '{line.Text}'");
                continue;
            }

            // Координаты строки OCR → экранные координаты.
            var screen = new Rectangle(
                region.X + line.Bounds.X,
                region.Y + line.Bounds.Y,
                line.Bounds.Width,
                line.Bounds.Height);

            // Дедуп: OCR иногда отдаёт две строки на одну награду (налипшие плашки).
            // Если новая строка перекрывает уже добавленную по вертикали — оставляем
            // более информативную (с ценой).
            var dupIndex = rewards.FindIndex(r => VerticalOverlap(r.ScreenBounds, screen) > 0.6);
            if (dupIndex >= 0)
            {
                if (price is not null && rewards[dupIndex].Price is null)
                {
                    total += lineTotal;
                    rewards[dupIndex] = new PricedReward(stack, displayName, price, lineTotal, screen, hasCount);
                }

                log?.Add($"  dedup-skip: '{line.Text}' (overlaps '{rewards[dupIndex].Name}')");
                continue;
            }

            if (price is not null)
            {
                total += lineTotal;
            }

            log?.Add($"  keep: x{stack} '{displayName}' -> {(price is null ? "?" : price.ExaltedValue.ToString("0.##") + " ex")}");
            rewards.Add(new PricedReward(stack, displayName, price, lineTotal, screen, hasCount));
        }

        if (log is not null)
        {
            WriteScanLog(id, lines.Count, log);
        }

        return new PylonScanResult(id, total, rewards, region, priceTable.Count > 0);
    }

    /// <summary>Это строка-заголовок панели («Runeshape Combinations»), а не награда.</summary>
    private static bool IsPanelTitle(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("runeshape") || n.Contains("combination");
    }

    /// <summary>
    /// Похоже на настоящее название награды — а не мусор OCR от строки рун-иконок,
    /// которая идёт над названием (напр. «- oy V- DRI s - - e», «Crat . = * AN»,
    /// «Wt SN ) B A r»). Требуем ≥2 «слов» из ≥3 букв подряд И высокую долю букв
    /// среди непробельных символов: иконочный мусор полон одиночных букв, цифр и
    /// спецсимволов и отсекается, а «Greater Jeweller's Orb» / «Uncut Spirit Gem»
    /// проходят. Без этого каждая награда давала лишнюю «?»-плашку.
    /// </summary>
    internal static bool LooksLikeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length < 6)
        {
            return false;
        }

        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var wordyWords = words.Count(w => w.Count(char.IsLetter) >= 3);
        // ≥2 настоящих слова И они должны составлять большинство токенов: иконочный
        // мусор вида «NI P o oo RIS JUS SO ps S NN o o I - 9» имеет пару 3-буквенных
        // обрывков среди кучи односимвольного шума — отсекаем по доле слов.
        if (wordyWords < 2 || wordyWords < words.Length * 0.5)
        {
            return false;
        }

        var nonSpace = trimmed.Count(c => !char.IsWhiteSpace(c));
        var letters = trimmed.Count(c => char.IsLetter(c) || c == '\'');
        return nonSpace > 0 && (double)letters / nonSpace >= 0.75;
    }

    private void WriteScanLog(string id, int lineCount, List<string> entries)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "scan_log.txt");
            var text = $"[{DateTime.Now:HH:mm:ss}] pylon '{id}': OCR lines={lineCount}{Environment.NewLine}"
                + string.Join(Environment.NewLine, entries) + Environment.NewLine;
            File.AppendAllText(path, text);
        }
        catch
        {
            // лог не критичен
        }
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

    /// <summary>Есть ли в строке явное количество («1x Foo», «3 Foo», «Foo x12»).</summary>
    internal static bool HasExplicitCount(string line)
    {
        var text = line.Trim();
        var lead = LeadingCount.Match(text);
        if (lead.Success && int.TryParse(lead.Groups[1].Value, out _) && lead.Groups[2].Value.Any(char.IsLetter))
        {
            return true;
        }

        var trail = TrailingCount.Match(text);
        return trail.Success && int.TryParse(trail.Groups[2].Value, out _) && trail.Groups[1].Value.Any(char.IsLetter);
    }

    /// <summary>Доля вертикального перекрытия двух прямоугольников (0..1) — для дедупа строк.</summary>
    internal static double VerticalOverlap(Rectangle a, Rectangle b)
    {
        var top = Math.Max(a.Top, b.Top);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        var inter = bottom - top;
        if (inter <= 0)
        {
            return 0;
        }

        var minHeight = Math.Min(a.Height, b.Height);
        return minHeight <= 0 ? 0 : (double)inter / minHeight;
    }

    /// <summary>Снимает тег-префикс ("Skill: ", "Support: ") — берём часть после двоеточия.</summary>
    internal static string StripTag(string text)
    {
        var m = TagPrefix.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : text.Trim();
    }
}
