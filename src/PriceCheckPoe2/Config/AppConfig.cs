using Newtonsoft.Json;

namespace PriceCheckPoe2.Config;

/// <summary>
/// Профиль калибровки под конкретное разрешение/раскладку экрана.
/// </summary>
public sealed class CalibrationProfile
{
    public string Name { get; set; } = "default";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Настройки приложения. Сериализуются в JSON рядом с exe.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Лига PoE2, по которой берём цены с poe.ninja.</summary>
    public string League { get; set; } = "Runes of Aldur";

    /// <summary>
    /// Базовый URL экономики poe.ninja для PoE2. Вынесен в конфиг, т.к. точный
    /// путь PoE2 не задокументирован и может меняться между лигами — проверять
    /// через DevTools браузера (Network) на poe.ninja/poe2/economy.
    /// </summary>
    public string PriceApiBaseUrl { get; set; } =
        "https://poe.ninja/poe2/api/economy/currencyexchange/overview";

    /// <summary>Имена overview-категорий, которые тянем (параметр overviewName).</summary>
    public List<string> PriceOverviews { get; set; } = new()
    {
        "Currency", "Fragments", "Runes", "Essences",
    };

    /// <summary>Период обновления цен, минуты.</summary>
    public int PriceRefreshMinutes { get; set; } = 30;

    /// <summary>Хоткей открытия игрового меню (имя клавиши SharpHook KeyCode).</summary>
    public string MenuHotkey { get; set; } = "VcF2";

    /// <summary>Хоткей рекалибровки области.</summary>
    public string RecalibrateHotkey { get; set; } = "VcF4";

    /// <summary>Хоткей debug-боксов.</summary>
    public string DebugHotkey { get; set; } = "VcF3";

    /// <summary>Прозрачность затемняющего слоя меню (0..1).</summary>
    public double MenuDimOpacity { get; set; } = 0.55;

    /// <summary>Прозрачность оверлея цен (0..1).</summary>
    public double PriceOverlayOpacity { get; set; } = 0.9;

    public List<CalibrationProfile> Profiles { get; set; } = new();

    public string? ActiveProfile { get; set; }

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                if (cfg is not null)
                {
                    return cfg;
                }
            }
        }
        catch
        {
            // Повреждённый конфиг — стартуем с дефолтов, чтобы не падать.
        }

        return new AppConfig();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }
}
