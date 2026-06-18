using SharpHook;
using SharpHook.Native;

namespace PriceCheckPoe2.Config;

/// <summary>
/// Глобальный перехват клавиш через SharpHook. Хук низкоуровневый, поэтому
/// хоткеи срабатывают даже когда фокус в игре. События поднимаются в UI-поток
/// вызывающей стороной (см. <see cref="TrayApplicationContext"/>).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly SimpleGlobalHook _hook;
    private readonly AppConfig _config;

    public event Action? MenuToggleRequested;
    public event Action? RecalibrateRequested;
    public event Action? DebugToggleRequested;

    public HotkeyManager(AppConfig config)
    {
        _config = config;
        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard);
        _hook.KeyPressed += OnKeyPressed;
    }

    /// <summary>Запускает хук в фоне. Не блокирует вызывающий поток.</summary>
    public void Start() => _hook.RunAsync();

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var key = e.Data.KeyCode;

        if (key == Parse(_config.MenuHotkey))
        {
            MenuToggleRequested?.Invoke();
        }
        else if (key == Parse(_config.RecalibrateHotkey))
        {
            RecalibrateRequested?.Invoke();
        }
        else if (key == Parse(_config.DebugHotkey))
        {
            DebugToggleRequested?.Invoke();
        }
    }

    private static KeyCode Parse(string name) =>
        Enum.TryParse<KeyCode>(name, ignoreCase: true, out var code) ? code : KeyCode.VcUndefined;

    public void Dispose()
    {
        _hook.KeyPressed -= OnKeyPressed;
        _hook.Dispose();
    }
}
