using System.Text.Json;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Services;

public class MacroStorage
{
    private readonly string _directoryPath;
    private readonly string _macroFilePath;
    private readonly Action<string>? _log;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public MacroStorage(Action<string>? logAction = null)
    {
        _log = logAction;
        _directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacrossApp");
        _macroFilePath = Path.Combine(_directoryPath, "macros.json");
    }

    public List<Macro> LoadMacros()
    {
        try
        {
            if (!Directory.Exists(_directoryPath) || !File.Exists(_macroFilePath))
            {
                return new List<Macro>();
            }

            var json = File.ReadAllText(_macroFilePath);
            var macros = JsonSerializer.Deserialize<List<Macro>>(json, _serializerOptions);
            return macros ?? new List<Macro>();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Ошибка загрузки макросов: {ex.Message}");
            return new List<Macro>();
        }
    }

    public void SaveMacros(IEnumerable<Macro> macros)
    {
        try
        {
            Directory.CreateDirectory(_directoryPath);
            var json = JsonSerializer.Serialize(macros, _serializerOptions);
            File.WriteAllText(_macroFilePath, json);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Ошибка сохранения макросов: {ex.Message}");
        }
    }

    public string GetLogFilePath()
    {
        Directory.CreateDirectory(_directoryPath);
        return Path.Combine(_directoryPath, "app.log");
    }
}
