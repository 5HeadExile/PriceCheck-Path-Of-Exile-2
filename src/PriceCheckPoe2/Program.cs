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
        // Масштаб UI под DPI монитора (иначе на 125/150% окна мелкие).
        Theme.Ui.Init();

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

        // Технический рендер образцов ценников на тёмном фоне (проверка дизайна):
        // --shot-chips <путь>.
        if (args.Length >= 2 && args[0] == "--shot-chips")
        {
            using var bmp = new System.Drawing.Bitmap(360, 320);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(28, 24, 20)); // имитация тёмной игры
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                var samples = new (Overlay.PriceChip.Model M, string Caption)[]
                {
                    (new(false, false, 0.05, "0.05 ex", null), "T1 тривиально"),
                    (new(false, false, 0.55, "0.55 ex", null), "T2 дёшево"),
                    (new(false, false, 2.18, "2.18 ex", null), "T3 заметно"),
                    (new(false, false, 9.16, "9.16 ex", null), "T4 дорого"),
                    (new(false, false, 17.46, "1.73 div", null), "T5 топ (div)"),
                    (new(false, false, 3.85, "3.85 ex", "0.55/шт"), "стак"),
                    (new(true, false, 0, "?", null), "нет цены"),
                    (new(false, true, 22.3, "2.23 div", null), "лучшая"),
                };

                float y = 16;
                using var cap = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
                foreach (var (m, caption) in samples)
                {
                    var w = Overlay.PriceChip.MeasureWidth(g, m);
                    Overlay.PriceChip.Draw(g, new System.Drawing.RectangleF(16, y, w, Overlay.PriceChip.Height), m);
                    System.Windows.Forms.TextRenderer.DrawText(g, caption, cap,
                        new System.Drawing.Point(190, (int)y + 4), System.Drawing.Color.FromArgb(150, 150, 150));
                    y += 36;
                }
            }

            bmp.Save(args[1]);
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
