using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PLLauncher.Models;

namespace PLLauncher.Services;

public class TimeTrackingService : IDisposable
{
    private readonly List<TimeLimitItem> _timeLimits = new();
    private readonly NotificationService _notificationService;
    private readonly ProcessMonitorService _processMonitor;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public event EventHandler<TimeLimitItem>? LimitReached;
    public event EventHandler<TimeLimitItem>? AppLocked;
    public event EventHandler<TimeLimitItem>? CooldownStarted;
    public event EventHandler<TimeLimitItem>? UsageUpdated;

    public IReadOnlyList<TimeLimitItem> TimeLimits => _timeLimits.AsReadOnly();

    public TimeTrackingService(NotificationService ns, ProcessMonitorService pm)
    { _notificationService = ns; _processMonitor = pm; }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true; _cts = new();
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try { await TrackUsageAsync(); await Task.Delay(10000, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    public void Stop() { _isRunning = false; _cts?.Cancel(); _cts?.Dispose(); _cts = null; }
    public void AddTimeLimit(TimeLimitItem limit) => _timeLimits.Add(limit);

    public void RemoveTimeLimit(string limitId)
    {
        var l = _timeLimits.FirstOrDefault(l => l.Id == limitId);
        if (l != null) { if (l.IsLocked) _processMonitor.UnlockApp(l.ProcessName); _timeLimits.Remove(l); }
    }

    public void DisableTimeLimit(string limitId)
    {
        var l = _timeLimits.FirstOrDefault(l => l.Id == limitId);
        if (l == null) return;
        l.IsEnabled = false;

        // Read the global cooldown setting from App.SettingsViewModel
        double cooldownHours = App.SettingsViewModel.TimeLimitCooldownHours;
        l.CooldownHours = cooldownHours;

        if (l.IsLocked)
        {
            if (cooldownHours <= 0)
            {
                // Cooldown is 0 — unlock immediately, no cooldown period
                l.IsInCooldown = false;
                l.CooldownEndAt = null;
                l.IsLocked = false;
                l.UsedMinutesToday = 0;
                l.LastResetDate = DateTime.Today;
                _processMonitor.UnlockApp(l.ProcessName);
                _notificationService.ShowNotification("Time Limit Disabled",
                    $"'{l.AppName}' unlocked immediately (cooldown = 0).");
            }
            else
            {
                l.IsInCooldown = true;
                l.CooldownEndAt = DateTime.Now.AddHours(cooldownHours);
                CooldownStarted?.Invoke(this, l);
                _notificationService.ShowNotification("Time Limit Disabled",
                    $"Apps for '{l.AppName}' remain locked until cooldown at {l.CooldownEndAt:HH:mm}.");
                _ = MonitorCooldownAsync(l);
            }
        }
    }

    public void EnableTimeLimit(string limitId)
    {
        var l = _timeLimits.FirstOrDefault(l => l.Id == limitId);
        if (l != null) { l.IsEnabled = true; l.IsInCooldown = false; l.CooldownEndAt = null;
            if (!l.IsLocked) _processMonitor.UnlockApp(l.ProcessName); }
    }

    public void LoadLimits(IEnumerable<TimeLimitItem> limits)
    {
        _timeLimits.Clear(); _timeLimits.AddRange(limits);
        foreach (var l in _timeLimits)
            if (l.LastResetDate < DateTime.Today) { l.UsedMinutesToday = 0; l.IsLocked = false; l.LastResetDate = DateTime.Today; }
    }

    private async Task TrackUsageAsync()
    {
        foreach (var l in _timeLimits)
        {
            if (l.LastResetDate < DateTime.Today) { l.UsedMinutesToday = 0; l.IsLocked = false; l.LastResetDate = DateTime.Today;
                if (l.IsEnabled) _processMonitor.UnlockApp(l.ProcessName); }
        }
        foreach (var l in _timeLimits.Where(l => l.IsEnabled && !l.IsLocked))
        {
            if (_processMonitor.IsProcessRunning(l.ProcessName))
            {
                l.UsedMinutesToday += 10.0 / 60.0;
                UsageUpdated?.Invoke(this, l);
                if (l.RemainingMinutes <= 0)
                {
                    l.IsLocked = true; l.LockedAt = DateTime.Now;
                    _processMonitor.LockApp(l.ProcessName);
                    AppLocked?.Invoke(this, l); LimitReached?.Invoke(this, l);
                    _notificationService.ShowNotification("Time Limit Reached",
                        $"'{l.AppName}' locked. Daily limit of {l.DailyLimitMinutes} min reached.");
                }
                else if (l.RemainingMinutes <= 5 && l.RemainingMinutes > 4.8)
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in ~5 min.");
                else if (l.RemainingMinutes <= 1 && l.RemainingMinutes > 0.8)
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in <1 min!");
            }
        }
    }

    private async Task MonitorCooldownAsync(TimeLimitItem l)
    {
        while (l.IsInCooldown && l.CooldownEndAt.HasValue)
        {
            await Task.Delay(60000);
            if (DateTime.Now >= l.CooldownEndAt.Value)
            {
                l.IsInCooldown = false; l.CooldownEndAt = null; l.IsLocked = false;
                l.UsedMinutesToday = 0; l.LastResetDate = DateTime.Today;
                _processMonitor.UnlockApp(l.ProcessName);
                _notificationService.ShowNotification("Cooldown Complete", $"'{l.AppName}' unlocked.");
                break;
            }
        }
    }

    public double GetRemainingTime(string processName)
        => _timeLimits.FirstOrDefault(l => l.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))?.RemainingMinutes ?? double.MaxValue;

    public void Dispose() { Stop(); GC.SuppressFinalize(this); }
}
