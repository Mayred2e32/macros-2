using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Services;

public class HookManager : IDisposable
{
    public event EventHandler<HookEventArgs>? InputCaptured;

    private IntPtr _keyboardHookId;
    private IntPtr _mouseHookId;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private NativeMethods.LowLevelMouseProc? _mouseProc;

    public bool IsRunning => _keyboardHookId != IntPtr.Zero && _mouseHookId != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);

        _keyboardHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc!, moduleHandle, 0);
        _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc!, moduleHandle, 0);

        if (_keyboardHookId == IntPtr.Zero || _mouseHookId == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to register global hooks");
        }
    }

    public void Stop()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }

        if (_mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && lParam != IntPtr.Zero)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var type = DetermineKeyboardEventType(wParam);
            if (type.HasValue)
            {
                InputCaptured?.Invoke(this, new HookEventArgs
                {
                    EventType = type.Value,
                    KeyCode = info.vkCode,
                    ScanCode = info.scanCode,
                    IsExtendedKey = (info.flags & NativeMethods.LLKHF_EXTENDED) != 0,
                    MouseButton = MouseButtons.None,
                    MouseX = 0,
                    MouseY = 0,
                    MouseDelta = 0
                });
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private static MacroEventType? DetermineKeyboardEventType(IntPtr wParam)
    {
        var message = wParam.ToInt32();
        return message switch
        {
            NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN => MacroEventType.KeyDown,
            NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP => MacroEventType.KeyUp,
            _ => null
        };
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && lParam != IntPtr.Zero)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
            var args = CreateMouseEvent(wParam, info);
            if (args != null)
            {
                InputCaptured?.Invoke(this, args);
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private static HookEventArgs? CreateMouseEvent(IntPtr wParam, NativeMethods.MsLlHookStruct info)
    {
        switch ((int)wParam)
        {
            case NativeMethods.WM_MOUSEMOVE:
                return new HookEventArgs
                {
                    EventType = MacroEventType.MouseMove,
                    MouseButton = MouseButtons.None,
                    MouseX = info.pt.x,
                    MouseY = info.pt.y
                };
            case NativeMethods.WM_LBUTTONDOWN:
                return CreateMouseButtonEvent(MacroEventType.MouseDown, MouseButtons.Left, info);
            case NativeMethods.WM_LBUTTONUP:
                return CreateMouseButtonEvent(MacroEventType.MouseUp, MouseButtons.Left, info);
            case NativeMethods.WM_RBUTTONDOWN:
                return CreateMouseButtonEvent(MacroEventType.MouseDown, MouseButtons.Right, info);
            case NativeMethods.WM_RBUTTONUP:
                return CreateMouseButtonEvent(MacroEventType.MouseUp, MouseButtons.Right, info);
            case NativeMethods.WM_MBUTTONDOWN:
                return CreateMouseButtonEvent(MacroEventType.MouseDown, MouseButtons.Middle, info);
            case NativeMethods.WM_MBUTTONUP:
                return CreateMouseButtonEvent(MacroEventType.MouseUp, MouseButtons.Middle, info);
            case NativeMethods.WM_MOUSEWHEEL:
                var delta = (short)((info.mouseData >> 16) & 0xFFFF);
                return new HookEventArgs
                {
                    EventType = MacroEventType.MouseWheel,
                    MouseButton = MouseButtons.None,
                    MouseDelta = delta,
                    MouseX = info.pt.x,
                    MouseY = info.pt.y
                };
            default:
                return null;
        }
    }

    private static HookEventArgs CreateMouseButtonEvent(MacroEventType type, MouseButtons button, NativeMethods.MsLlHookStruct info)
    {
        return new HookEventArgs
        {
            EventType = type,
            MouseButton = button,
            MouseX = info.pt.x,
            MouseY = info.pt.y
        };
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    public class HookEventArgs : EventArgs
    {
        public MacroEventType EventType { get; set; }
        public int KeyCode { get; set; }
        public int ScanCode { get; set; }
        public bool IsExtendedKey { get; set; }
        public MouseButtons MouseButton { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public int MouseDelta { get; set; }
    }

    private static class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int LLKHF_EXTENDED = 0x01;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_MOUSEWHEEL = 0x020A;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KbdLlHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MsLlHookStruct
        {
            public Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
