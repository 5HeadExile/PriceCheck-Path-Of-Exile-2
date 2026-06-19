using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PriceCheckPoe2.ItemCheck.Input;

/// <summary>
/// Эмулирует Ctrl+C (через SendInput), чтобы игра положила текст наведённого
/// предмета в буфер обмена, и читает буфер с ретраями (буфер может быть временно
/// залочен другим процессом).
/// <para>Доступ к буферу в WinForms требует STA-потока — вызывать из UI-потока
/// приложения.</para>
/// </summary>
public sealed class ClipboardItemReader
{
    private const int VK_CONTROL = 0x11;
    private const int VK_C = 0x43;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private readonly int _settleMs;
    private readonly int _retries;

    public ClipboardItemReader(int settleMs = 120, int retries = 8)
    {
        _settleMs = settleMs;
        _retries = retries;
    }

    /// <summary>
    /// Шлёт Ctrl+C и возвращает текст из буфера, либо <c>null</c>, если за отведённые
    /// попытки текст не появился (например, курсор не на предмете).
    /// </summary>
    public string? CopyAndRead()
    {
        var before = SafeGetText();
        SendCopy();

        // Ждём, пока игра обновит буфер; считаем успехом непустой текст,
        // отличающийся от прежнего (или просто непустой, если прежний был пуст).
        for (var i = 0; i < _retries; i++)
        {
            Thread.Sleep(_settleMs / _retries + 5);
            var now = SafeGetText();
            if (!string.IsNullOrEmpty(now) && (before is null || now != before || now.Contains("Item Class:")))
            {
                return now;
            }
        }

        var final = SafeGetText();
        return string.IsNullOrEmpty(final) ? null : final;
    }

    public static void SendCopy()
    {
        var inputs = new[]
        {
            KeyInput(VK_CONTROL, false),
            KeyInput(VK_C, false),
            KeyInput(VK_C, true),
            KeyInput(VK_CONTROL, true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static string? SafeGetText()
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (ExternalException)
            {
                Thread.Sleep(15); // буфер занят — короткая пауза и повтор
            }
        }

        return null;
    }

    private static INPUT KeyInput(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
            },
        },
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
