using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MacroRecorderApp.Infrastructure;
using MacroRecorderApp.Models;
using MacroRecorderApp.Services;

namespace MacroRecorderApp.Forms;

public class MainForm : Form
{
    private readonly RecordingService _recordingService = new();
    private readonly MacroStorage _storage;
    private readonly Logger _logger;
    private readonly PlaybackService _playbackService;
    private readonly List<Macro> _macros = new();
    private readonly Timer _hotkeyTimer = new() { Interval = 4000 };

    private HotkeyManager? _hotkeyManager;

    private Button _recordButton = null!;
    private Button _chooseHotkeyButton = null!;
    private ListBox _macroListBox = null!;
    private CheckBox _loopCheckBox = null!;
    private ListBox _logListBox = null!;
    private Label _hotkeyStatusLabel = null!;

    private bool _isAwaitingHotkey;

    public MainForm()
    {
        KeyPreview = true;
        _storage = new MacroStorage(Log);
        _logger = new Logger(_storage.GetLogFilePath(), Log);
        _playbackService = new PlaybackService(_logger);
        _playbackService.PlaybackFinished += (_, _) => Log("Воспроизведение остановлено.");
        _hotkeyTimer.Tick += (_, _) => StopWaitingForHotkey(true);

        BuildUi();
        LoadMacros();

        Text = "Roblox Macro Recorder";
        FormClosing += (_, _) => Cleanup();
        KeyDown += OnKeyDown;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_hotkeyManager == null)
        {
            _hotkeyManager = new HotkeyManager(Handle);
            _hotkeyManager.HotkeyPressed += (_, _) => HandleHotkeyPress();
            UpdateHotkeyLabel(_hotkeyManager.CurrentHotkey);
            Log($"Глобальный хоткей: {_hotkeyManager.CurrentHotkey}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        _hotkeyManager?.ProcessMessage(m);
        base.WndProc(ref m);
    }

    private void BuildUi()
    {
        Size = new Size(640, 540);
        StartPosition = FormStartPosition.CenterScreen;

        _recordButton = new Button
        {
            Text = "Start",
            Width = 120,
            Height = 40,
            Left = 20,
            Top = 20
        };
        _recordButton.Click += (_, _) => ToggleRecording();
        Controls.Add(_recordButton);

        var macrosLabel = new Label
        {
            Text = "Сохраненные макросы",
            Left = 20,
            Top = 70,
            AutoSize = true
        };
        Controls.Add(macrosLabel);

        _macroListBox = new ListBox
        {
            Left = 20,
            Top = 100,
            Width = 260,
            Height = 220
        };
        Controls.Add(_macroListBox);

        _chooseHotkeyButton = new Button
        {
            Text = "Выбрать хоткей",
            Width = 200,
            Height = 40,
            Left = 320,
            Top = 20
        };
        _chooseHotkeyButton.Click += (_, _) => BeginHotkeySelection();
        Controls.Add(_chooseHotkeyButton);

        _hotkeyStatusLabel = new Label
        {
            Text = "Текущий хоткей: -",
            Left = 320,
            Top = 70,
            AutoSize = true
        };
        Controls.Add(_hotkeyStatusLabel);

        _loopCheckBox = new CheckBox
        {
            Text = "Воспроизводить без конца",
            Left = 320,
            Top = 110,
            Width = 250
        };
        Controls.Add(_loopCheckBox);

        var logLabel = new Label
        {
            Text = "Логи",
            Left = 20,
            Top = 340,
            AutoSize = true
        };
        Controls.Add(logLabel);

        _logListBox = new ListBox
        {
            Left = 20,
            Top = 370,
            Width = 600,
            Height = 140
        };
        Controls.Add(_logListBox);
    }

    private void ToggleRecording()
    {
        if (_recordingService.IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (_playbackService.IsPlaying)
        {
            Log("Нельзя начать запись во время воспроизведения.");
            return;
        }

        try
        {
            _recordingService.Start();
            _recordButton.Text = "Stop";
            Log("Запись начата.");
        }
        catch (Exception ex)
        {
            _recordButton.Text = "Start";
            Log($"Ошибка при запуске записи: {ex.Message}");
        }
    }

    private void StopRecording()
    {
        if (!_recordingService.IsRecording)
        {
            return;
        }

        var events = _recordingService.Stop();
        _recordButton.Text = "Start";
        Log("Запись остановлена.");

        var recordedEvents = events
            .Select(e => new MacroEvent
            {
                EventType = e.EventType,
                Delay = e.Delay,
                KeyCode = e.KeyCode,
                MouseButton = e.MouseButton,
                MouseX = e.MouseX,
                MouseY = e.MouseY,
                MouseDelta = e.MouseDelta
            })
            .ToList();

        if (recordedEvents.Count == 0)
        {
            Log("Пустой макрос не сохранен.");
            return;
        }

        var saved = false;
        string? lastName = null;
        while (!saved)
        {
            using var dialog = new SaveMacroForm(lastName);
            var dialogResult = dialog.ShowDialog(this);
            if (dialogResult != DialogResult.Yes)
            {
                Log("Пользователь отменил сохранение макроса.");
                break;
            }

            lastName = dialog.MacroName;
            if (string.IsNullOrWhiteSpace(lastName))
            {
                MessageBox.Show("Введите имя макроса", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
                continue;
            }

            var macro = new Macro
            {
                Name = lastName,
                Events = recordedEvents
                    .Select(e => new MacroEvent
                    {
                        EventType = e.EventType,
                        Delay = e.Delay,
                        KeyCode = e.KeyCode,
                        MouseButton = e.MouseButton,
                        MouseX = e.MouseX,
                        MouseY = e.MouseY,
                        MouseDelta = e.MouseDelta
                    })
                    .ToList()
            };

            _macros.Add(macro);
            SaveMacros();
            RefreshMacroList();
            Log($"Макрос '{macro.Name}' сохранен ({macro.Events.Count} событий).");
            saved = true;
        }
    }

    private void BeginHotkeySelection()
    {
        _isAwaitingHotkey = true;
        _hotkeyTimer.Stop();
        _hotkeyTimer.Start();
        Log("Ожидание выбора хоткея (4 секунды)...");
    }

    private void StopWaitingForHotkey(bool timedOut)
    {
        if (!_isAwaitingHotkey)
        {
            return;
        }

        _isAwaitingHotkey = false;
        _hotkeyTimer.Stop();
        if (timedOut)
        {
            Log("Выбор хоткея отменен.");
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isAwaitingHotkey)
        {
            return;
        }

        e.Handled = true;
        StopWaitingForHotkey(false);

        var newKey = e.KeyData;
        if (newKey == Keys.None)
        {
            Log("Некорректный хоткей.");
            return;
        }

        var baseKey = newKey & Keys.KeyCode;
        var modifiers = newKey & Keys.Modifiers;
        if (IsModifierKey(baseKey) && modifiers == Keys.None)
        {
            Log("Нужна обычная клавиша, а не только модификатор.");
            return;
        }

        if (_hotkeyManager != null && _hotkeyManager.UpdateHotkey(newKey))
        {
            UpdateHotkeyLabel(newKey);
            Log($"Новый хоткей: {newKey}");
        }
        else
        {
            Log("Не удалось установить хоткей.");
        }
    }

    private void HandleHotkeyPress()
    {
        if (_isAwaitingHotkey)
        {
            return;
        }

        Log("Глобальный хоткей нажат.");

        if (_recordingService.IsRecording)
        {
            Log("Хоткей: остановка записи.");
            StopRecording();
            return;
        }

        if (_playbackService.IsPlaying)
        {
            Log("Хоткей: остановка воспроизведения.");
            _playbackService.Stop();
            return;
        }

        var selectedMacro = GetSelectedMacro();
        if (selectedMacro == null)
        {
            Log("Хоткей: запуск новой записи.");
            StartRecording();
        }
        else
        {
            StartPlayback(selectedMacro);
        }
    }

    private void StartPlayback(Macro macro)
    {
        if (_recordingService.IsRecording)
        {
            Log("Сначала остановите запись.");
            return;
        }

        Log($"Старт воспроизведения макроса '{macro.Name}'.");
        _playbackService.Start(macro, _loopCheckBox.Checked);
    }

    private Macro? GetSelectedMacro()
    {
        var index = _macroListBox.SelectedIndex;
        if (index < 0 || index >= _macros.Count)
        {
            return null;
        }

        return _macros[index];
    }

    private void LoadMacros()
    {
        var items = _storage.LoadMacros();
        _macros.Clear();
        _macros.AddRange(items);
        RefreshMacroList();
        Log(_macros.Count == 0 ? "Макросы не найдены." : $"Загружено макросов: {_macros.Count}.");
    }

    private void SaveMacros()
    {
        _storage.SaveMacros(_macros);
    }

    private void RefreshMacroList()
    {
        var previous = _macroListBox.SelectedItem as string;
        _macroListBox.Items.Clear();
        foreach (var macro in _macros)
        {
            _macroListBox.Items.Add(macro.Name);
        }

        if (!string.IsNullOrEmpty(previous))
        {
            var index = _macroListBox.Items.IndexOf(previous);
            if (index >= 0)
            {
                _macroListBox.SelectedIndex = index;
                return;
            }
        }

        if (_macroListBox.Items.Count > 0)
        {
            _macroListBox.SelectedIndex = 0;
        }
    }

    private void UpdateHotkeyLabel(Keys key)
    {
        _hotkeyStatusLabel.Text = $"Текущий хоткей: {key}";
    }

    private void Cleanup()
    {
        if (_recordingService.IsRecording)
        {
            _recordingService.Stop();
        }

        if (_playbackService.IsPlaying)
        {
            _playbackService.Stop();
        }

        _hotkeyManager?.Dispose();
        _hotkeyManager = null;
        _recordingService.Dispose();
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin;
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }

        _logListBox.Items.Add(message);
        if (_logListBox.Items.Count > 200)
        {
            _logListBox.Items.RemoveAt(0);
        }

        _logListBox.TopIndex = _logListBox.Items.Count - 1;
    }
}
