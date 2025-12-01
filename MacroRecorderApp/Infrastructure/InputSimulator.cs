using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Infrastructure;

public static class InputSimulator
{
    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    public static void Play(MacroEvent macroEvent)
    {
        switch (macroEvent.EventType)
        {
            case MacroEventType.KeyDown:
                SendKeyboard(macroEvent.KeyCode, false);
                break;
            case MacroEventType.KeyUp:
                SendKeyboard(macroEvent.KeyCode, true);
                break;
            case MacroEventType.MouseDown:
                SendMouseButton(macroEvent.MouseButton, false, macroEvent.MouseX, macroEvent.MouseY);
                break;
            case MacroEventType.MouseUp:
                SendMouseButton(macroEvent.MouseButton, true, macroEvent.MouseX, macroEvent.MouseY);
                break;
            case MacroEventType.MouseMove:
                MoveMouse(macroEvent.MouseX, macroEvent.MouseY);
                break;
            case MacroEventType.MouseWheel:
                ScrollWheel(macroEvent.MouseDelta);
                break;
        }
    }

    private static void SendKeyboard(int keyCode, bool isKeyUp)
    {
        var input = new NativeInput
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wVk = (ushort)keyCode,
                    dwFlags = isKeyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void MoveMouse(int x, int y)
    {
        var (absX, absY) = NormalizeAbsolute(x, y);
        var input = new NativeInput
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void SendMouseButton(MouseButtons button, bool isRelease, int x, int y)
    {
        var flag = button switch
        {
            MouseButtons.Left => isRelease ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN,
            MouseButtons.Right => isRelease ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN,
            MouseButtons.Middle => isRelease ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_MIDDLEDOWN,
            _ => 0u
        };

        if (flag == 0u)
        {
            return;
        }

        var (absX, absY) = NormalizeAbsolute(x, y);

        var input = new NativeInput
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = flag | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void ScrollWheel(int delta)
    {
        var input = new NativeInput
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    mouseData = unchecked((uint)delta),
                    dwFlags = MOUSEEVENTF_WHEEL
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static (int, int) NormalizeAbsolute(int x, int y)
    {
        var screenWidth = NativeMethods.GetSystemMetrics(0);
        var screenHeight = NativeMethods.GetSystemMetrics(1);

        int Normalize(int value, int size)
        {
            var clipped = Math.Max(0, Math.Min(size - 1, value));
            return (int)Math.Round(clipped * 65535.0 / Math.Max(1, size - 1));
        }

        return (Normalize(x, screenWidth), Normalize(y, screenHeight));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput mi;
        [FieldOffset(0)]
        public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, NativeInput[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);
    }
}
