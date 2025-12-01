using System.Threading;
using System.Threading.Tasks;
using MacroRecorderApp.Infrastructure;
using MacroRecorderApp.Models;

namespace MacroRecorderApp.Services;

public class PlaybackService
{
    private readonly Logger _logger;
    private Task? _playbackTask;
    private CancellationTokenSource? _cts;

    public PlaybackService(Logger logger)
    {
        _logger = logger;
    }

    public bool IsPlaying => _cts is not null;

    public event EventHandler? PlaybackFinished;

    public void Start(Macro macro, bool loop)
    {
        if (IsPlaying)
        {
            return;
        }

        if (macro.Events.Count == 0)
        {
            _logger.Info("Нет событий для воспроизведения.");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _playbackTask = Task.Run(async () =>
        {
            try
            {
                _logger.Info("Старт воспроизведения через 2 секунды...");
                await Task.Delay(TimeSpan.FromSeconds(2), token);

                do
                {
                    foreach (var macroEvent in macro.Events)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (macroEvent.Delay > 0)
                        {
                            await Task.Delay(macroEvent.Delay, token);
                        }

                        InputSimulator.Play(macroEvent);
                    }
                } while (loop && !token.IsCancellationRequested);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка при воспроизведении: {ex.Message}");
            }
            finally
            {
                StopInternal();
            }
        }, token);
    }

    public void Stop()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();
    }

    private void StopInternal()
    {
        _cts?.Dispose();
        _cts = null;
        _playbackTask = null;
        PlaybackFinished?.Invoke(this, EventArgs.Empty);
    }
}
