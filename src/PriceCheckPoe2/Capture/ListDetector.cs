using System.Drawing;

namespace PriceCheckPoe2.Capture;

/// <summary>
/// Дёшево определяет, открыта ли панель наград в области, и даёт «сигнатуру»
/// содержимого для детекта изменений. Идея из референса PoeAncientsPriceHelper:
/// UI панели заметно светлее тёмного игрового мира. Семплим сетку пикселей в
/// правой части (там текст наград; левая колонка с иконками тёмная и занижает
/// среднее). Сигнатура — грубая сумма яркостей по всей области: меняется, когда
/// меняется содержимое (другой пилон / другой список).
/// </summary>
public sealed class ListDetector
{
    private const double LeftFraction = 0.40;
    private const double RightFraction = 0.98;
    private static readonly double[] RowFractions = { 0.20, 0.35, 0.50, 0.65, 0.80 };
    private const int Cols = 12;

    private const int SignatureColumns = 10;
    private const int SignatureRows = 16;

    /// <summary>Возвращает яркость правой части и сигнатуру содержимого.</summary>
    public (int Brightness, long Signature) Sample(Bitmap bmp)
    {
        return (Brightness(bmp), Signature(bmp));
    }

    private static int Brightness(Bitmap bmp)
    {
        var x0 = (int)(bmp.Width * LeftFraction);
        var x1 = (int)(bmp.Width * RightFraction);
        var span = Math.Max(1, x1 - x0);

        long sum = 0;
        var count = 0;
        foreach (var yf in RowFractions)
        {
            var cy = Math.Clamp((int)(bmp.Height * yf), 0, bmp.Height - 1);
            for (var i = 0; i < Cols; i++)
            {
                var cx = Math.Clamp(x0 + (int)((i + 0.5) * span / Cols), 0, bmp.Width - 1);
                var px = bmp.GetPixel(cx, cy);
                sum += (px.R + px.G + px.B) / 3;
                count++;
            }
        }

        return (int)(sum / Math.Max(1, count));
    }

    private static long Signature(Bitmap bmp)
    {
        long sig = 0;
        for (var j = 0; j < SignatureRows; j++)
        {
            var cy = Math.Clamp((int)((j + 0.5) * bmp.Height / SignatureRows), 0, bmp.Height - 1);
            for (var i = 0; i < SignatureColumns; i++)
            {
                var cx = Math.Clamp((int)((i + 0.5) * bmp.Width / SignatureColumns), 0, bmp.Width - 1);
                var px = bmp.GetPixel(cx, cy);
                sig += (px.R + px.G + px.B) / 3;
            }
        }

        return sig;
    }
}
