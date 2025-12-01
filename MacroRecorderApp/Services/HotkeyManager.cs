using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MacroRecorderApp.Services;

public class HotkeyManager : IDisposable
{
    private readonly IntPtr _windowHandle;
    private const int HotkeyId = 0x0001;
    private Keys _currentHotkey = Keys.F8;

    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        if (!ApplyHotkey(_currentHotkey))
        {
            throw new InvalidOperationException("Не удалось зарегистрировать глобальный хоткей.");
        }
    }

    public Keys CurrentHotkey => _currentHotkey;

    public event EventHandler? HotkeyPressed;

    public bool UpdateHotkey(Keys key)
    {
        return ApplyHotkey(key);
    }

    public void ProcessMessage(Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == (IntPtr)HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool ApplyHotkey(Keys key)
    {
        var previous = _currentHotkey;
        Unregister();
        var modifiers = GetModifiers(key);
        var keyCode = GetKeyCode(key);

        if (!NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, (uint)modifiers, keyCode))
        {
            if (previous != Keys.None && previous != key)
            {
                NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, (uint)GetModifiers(previous), GetKeyCode(previous));
            }

            return false;
        }

        _currentHotkey = key;
        return true;
    }

    public void Unregister()
    {
        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    [Flags]
    private enum HotkeyModifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    private static HotkeyModifiers GetModifiers(Keys key)
    {
        HotkeyModifiers modifiers = HotkeyModifiers.None;

        if (key.HasFlag(Keys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (key.HasFlag(Keys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (key.HasFlag(Keys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (key.HasFlag(Keys.LWin) || key.HasFlag(Keys.RWin))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return modifiers;
    }

    private static uint GetKeyCode(Keys key)
    {
        var keyCode = (uint)(key & Keys.KeyCode);
        if (keyCode == 0)
        {
            keyCode = (uint)Keys.F8;
        }

        return keyCode;
    }

    private static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
