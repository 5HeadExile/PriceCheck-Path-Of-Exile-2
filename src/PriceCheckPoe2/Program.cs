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

        Application.Run(new TrayApplicationContext());
    }
}
