using System.Diagnostics;
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
                    InputSimulator.ResetState();
                    var playbackTimer = Stopwatch.StartNew();
                    long scheduledTime = 0;

                    foreach (var macroEvent in macro.Events)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        scheduledTime += macroEvent.Delay;
                        await WaitForTargetTimeAsync(playbackTimer, scheduledTime, token);

                        if (token.IsCancellationRequested)
                        {
                            break;
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
                InputSimulator.ResetState();
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

    private static async Task WaitForTargetTimeAsync(Stopwatch stopwatch, long targetMilliseconds, CancellationToken token)
    {
        const int spinThresholdMs = 5;

        while (!token.IsCancellationRequested)
        {
            var remaining = targetMilliseconds - stopwatch.ElapsedMilliseconds;
            if (remaining <= 0)
            {
                return;
            }

            if (remaining > spinThresholdMs)
            {
                var delay = (int)Math.Max(1, remaining - spinThresholdMs);
                await Task.Delay(delay, token);
            }
            else
            {
                SpinWait.SpinUntil(() => stopwatch.ElapsedMilliseconds >= targetMilliseconds || token.IsCancellationRequested);
                return;
            }
        }
    }

    private void StopInternal()
    {
        _cts?.Dispose();
        _cts = null;
        _playbackTask = null;
        PlaybackFinished?.Invoke(this, EventArgs.Empty);
    }
}
