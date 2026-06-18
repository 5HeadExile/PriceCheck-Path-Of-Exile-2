using System.Drawing;

namespace PriceCheckPoe2.Ocr;

/// <summary>
/// Распознанная строка текста и её прямоугольник в координатах захваченной
/// области (в пикселях исходного, не апскейленного кадра).
/// </summary>
public sealed record OcrLine(string Text, Rectangle Bounds);
