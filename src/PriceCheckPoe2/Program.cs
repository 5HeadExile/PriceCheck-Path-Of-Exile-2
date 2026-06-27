using System.Windows.Forms;
using PriceCheckPoe2.Tray;

namespace PriceCheckPoe2;

internal static class Program
{
    /// <summary>
    /// Точка входа. Приложение стартует свёрнутым в трей (без главного окна):
    /// весь жизненный цикл держит <see cref="TrayApplicationContext"/>, а UI
    /// (игровое меню, настройки, оверлей цен) поднимается по запросу.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Масштаб UI под DPI монитора (иначе на 125/150% окна мелкие) + пользовательский
        // множитель из конфига (на мониторах со 100% масштабом плашки иначе мелкие).
        Theme.Ui.Init(Config.AppConfig.Load().UiScale);

        // Технический режим предпросмотра UI (для разработки): показывает окно
        // настроек поверх затемнения без трея/игры. Снимок → проверка дизайна.
        if (args.Length > 0 && args[0] == "--preview")
        {
            var cfg = Config.AppConfig.Load();
            using var settings = new Settings.SettingsForm(cfg);
            Application.Run(settings);
            return;
        }

        if (args.Length > 0 && args[0] == "--preview-menu")
        {
            using var menu = new Overlay.MenuWindow(overlayActive: true, hasSelection: true)
            {
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true,
            };
            Application.Run(menu);
            return;
        }

        // Технический рендер образцов ценников на тёмном фоне (проверка дизайна):
        // --shot-chips <путь>.
        if (args.Length >= 2 && args[0] == "--shot-chips")
        {
            var samples = new Overlay.PriceChip.Model[]
            {
                new(false, false, 0.3, "0.3 ex", null),    // T1 трив. (<1)
                new(false, false, 3.0, "3 ex", null),       // T2 дёшево (1..5)
                new(false, false, 12.0, "12 ex", null),     // T3 заметно (5..20)
                new(false, false, 40.0, "40 ex", null),     // T4 дорого (20..75)
                new(false, false, 120.0, "1.2 div", null),  // T5 топ (>=75)
                new(false, false, 8.0, "8 ex", "4/шт"),     // стак
                new(true, false, 0, "?", null),             // нет цены
                new(false, true, 90.0, "0.9 div", null),    // лучшая
            };

            const float em = 24f; // фиксированный размер для превью образцов
            var rowStep = (int)(Overlay.PriceChip.HeightFor(em) + Theme.Ui.S(13));
            using var bmp = new System.Drawing.Bitmap(560, 24 + samples.Length * rowStep);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                // Левая половина — тёмный фон, правая — светлый пергамент (как панель пилона).
                using (var dark = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(28, 24, 20)))
                {
                    g.FillRectangle(dark, 0, 0, 280, bmp.Height);
                }

                using (var parch = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(206, 192, 165)))
                {
                    g.FillRectangle(parch, 280, 0, 280, bmp.Height);
                }

                float y = 14;
                foreach (var m in samples)
                {
                    var w = Overlay.PriceChip.MeasureWidth(g, m, em);
                    Overlay.PriceChip.Draw(g, new System.Drawing.RectangleF(16, y, w, Overlay.PriceChip.HeightFor(em)), m, em);
                    Overlay.PriceChip.Draw(g, new System.Drawing.RectangleF(296, y, w, Overlay.PriceChip.HeightFor(em)), m, em);
                    y += rowStep;
                }
            }

            bmp.Save(args[1]);
            return;
        }

        // Технический рендер окна в PNG (для проверки дизайна без захвата экрана):
        // --shot menu|settings <путь>.
        if (args.Length >= 3 && args[0] == "--shot")
        {
            Form form = args[1] == "menu"
                ? new Overlay.MenuWindow(overlayActive: true, hasSelection: true)
                : new Settings.SettingsForm(Config.AppConfig.Load());
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-3000, -3000);
            form.Show();
            Application.DoEvents();
            using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(args[2]);
            form.Close();
            return;
        }

        // Офлайн-стенд оценщика: прогон кадра пилона (PNG) через весь пайплайн без
        // захвата экрана/игры. Печатает отчёт (награды, цены, тиры, дубликаты, трасса
        // решений) и рендерит плашки поверх кадра в <image>.scan.png — чтобы видеть
        // результат глазами. Использование:
        //   --scan <image.png> [whole]   whole = сканировать весь кадр (уже обрезанная панель)
        if (args.Length >= 2 && args[0] == "--scan")
        {
            RunScan(args);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }

    private static void RunScan(string[] args)
    {
        var imagePath = args[1];
        var whole = args.Contains("whole");
        // Необяз. числовой аргумент — переопределение порога OCR (для подбора в стенде).
        int? thresholdOverride = null;
        foreach (var a in args.Skip(2))
        {
            if (int.TryParse(a, out var t))
            {
                thresholdOverride = t;
            }
        }

        // WinExe не пишет в родительскую консоль — перехватываем вывод в файл-отчёт.
        var reportPath = System.IO.Path.ChangeExtension(imagePath, null) + ".scan.txt";
        var capture = new System.IO.StringWriter();
        Console.SetOut(capture);
        try
        {
        if (!System.IO.File.Exists(imagePath))
        {
            Console.WriteLine($"Файл не найден: {imagePath}");
            return;
        }

        var cfg = Config.AppConfig.Load();
        using var source = new System.Drawing.Bitmap(imagePath);
        Console.WriteLine($"Кадр: {imagePath} ({source.Width}x{source.Height})");

        // Какие области сканируем: либо весь кадр, либо обрезаем по калибровке.
        var jobs = new List<(string Id, System.Drawing.Rectangle Crop)>();
        if (whole || cfg.PylonRegions.Count == 0)
        {
            jobs.Add(("whole", new System.Drawing.Rectangle(0, 0, source.Width, source.Height)));
        }
        else
        {
            foreach (var p in cfg.PylonRegions)
            {
                var crop = System.Drawing.Rectangle.Intersect(
                    new System.Drawing.Rectangle(p.X, p.Y, p.Width, p.Height),
                    new System.Drawing.Rectangle(0, 0, source.Width, source.Height));
                // Калибровка не подходит размеру кадра (другое разрешение) → весь кадр.
                if (crop.Width < 40 || crop.Height < 40)
                {
                    crop = new System.Drawing.Rectangle(0, 0, source.Width, source.Height);
                }

                jobs.Add((p.Name, crop));
            }
        }

        // Явный порог в аргументе → один проход (для диагностики); иначе много-проходный из конфига.
        if (thresholdOverride is { } th)
        {
            cfg.OcrThresholds = new List<int> { th };
        }

        Console.WriteLine($"OCR thresholds: {string.Join(",", cfg.OcrThresholds)}");
        using var ocr = new Ocr.OcrEngine(threshold: cfg.OcrThreshold, saveDebug: false);
        var aliasesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "reward-aliases.json");
        var fallback = System.IO.File.Exists(aliasesPath) ? Ocr.RewardParser.FromFile(aliasesPath) : null;
        var priceCache = new Pricing.PriceCache(
            new Pricing.PoeNinjaClient(cfg), TimeSpan.FromMinutes(cfg.PriceRefreshMinutes));
        var scanner = new Scanning.PylonScanner(ocr, priceCache, cfg, fallback);

        foreach (var (id, crop) in jobs)
        {
            using var panel = source.Clone(crop, source.PixelFormat);
            var (result, trace, priceCount) = scanner
                .ScanImageAsync(id, panel).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine($"=== {id}  (crop {crop.X},{crop.Y} {crop.Width}x{crop.Height}) ===");
            Console.WriteLine($"Цен в таблице: {priceCount}   Наград: {result.Rewards.Count}   Сумма: {result.TotalExalted:0.##} ex");
            Console.WriteLine($"{"#",2}  {"stack",5}  {"ex",10}  {"div",8}  {"tier",-8}  name");

            var dupGroups = result.Rewards
                .Where(r => r.Price is not null || PylonScanner_LooksReal(r.Name))
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < result.Rewards.Count; i++)
            {
                var r = result.Rewards[i];
                var ex = r.Price is null ? "—" : (r.Price.ExaltedValue * r.Stack).ToString("0.##");
                var div = r.Price?.DivineValue is { } d ? (d * r.Stack).ToString("0.##") : "—";
                var tier = r.Price is null ? "?" : TierName(r.Price.ExaltedValue);
                var dup = dupGroups.ContainsKey(r.Name) ? "  <DUP>" : "";
                Console.WriteLine($"{i,2}  {("x" + r.Stack),5}  {ex,10}  {div,8}  {tier,-8}  y={r.ScreenBounds.Y,4} h={r.ScreenBounds.Height,3}  {r.Name}{dup}");
            }

            if (dupGroups.Count > 0)
            {
                Console.WriteLine("ДУБЛИКАТЫ: " + string.Join(", ", dupGroups.Select(kv => $"'{kv.Key}' x{kv.Value}")));
            }

            Console.WriteLine("ТРАССА:");
            foreach (var t in trace)
            {
                Console.WriteLine("  " + t);
            }

            var outPath = System.IO.Path.ChangeExtension(imagePath, null) + $".scan-{id}.png";
            using var rendered = RenderScan(panel, result);
            rendered.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"Рендер: {outPath}");
        }
        }
        finally
        {
            try { System.IO.File.WriteAllText(reportPath, capture.ToString()); } catch { /* отчёт не критичен */ }
        }
    }

    private static string TierName(double ex) =>
        ex < 1.0 ? "trivial" : ex < 5.0 ? "cheap" : ex < 20.0 ? "notable" : ex < 75.0 ? "pricey" : "top";

    // «Похоже на реальную награду» для группировки дублей в отчёте (без цены тоже).
    private static bool PylonScanner_LooksReal(string name) =>
        Scanning.PylonScanner.LooksLikeName(name);

    /// <summary>Рисует кадр + плашки справа от каждой строки (как живой оверлей) для глаз.</summary>
    private static System.Drawing.Bitmap RenderScan(System.Drawing.Bitmap panel, Scanning.PylonScanResult result)
    {
        const int margin = 360;
        var canvas = new System.Drawing.Bitmap(panel.Width + margin, panel.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(canvas);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.DrawImage(panel, 0, 0);
        using (var sep = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(20, 18, 16)))
        {
            g.FillRectangle(sep, panel.Width, 0, margin, panel.Height);
        }

        var best = result.Rewards.Where(x => x.Price is not null)
            .OrderByDescending(x => x.LineTotal).FirstOrDefault();

        // Тот же авто-масштаб, что в живом оверлее: em = 40-й перцентиль высоты строк
        // (низкий перцентиль игнорирует завышенные bbox-артефакты OCR).
        var hs = result.Rewards.Select(x => x.ScreenBounds.Height).Where(h => h > 0).OrderBy(h => h).ToList();
        float em = hs.Count > 0 ? hs[Math.Min(hs.Count - 1, (int)(hs.Count * 0.40))] : 20f;
        em = Math.Clamp(em, 10, 48);

        var chipH = Overlay.PriceChip.HeightFor(em);
        var stackGap = Math.Max(2f, em * 0.18f);
        var columnLeft = panel.Width + Theme.Ui.S(12);
        var placed = new List<System.Drawing.RectangleF>();

        foreach (var r in result.Rewards)
        {
            // Рамка строки на самом кадре — видно, что OCR взял за строку.
            using (var rp = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, 0, 200, 255)))
            {
                g.DrawRectangle(rp, r.ScreenBounds.X, r.ScreenBounds.Y, r.ScreenBounds.Width, r.ScreenBounds.Height);
            }

            var model = BuildChipModel(r, ReferenceEquals(r, best));
            var w = Overlay.PriceChip.MeasureWidth(g, model, em);
            var top = r.ScreenBounds.Y + (r.ScreenBounds.Height - chipH) / 2f;
            var rect = new System.Drawing.RectangleF(columnLeft, top, w, chipH);
            var guard = 0;
            var test = System.Drawing.RectangleF.Inflate(rect, 0, stackGap);
            while (placed.Any(p => p.IntersectsWith(test)) && guard++ < 64)
            {
                rect.Y = placed.Where(p => p.IntersectsWith(test)).Max(p => p.Bottom) + stackGap;
                test = System.Drawing.RectangleF.Inflate(rect, 0, stackGap);
            }

            placed.Add(rect);
            Overlay.PriceChip.Draw(g, rect, model, em);
        }

        return canvas;
    }

    private static Overlay.PriceChip.Model BuildChipModel(Scanning.PricedReward r, bool best)
    {
        if (r.Price is null)
        {
            return new Overlay.PriceChip.Model(true, false, 0, "?", null);
        }

        var exTotal = r.LineTotal;
        var divTotal = (r.Price.DivineValue ?? 0) * r.Stack;
        var useDiv = divTotal >= 1.0;
        var main = useDiv ? $"{divTotal:0.##} div" : $"{exTotal:0.##} ex";
        string? unit = null;
        if (r.Stack > 1)
        {
            var u = useDiv ? (r.Price.DivineValue ?? 0) : r.Price.ExaltedValue;
            unit = $"{u:0.##}/шт";
        }

        return new Overlay.PriceChip.Model(false, best, exTotal, main, unit);
    }
}
