using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
// Tesseract тоже определяет тип ImageFormat — фиксируем простое имя на System.Drawing.
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace PriceCheckPoe2.Ocr;

/// <summary>
/// Обёртка над Tesseract. На тёмном фоне PoE2 текст распознаётся плохо, поэтому
/// перед OCR применяем предобработку (апскейл + grayscale + бинаризация по
/// настраиваемому порогу). Возвращает строки вместе с их прямоугольниками,
/// чтобы оверлей мог рисовать цену напротив каждой строки.
/// </summary>
public sealed class OcrEngine : IDisposable
{
    // Во сколько раз увеличиваем кадр перед OCR (улучшает распознавание мелкого
    // текста). Координаты bounding-box'ов делим на этот множитель обратно.
    private const int Scale = 2;

    private readonly TesseractEngine _engine;
    private readonly int _threshold;
    private readonly bool _saveDebug;

    public OcrEngine(string? tessDataPath = null, int threshold = 110, bool saveDebug = false)
    {
        var path = tessDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(path, "eng");
        _threshold = Math.Clamp(threshold, 0, 255);
        _saveDebug = saveDebug;
    }

    /// <summary>
    /// Распознаёт текст в области и возвращает непустые строки с координатами
    /// (в пикселях исходной области).
    /// </summary>
    public IReadOnlyList<OcrLine> ReadLines(Bitmap region)
    {
        using var prepared = Preprocess(region);

        if (_saveDebug)
        {
            TrySaveDebug(prepared);
        }

        // Tesseract 5.x убрал PixConverter — грузим Pix из PNG-байтов в памяти.
        using var ms = new MemoryStream();
        prepared.Save(ms, ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(ms.ToArray());
        using var page = _engine.Process(pix);

        var lines = new List<OcrLine>();
        using var iter = page.GetIterator();
        iter.Begin();
        do
        {
            var text = iter.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
            {
                // Возвращаем координаты в масштабе исходной области.
                var bounds = new Rectangle(
                    rect.X1 / Scale,
                    rect.Y1 / Scale,
                    (rect.X2 - rect.X1) / Scale,
                    (rect.Y2 - rect.Y1) / Scale);
                lines.Add(new OcrLine(text, bounds));
            }
        }
        while (iter.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    /// <summary>Апскейл, grayscale и бинаризация по порогу.</summary>
    private Bitmap Preprocess(Bitmap source)
    {
        var scaled = new Bitmap(source.Width * Scale, source.Height * Scale, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, scaled.Width, scaled.Height);
        }

        for (var y = 0; y < scaled.Height; y++)
        {
            for (var x = 0; x < scaled.Width; x++)
            {
                var c = scaled.GetPixel(x, y);
                var lum = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                var v = lum > _threshold ? 255 : 0;
                scaled.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        return scaled;
    }

    private static void TrySaveDebug(Bitmap prepared)
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                $"ocr-debug-{DateTime.Now:HHmmss-fff}.png");
            prepared.Save(path, ImageFormat.Png);
        }
        catch
        {
            // Отладочное сохранение не критично.
        }
    }

    public void Dispose() => _engine.Dispose();
}
