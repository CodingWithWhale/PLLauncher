using System;
using System.Threading;
using System.Threading.Tasks;

namespace PLLauncher.Services;

public class HealthReminderService : IDisposable
{
    private readonly NotificationService _notificationService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public bool IsEnabled { get; private set; }
    public int IntervalMinutes { get; set; } = 60; // Default: remind every 60 minutes

    private static readonly string[] Messages = new[]
    {
        "You haven't moved much lately. Time to stretch!",
        "Take a quick break and move around a bit!",
        "Hey, your body needs some movement. Stand up and stretch!",
        "Sitting too long? Take a short walk!",
        "Time for a mini-break! Stretch your arms and back.",
        "Your eyes need a rest too. Look away from the screen for 20 seconds.",
        "Roll your shoulders and stretch your neck. You'll feel better!",
        "A quick stretch now can prevent stiffness later. Get moving!"
    };

    public HealthReminderService(NotificationService ns)
    {
        _notificationService = ns;
    }

    public void Enable(int intervalMinutes)
    {
        if (_isRunning) Disable();
        IsEnabled = true;
        IntervalMinutes = intervalMinutes;
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _ = RunReminderLoopAsync(_cts.Token);
    }

    public void Disable()
    {
        IsEnabled = false;
        _isRunning = false;
        _cts?.Cancel();
    }

    private async Task RunReminderLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_isRunning && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), ct);
                if (ct.IsCancellationRequested) break;

                var msg = Messages[new Random().Next(Messages.Length)];
                _notificationService.ShowNotification("Health Reminder", msg);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    public void Dispose() { Disable(); GC.SuppressFinalize(this); }
}
