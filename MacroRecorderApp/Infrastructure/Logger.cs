namespace MacroRecorderApp.Infrastructure;

public class Logger
{
    private readonly object _sync = new();
    private readonly Action<string>? _uiSink;
    private readonly string _logFilePath;

    public Logger(string logFilePath, Action<string>? uiSink = null)
    {
        _uiSink = uiSink;
        _logFilePath = logFilePath;
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";
        _uiSink?.Invoke(line);
        try
        {
            lock (_sync)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // ignore
        }
    }
}
