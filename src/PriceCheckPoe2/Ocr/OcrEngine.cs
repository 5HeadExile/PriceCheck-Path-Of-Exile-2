using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace PriceCheckPoe2.Ocr;

/// <summary>
/// Обёртка над Tesseract. На тёмном фоне PoE2 текст распознаётся плохо, поэтому
/// перед OCR применяем предобработку (grayscale + увеличение + порог).
/// TODO(M3): подобрать параметры предобработки на реальных скриншотах пилонов.
/// </summary>
public sealed class OcrEngine : IDisposable
{
    private readonly TesseractEngine _engine;

    public OcrEngine(string? tessDataPath = null)
    {
        var path = tessDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(path, "eng", EngineMode.Default);
    }

    /// <summary>Распознаёт текст в области и возвращает непустые строки.</summary>
    public IReadOnlyList<string> ReadLines(Bitmap region)
    {
        using var prepared = Preprocess(region);
        using var pix = PixConverter.ToPix(prepared);
        using var page = _engine.Process(pix);

        return page.GetText()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Простейшая предобработка: апскейл ×2 и перевод в grayscale.</summary>
    private static Bitmap Preprocess(Bitmap source)
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
                var v = lum > 110 ? 255 : 0; // порог, TODO(M3): откалибровать
                scaled.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        return scaled;
    }

    public void Dispose() => _engine.Dispose();
}
