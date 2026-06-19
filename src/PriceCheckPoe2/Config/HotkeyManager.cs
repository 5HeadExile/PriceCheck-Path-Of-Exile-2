using SharpHook;
using SharpHook.Data;

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

    private bool _ctrlDown;

    public event Action? MenuToggleRequested;
    public event Action? RecalibrateRequested;
    public event Action? DebugToggleRequested;
    public event Action? ItemCheckRequested;

    public HotkeyManager(AppConfig config)
    {
        _config = config;
        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <summary>Запускает хук в фоне. Не блокирует вызывающий поток.</summary>
    public void Start() => _hook.RunAsync();

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var key = e.Data.KeyCode;

        if (key is KeyCode.VcLeftControl or KeyCode.VcRightControl)
        {
            _ctrlDown = true;
        }

        // Price-check (фича 2): хоткей с модификатором Ctrl (по умолчанию Ctrl+D).
        if (key == Parse(_config.ItemCheckHotkey) && (!_config.ItemCheckUseCtrl || _ctrlDown))
        {
            ItemCheckRequested?.Invoke();
            return;
        }

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

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl)
        {
            _ctrlDown = false;
        }
    }

    private static KeyCode Parse(string name) =>
        Enum.TryParse<KeyCode>(name, ignoreCase: true, out var code) ? code : KeyCode.VcUndefined;

    public void Dispose()
    {
        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;
        _hook.Dispose();
    }
}
