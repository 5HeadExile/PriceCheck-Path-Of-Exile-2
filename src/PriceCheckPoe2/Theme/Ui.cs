using System.Drawing;

namespace PriceCheckPoe2.Theme;

/// <summary>
/// Единый коэффициент масштаба UI. Раскладка дизайн-системы задана в базовых px
/// (96 dpi); чтобы окна не были мелкими на мониторах с высоким DPI, умножаем все
/// размеры/координаты/шрифты на <see cref="Scale"/>. Значение берём из системного
/// DPI один раз на старте + множитель из конфига (ручная подгонка размера).
/// </summary>
public static class Ui
{
    /// <summary>Итоговый множитель (DPI × пользовательский). 1.0 = базовый размер.</summary>
    public static double Scale { get; private set; } = 1.0;

    /// <summary>
    /// Инициализация из системного DPI. <paramref name="userScale"/> — доп.
    /// множитель из настроек (напр. 1.1, чтобы было крупнее). Минимум — DPI.
    /// </summary>
    public static void Init(double userScale = 1.0)
    {
        double dpiScale = 1.0;
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            dpiScale = g.DpiX / 96.0;
        }
        catch
        {
            // нет графики — остаёмся на 1.0
        }

        Scale = Math.Clamp(dpiScale * Math.Clamp(userScale, 0.8, 2.0), 1.0, 3.0);
    }

    /// <summary>Масштабировать число и округлить до int (для координат/размеров).</summary>
    public static int S(double v) => (int)Math.Round(v * Scale);

    /// <summary>Масштабировать число как float (для размеров шрифта/толщин).</summary>
    public static float Sf(double v) => (float)(v * Scale);
}
