using System.Diagnostics;
using System.Linq;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Services;

public class RecordingService : IDisposable
{
    private readonly HookManager _hookManager;
    private readonly List<MacroEvent> _buffer = new();
    private readonly Stopwatch _stopwatch = new();
    private int _lastTimestamp;

    public RecordingService()
    {
        _hookManager = new HookManager();
        _hookManager.InputCaptured += OnHookInput;
    }

    public bool IsRecording { get; private set; }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        _buffer.Clear();
        _stopwatch.Restart();
        _lastTimestamp = 0;
        _hookManager.Start();
        IsRecording = true;
    }

    public IReadOnlyList<MacroEvent> Stop()
    {
        if (!IsRecording)
        {
            return Array.Empty<MacroEvent>();
        }

        _hookManager.Stop();
        _stopwatch.Stop();
        IsRecording = false;
        return _buffer.Select(CloneEvent).ToList();
    }

    private void OnHookInput(object? sender, HookManager.HookEventArgs e)
    {
        if (!IsRecording)
        {
            return;
        }

        var timestamp = (int)_stopwatch.ElapsedMilliseconds;
        var delay = timestamp - _lastTimestamp;
        _lastTimestamp = timestamp;

        var macroEvent = new MacroEvent
        {
            EventType = e.EventType,
            Delay = delay < 0 ? 0 : delay,
            KeyCode = e.KeyCode,
            ScanCode = e.ScanCode,
            IsExtendedKey = e.IsExtendedKey,
            MouseButton = e.MouseButton,
            MouseX = e.MouseX,
            MouseY = e.MouseY,
            MouseDelta = e.MouseDelta
        };

        _buffer.Add(macroEvent);
    }

    private static MacroEvent CloneEvent(MacroEvent source)
    {
        return new MacroEvent
        {
            EventType = source.EventType,
            Delay = source.Delay,
            KeyCode = source.KeyCode,
            ScanCode = source.ScanCode,
            IsExtendedKey = source.IsExtendedKey,
            MouseButton = source.MouseButton,
            MouseX = source.MouseX,
            MouseY = source.MouseY,
            MouseDelta = source.MouseDelta
        };
    }

    public void Dispose()
    {
        _hookManager.InputCaptured -= OnHookInput;
        _hookManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
