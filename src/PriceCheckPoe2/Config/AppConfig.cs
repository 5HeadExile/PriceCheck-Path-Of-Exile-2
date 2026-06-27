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
    /// Базовый URL экономики poe.ninja для PoE2 (currency exchange). Проверен на
    /// лиге Runes of Aldur через DevTools: возвращает { core, lines, items }.
    /// Запрос: {base}?league={League}&type={overview}.
    /// </summary>
    public string PriceApiBaseUrl { get; set; } =
        "https://poe.ninja/poe2/api/economy/exchange/current/overview";

    /// <summary>
    /// Категории (параметр type), которые тянем с poe.ninja. Перечислены все
    /// стакаемые награды актуальной лиги (Runes of Aldur), которые выпадают из
    /// пилонов; проверено через DevTools, что у каждой есть данные. Уники/omens и
    /// т.п. идут другим механизмом (пустые здесь) и пилонами не выдаются.
    /// <para>Это встроенный каталог, а не пользовательская настройка —
    /// <see cref="JsonIgnoreAttribute"/> гарантирует, что список всегда берётся
    /// из кода и старый config.json не «заморозит» урезанный набор.</para>
    /// </summary>
    [JsonIgnore]
    public List<string> PriceOverviews { get; set; } = new()
    {
        "Currency",
        "Fragments",
        "Runes",
        "Essences",
        "SoulCores",
        "UncutGems",
        "LineageSupportGems",
        "Idols",
        "Expedition",
        "Verisium",
    };

    /// <summary>Период обновления цен, минуты.</summary>
    public int PriceRefreshMinutes { get; set; } = 30;

    /// <summary>Хоткей открытия игрового меню (имя клавиши SharpHook KeyCode).</summary>
    public string MenuHotkey { get; set; } = "VcF2";

    /// <summary>Хоткей рекалибровки области.</summary>
    public string RecalibrateHotkey { get; set; } = "VcF4";

    /// <summary>Хоткей debug-боксов.</summary>
    public string DebugHotkey { get; set; } = "VcF3";

    /// <summary>
    /// Хоткей «режима скриншота»: замораживает оверлей и временно разрешает захват
    /// окна (снимает WDA_EXCLUDEFROMCAPTURE), чтобы плашки попали в скриншот. Повторное
    /// нажатие возвращает обычный режим (оверлей снова скрыт от собственного захвата).
    /// </summary>
    public string ScreenshotHotkey { get; set; } = "VcF6";

    /// <summary>Прозрачность затемняющего слоя меню (0..1).</summary>
    public double MenuDimOpacity { get; set; } = 0.55;

    /// <summary>Прозрачность оверлея цен (0..1).</summary>
    public double PriceOverlayOpacity { get; set; } = 0.9;

    /// <summary>
    /// Откалиброванные области пилонов. Игрок выбирает один пилон из нескольких,
    /// поэтому регионов может быть несколько — каждый оценивается отдельно, а
    /// лучший подсвечивается.
    /// </summary>
    public List<CalibrationProfile> PylonRegions { get; set; } = new();

    /// <summary>Порог бинаризации для предобработки OCR (0..255). Легаси/одиночный режим.</summary>
    public int OcrThreshold { get; set; } = 110;

    /// <summary>
    /// Набор порогов бинаризации для много-проходного OCR. Полупрозрачная книга PoE2
    /// поверх разного фона даёт панели РАЗНОЙ яркости: тёмные читаются низким порогом,
    /// яркие — высоким. Сканируем каждым порогом и объединяем результат (дедуп
    /// оставляет на каждую строку вариант с ценой). 0 в списке = авто-порог Оцу.
    /// <para><see cref="ObjectCreationHandling.Replace"/> обязателен: иначе Newtonsoft
    /// при десериализации ДОБАВЛЯЕТ значения из JSON к дефолтному списку, и список
    /// растёт на каждый Load/Save (→ лишние проходы OCR и тормоза).</para>
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<int> OcrThresholds { get; set; } = new() { 60, 105, 150 };

    /// <summary>
    /// Пользовательский множитель размера UI поверх системного DPI (см. <see cref="Theme.Ui"/>).
    /// 1.0 — без изменений. Полезно на мониторах с масштабом 100%, где плашки/меню
    /// кажутся мелкими: например 1.25 вернёт размер как при системных 125%.
    /// Итоговый масштаб = DPI × UiScale (с клампом).
    /// </summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>
    /// Ручная подстройка размера ценников-плашек поверх авто-масштаба. Плашки сами
    /// масштабируются от высоты текста награды (любое разрешение — из коробки); этот
    /// множитель нужен лишь если хочется крупнее/мельче. 1.0 = чистый авто-размер.
    /// </summary>
    public double PriceChipScale { get; set; } = 1.0;

    /// <summary>Сохранять предобработанные кадры OCR рядом с exe (для отладки).</summary>
    public bool SaveOcrDebugImages { get; set; }

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
        try
        {
            // Атомарно: пишем во временный файл и подменяем — обрыв записи не оставит
            // битый config.json. Вызывается из UI-потока, поэтому IO-ошибку
            // (файл занят и т.п.) глушим, чтобы не уронить приложение.
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
