using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Infrastructure;

public static class InputSimulator
{
    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const uint MAPVK_VK_TO_VSC = 0x0000;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private static readonly HashSet<MouseButtons> _pressedButtons = new();
    private static int _lastRecordedMouseX;
    private static int _lastRecordedMouseY;
    private static bool _hasRecordedMousePosition;

    public static void ResetState()
    {
        _pressedButtons.Clear();
        _lastRecordedMouseX = 0;
        _lastRecordedMouseY = 0;
        _hasRecordedMousePosition = false;
    }

    public static void Play(MacroEvent macroEvent)
    {
        switch (macroEvent.EventType)
        {
            case MacroEventType.KeyDown:
                SendKeyboard(macroEvent, false);
                break;
            case MacroEventType.KeyUp:
                SendKeyboard(macroEvent, true);
                break;
            case MacroEventType.MouseDown:
                SendMouseButton(macroEvent, false);
                break;
            case MacroEventType.MouseUp:
                SendMouseButton(macroEvent, true);
                break;
            case MacroEventType.MouseMove:
                MoveMouse(macroEvent);
                break;
            case MacroEventType.MouseWheel:
                ScrollWheel(macroEvent.MouseDelta);
                break;
        }
    }

    private static void SendKeyboard(MacroEvent macroEvent, bool isKeyUp)
    {
        var scanCode = macroEvent.ScanCode;
        if (scanCode == 0 && macroEvent.KeyCode != 0)
        {
            scanCode = (int)NativeMethods.MapVirtualKey((uint)macroEvent.KeyCode, MAPVK_VK_TO_VSC);
        }

        if (scanCode == 0)
        {
            return;
        }

        var flags = KEYEVENTF_SCANCODE;
        if (ShouldUseExtendedKey(macroEvent))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        if (isKeyUp)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        var input = new NativeInput
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wVk = 0,
                    wScan = (ushort)scanCode,
                    dwFlags = flags
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void MoveMouse(MacroEvent macroEvent)
    {
        if (!_hasRecordedMousePosition)
        {
            UpdateRecordedMousePosition(macroEvent.MouseX, macroEvent.MouseY);
            MoveMouseAbsolute(macroEvent.MouseX, macroEvent.MouseY);
            return;
        }

        var previousX = _lastRecordedMouseX;
        var previousY = _lastRecordedMouseY;

        if (_pressedButtons.Count == 0)
        {
            MoveMouseAbsolute(macroEvent.MouseX, macroEvent.MouseY);
        }
        else
        {
            var deltaX = macroEvent.MouseX - previousX;
            var deltaY = macroEvent.MouseY - previousY;
            if (deltaX != 0 || deltaY != 0)
            {
                MoveMouseRelative(deltaX, deltaY);
            }
        }

        UpdateRecordedMousePosition(macroEvent.MouseX, macroEvent.MouseY);
    }

    private static void MoveMouseAbsolute(int x, int y)
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

    private static void MoveMouseRelative(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        var input = new NativeInput
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = deltaX,
                    dy = deltaY,
                    dwFlags = MOUSEEVENTF_MOVE
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void SendMouseButton(MacroEvent macroEvent, bool isRelease)
    {
        var button = macroEvent.MouseButton;
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

        UpdateRecordedMousePosition(macroEvent.MouseX, macroEvent.MouseY);

        if (isRelease)
        {
            _pressedButtons.Remove(button);
        }
        else
        {
            _pressedButtons.Add(button);
        }

        var (absX, absY) = NormalizeAbsolute(macroEvent.MouseX, macroEvent.MouseY);

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

    private static void UpdateRecordedMousePosition(int x, int y)
    {
        _lastRecordedMouseX = x;
        _lastRecordedMouseY = y;
        _hasRecordedMousePosition = true;
    }

    private static bool ShouldUseExtendedKey(MacroEvent macroEvent)
    {
        if (macroEvent.IsExtendedKey)
        {
            return true;
        }

        if (macroEvent.KeyCode == 0)
        {
            return false;
        }

        var key = (Keys)macroEvent.KeyCode;
        return key is Keys.Right
            or Keys.Left
            or Keys.Up
            or Keys.Down
            or Keys.Insert
            or Keys.Delete
            or Keys.Home
            or Keys.End
            or Keys.PageUp
            or Keys.PageDown
            or Keys.NumLock
            or Keys.Divide
            or Keys.RControlKey
            or Keys.RMenu
            or Keys.BrowserBack
            or Keys.BrowserForward
            or Keys.BrowserRefresh
            or Keys.BrowserStop
            or Keys.BrowserSearch
            or Keys.BrowserFavorites
            or Keys.BrowserHome
            or Keys.VolumeMute
            or Keys.VolumeDown
            or Keys.VolumeUp
            or Keys.MediaNextTrack
            or Keys.MediaPreviousTrack
            or Keys.MediaStop
            or Keys.MediaPlayPause
            or Keys.LaunchMail
            or Keys.LaunchMediaSelect
            or Keys.LaunchApplication1
            or Keys.LaunchApplication2
            or Keys.PrintScreen
            or Keys.Apps;
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

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);
    }
}
