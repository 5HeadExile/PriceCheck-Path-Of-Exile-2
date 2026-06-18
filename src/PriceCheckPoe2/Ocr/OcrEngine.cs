using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace PriceCheckPoe2.Ocr;

/// <summary>
/// Обёртка над Tesseract. На тёмном фоне PoE2 текст распознаётся плохо, поэтому
/// перед OCR применяем предобработку (апскейл + grayscale + бинаризация по
/// настраиваемому порогу). Порог подбирается на реальных скриншотах пилонов.
/// </summary>
public sealed class OcrEngine : IDisposable
{
    private readonly TesseractEngine _engine;
    private readonly int _threshold;
    private readonly bool _saveDebug;

    public OcrEngine(string? tessDataPath = null, int threshold = 110, bool saveDebug = false)
    {
        var path = tessDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(path, "eng", EngineMode.Default);
        _threshold = Math.Clamp(threshold, 0, 255);
        _saveDebug = saveDebug;
    }

    /// <summary>Распознаёт текст в области и возвращает непустые строки.</summary>
    public IReadOnlyList<string> ReadLines(Bitmap region)
    {
        using var prepared = Preprocess(region);

        if (_saveDebug)
        {
            TrySaveDebug(prepared);
        }

        using var pix = PixConverter.ToPix(prepared);
        using var page = _engine.Process(pix);

        return page.GetText()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Апскейл ×2, grayscale и бинаризация по порогу.</summary>
    private Bitmap Preprocess(Bitmap source)
    {
        var scaled = new Bitmap(source.Width * 2, source.Height * 2, PixelFormat.Format24bppRgb);
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
