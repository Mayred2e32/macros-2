using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace MacroRecorderApp.Models;

public class MacroEvent
{
    public MacroEventType EventType { get; set; }

    public int Delay { get; set; }

    public int KeyCode { get; set; }

    public MouseButtons MouseButton { get; set; }

    public int MouseX { get; set; }

    public int MouseY { get; set; }

    public int MouseDelta { get; set; }

    [JsonIgnore]
    public bool IsKeyboardEvent => EventType == MacroEventType.KeyDown || EventType == MacroEventType.KeyUp;

    [JsonIgnore]
    public bool IsMouseMove => EventType == MacroEventType.MouseMove;
}
