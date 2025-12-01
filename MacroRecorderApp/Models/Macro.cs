namespace MacroRecorderApp.Models;

public class Macro
{
    public string Name { get; set; } = string.Empty;

    public List<MacroEvent> Events { get; set; } = new();
}
