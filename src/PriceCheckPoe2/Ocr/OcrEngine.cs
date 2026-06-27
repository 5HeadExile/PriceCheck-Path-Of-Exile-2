using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
    public IReadOnlyList<OcrLine> ReadLines(Bitmap region) => ReadLines(region, _threshold);

    /// <summary>Распознаёт строки, бинаризуя кадр заданным порогом (0 = авто Оцу).</summary>
    public IReadOnlyList<OcrLine> ReadLines(Bitmap region, int threshold)
    {
        using var prepared = Preprocess(region, threshold);

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

    /// <summary>Апскейл, grayscale и бинаризация по порогу (0 = авто Оцу).</summary>
    private Bitmap Preprocess(Bitmap source, int threshold)
    {
        var scaled = new Bitmap(source.Width * Scale, source.Height * Scale, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, scaled.Width, scaled.Height);
        }

        // Бинаризация через LockBits: прямой доступ к буферу пикселей вместо
        // GetPixel/SetPixel (те блокируют биты на каждый вызов → ~1 млн блокировок
        // на кадр и десятки мс на скан). Формат 24bppRgb, порядок байт — BGR.
        var rect = new Rectangle(0, 0, scaled.Width, scaled.Height);
        var data = scaled.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        try
        {
            var byteCount = Math.Abs(data.Stride) * scaled.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(data.Scan0, buffer, 0, byteCount);

            // Первый проход: яркость каждого пикселя + гистограмма (для авто-порога).
            var lum = new byte[scaled.Width * scaled.Height];
            var hist = new int[256];
            var li = 0;
            for (var y = 0; y < scaled.Height; y++)
            {
                var row = y * data.Stride;
                for (var x = 0; x < scaled.Width; x++)
                {
                    var i = row + x * 3;
                    var l = (int)(0.114 * buffer[i] + 0.587 * buffer[i + 1] + 0.299 * buffer[i + 2]);
                    lum[li++] = (byte)l;
                    hist[l]++;
                }
            }

            // Порог: фиксированный (>0) или авто по Оцу (threshold<=0). Много-проходный
            // OCR зовёт нас с разными порогами (см. PylonScanner) — это надёжнее одного.
            var effThreshold = threshold > 0 ? threshold : OtsuThreshold(hist, lum.Length);

            // Второй проход: бинаризация по выбранному порогу.
            li = 0;
            for (var y = 0; y < scaled.Height; y++)
            {
                var row = y * data.Stride;
                for (var x = 0; x < scaled.Width; x++)
                {
                    var i = row + x * 3;
                    var v = (byte)(lum[li++] > effThreshold ? 255 : 0);
                    buffer[i] = v;
                    buffer[i + 1] = v;
                    buffer[i + 2] = v;
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, byteCount);
        }
        finally
        {
            scaled.UnlockBits(data);
        }

        return scaled;
    }

    /// <summary>
    /// Авто-порог по методу Оцу: ищет уровень яркости, максимизирующий межклассовую
    /// дисперсию (оптимальное разделение «фон/текст» по гистограмме кадра). Адаптируется
    /// к яркости конкретной панели, поэтому надёжнее одного фиксированного порога.
    /// </summary>
    internal static int OtsuThreshold(int[] hist, int total)
    {
        long sumAll = 0;
        for (var i = 0; i < 256; i++)
        {
            sumAll += (long)i * hist[i];
        }

        long sumB = 0;
        int wB = 0;
        double maxVar = -1;
        var threshold = 127;
        for (var i = 0; i < 256; i++)
        {
            wB += hist[i];
            if (wB == 0)
            {
                continue;
            }

            var wF = total - wB;
            if (wF == 0)
            {
                break;
            }

            sumB += (long)i * hist[i];
            var mB = (double)sumB / wB;
            var mF = (double)(sumAll - sumB) / wF;
            var between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > maxVar)
            {
                maxVar = between;
                threshold = i;
            }
        }

        return threshold;
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
