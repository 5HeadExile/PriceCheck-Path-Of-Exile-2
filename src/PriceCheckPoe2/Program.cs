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
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
