using System;
using System.Threading;
using System.Threading.Tasks;

namespace PLLauncher.Services;

public class PomodoroService : IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public event EventHandler<PomodoroEventArgs>? TimerTick;
    public event EventHandler<PomodoroPhase>? PhaseChanged;

    public PomodoroPhase CurrentPhase { get; private set; } = PomodoroPhase.Work;
    public TimeSpan Remaining { get; private set; }
    public int WorkMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int SessionsCompleted { get; private set; }

    public bool IsRunning => _isRunning;

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();

        if (Remaining == TimeSpan.Zero)
            Remaining = TimeSpan.FromMinutes(CurrentPhase == PomodoroPhase.Work ? WorkMinutes : BreakMinutes);

        _ = RunTimerAsync(_cts.Token);
    }

    public void Pause()
    {
        _isRunning = false;
        _cts?.Cancel();
    }

    public void Reset()
    {
        _isRunning = false;
        _cts?.Cancel();
        CurrentPhase = PomodoroPhase.Work;
        Remaining = TimeSpan.FromMinutes(WorkMinutes);
        TimerTick?.Invoke(this, new PomodoroEventArgs { Remaining = Remaining, Phase = CurrentPhase });
    }

    public void Skip()
    {
        _isRunning = false;
        _cts?.Cancel();
        SwitchPhase();
    }

    public void UpdateWorkMinutes(int minutes)
    {
        WorkMinutes = minutes;
        if (!_isRunning && CurrentPhase == PomodoroPhase.Work)
        {
            Remaining = TimeSpan.FromMinutes(minutes);
            TimerTick?.Invoke(this, new PomodoroEventArgs { Remaining = Remaining, Phase = CurrentPhase });
        }
    }

    public void UpdateBreakMinutes(int minutes)
    {
        BreakMinutes = minutes;
        if (!_isRunning && CurrentPhase == PomodoroPhase.Break)
        {
            Remaining = TimeSpan.FromMinutes(minutes);
            TimerTick?.Invoke(this, new PomodoroEventArgs { Remaining = Remaining, Phase = CurrentPhase });
        }
    }

    private async Task RunTimerAsync(CancellationToken ct)
    {
        try
        {
            while (_isRunning && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                if (ct.IsCancellationRequested) break;

                Remaining = Remaining.Subtract(TimeSpan.FromSeconds(1));
                if (Remaining <= TimeSpan.Zero)
                {
                    Remaining = TimeSpan.Zero;
                    _isRunning = false;
                    SwitchPhase();
                    return;
                }

                TimerTick?.Invoke(this, new PomodoroEventArgs { Remaining = Remaining, Phase = CurrentPhase });
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void SwitchPhase()
    {
        if (CurrentPhase == PomodoroPhase.Work)
        {
            SessionsCompleted++;
            CurrentPhase = PomodoroPhase.Break;
            Remaining = TimeSpan.FromMinutes(BreakMinutes);
        }
        else
        {
            CurrentPhase = PomodoroPhase.Work;
            Remaining = TimeSpan.FromMinutes(WorkMinutes);
        }

        PhaseChanged?.Invoke(this, CurrentPhase);
        TimerTick?.Invoke(this, new PomodoroEventArgs { Remaining = Remaining, Phase = CurrentPhase });
    }

    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); GC.SuppressFinalize(this); }
}

public enum PomodoroPhase { Work, Break }

public class PomodoroEventArgs : EventArgs
{
    public TimeSpan Remaining { get; set; }
    public PomodoroPhase Phase { get; set; }
}
