using System.Drawing;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Capture;

/// <summary>
/// Захват прямоугольной области экрана через GDI (<see cref="Graphics.CopyFromScreen(Point,Point,Size)"/>).
/// </summary>
public static class ScreenCapturer
{
    public static Bitmap Capture(Rectangle region)
    {
        var bmp = new Bitmap(region.Width, region.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.Location, Point.Empty, region.Size);
        return bmp;
    }

    public static Bitmap Capture(CalibrationProfile profile) =>
        Capture(new Rectangle(profile.X, profile.Y, profile.Width, profile.Height));
}
