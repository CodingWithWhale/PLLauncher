using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly DataService _dataService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

    public event EventHandler<TimeLimitItem>? LimitReached;
    public event EventHandler<TimeLimitItem>? AppLocked;
    public event EventHandler<TimeLimitItem>? CooldownStarted;
    public event EventHandler<TimeLimitItem>? UsageUpdated;

    public IReadOnlyList<TimeLimitItem> TimeLimits => _timeLimits.AsReadOnly();

    public TimeTrackingService(NotificationService ns, ProcessMonitorService pm, DataService ds)
    { _notificationService = ns; _processMonitor = pm; _dataService = ds; }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true; _cts = new();

        // Tracking loop: every ~10 seconds, count foreground usage based on actual elapsed time
        _ = Task.Run(async () =>
        {
            var sw = new Stopwatch();
            sw.Start();
            var lastTickElapsed = TimeSpan.Zero;
            while (!_cts.IsCancellationRequested)
            {
                var now = sw.Elapsed;
                var elapsedSinceLastTick = now - lastTickElapsed;
                lastTickElapsed = now;
                try { await TrackUsageAsync(elapsedSinceLastTick); }
                catch (OperationCanceledException) { break; }
                catch { }
                var toWait = TimeSpan.FromSeconds(10) - (sw.Elapsed - now);
                if (toWait > TimeSpan.Zero)
                    try { await Task.Delay(toWait, _cts.Token); }
                    catch (OperationCanceledException) { break; }
                    catch { }
            }
        }, _cts.Token);

        // Enforcement loop: every 2 seconds, catch re-launch attempts
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try { EnforceLockedProcesses(); await Task.Delay(2000, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    // Tracks which locked processes we've already notified about (per re-launch attempt)
    private readonly Dictionary<string, DateTime> _lastNotificationTime = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromSeconds(10);

    public void Stop() { _isRunning = false; _cts?.Cancel(); _cts?.Dispose(); _cts = null; }

    private void EnforceLockedProcesses()
    {
        foreach (var l in _timeLimits.Where(l => l.IsLocked))
        {
            if (string.IsNullOrWhiteSpace(l.ProcessName)) continue;

            if (!_processMonitor.IsProcessRunning(l.ProcessName)) continue;

            _processMonitor.TerminateProcess(l.ProcessName);

            var now = DateTime.Now;
            if (_lastNotificationTime.TryGetValue(l.ProcessName, out var last) &&
                (now - last) < NotificationCooldown)
                continue;

            _lastNotificationTime[l.ProcessName] = now;
            _notificationService.ShowNotification("Time Limit Reached",
                $"You've hit your time limit for today.");
        }
    }
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
        Console.WriteLine($"[TimeTrack] Limits loaded: {string.Join(", ", _timeLimits.Select(l => $"{l.AppName}({l.ProcessName})"))}");
    }

    private async Task TrackUsageAsync(TimeSpan elapsedSinceLastTick)
    {
        var foregroundProcess = _processMonitor.GetForegroundProcessName();
        Console.WriteLine($"[TimeTrack] FG={foregroundProcess ?? "(null)"}, elapsed={elapsedSinceLastTick.TotalSeconds:F1}s");

        foreach (var l in _timeLimits)
        {
            if (l.LastResetDate < DateTime.Today)
            {
                l.UsedMinutesToday = 0; l.IsLocked = false;
                l.IsInCooldown = false; l.CooldownEndAt = null;
                l.LastResetDate = DateTime.Today;
                _lastNotificationTime.Clear();
                if (l.IsEnabled) _processMonitor.UnlockApp(l.ProcessName);
            }
        }

        double incrementMinutes = elapsedSinceLastTick.TotalMinutes;

        bool dirty = false;
        foreach (var l in _timeLimits.Where(l => l.IsEnabled && !l.IsLocked))
        {
            if (string.Equals(foregroundProcess, l.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[TimeTrack] MATCH {l.AppName}: +{incrementMinutes:F4}min (now={l.UsedMinutesToday + incrementMinutes:F2}, limit={l.DailyLimitMinutes})");
                l.UsedMinutesToday += incrementMinutes;
                dirty = true;
                UsageUpdated?.Invoke(this, l);
                if (l.RemainingMinutes <= 0 && !l.IsLocked)
                {
                    l.IsLocked = true; l.LockedAt = DateTime.Now;
                    _processMonitor.LockApp(l.ProcessName);
                    AppLocked?.Invoke(this, l); LimitReached?.Invoke(this, l);
                    _notificationService.ShowNotification("Time Limit Reached",
                        $"'{l.AppName}' locked. Daily limit of {l.DailyLimitMinutes} min reached.");

                    // Start cooldown automatically
                    double cooldownHours = App.SettingsViewModel.TimeLimitCooldownHours;
                    if (cooldownHours <= 0) cooldownHours = 10.0 / 60.0; // default 10 min
                    l.CooldownHours = cooldownHours;
                    l.IsInCooldown = true;
                    l.CooldownEndAt = DateTime.Now.AddHours(cooldownHours);
                    CooldownStarted?.Invoke(this, l);
                    _ = MonitorCooldownAsync(l);
                }
                else if (l.RemainingMinutes <= 5 && l.RemainingMinutes > 4.8)
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in ~5 min.");
                else if (l.RemainingMinutes <= 1 && l.RemainingMinutes > 0.8)
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in <1 min!");
            }
        }

        if (dirty && DateTime.Now - _lastSaveTime >= SaveInterval)
        {
            _lastSaveTime = DateTime.Now;
            await SaveLimitsAsync();
        }
    }

    private async Task SaveLimitsAsync()
    {
        try { await _dataService.SaveTimeLimitsAsync(_timeLimits.ToList()); }
        catch { }
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

    public void Dispose()
    {
        Stop();
        _ = SaveLimitsAsync();
        GC.SuppressFinalize(this);
    }
}
