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
    private readonly object _timeLimitsLock = new();
    private readonly NotificationService _notificationService;
    private readonly ProcessMonitorService _processMonitor;
    private readonly DataService _dataService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);
    // Track which warnings have been sent per limit to avoid spamming
    private readonly HashSet<string> _warningsSent = new();

    public event EventHandler<TimeLimitItem>? LimitReached;
    public event EventHandler<TimeLimitItem>? AppLocked;
    public event EventHandler<TimeLimitItem>? LockedProcessReLaunched;
    public event EventHandler<TimeLimitItem>? CooldownStarted;
    public event EventHandler<TimeLimitItem>? CooldownEnded;
    public event EventHandler<TimeLimitItem>? UsageUpdated;

    public IReadOnlyList<TimeLimitItem> TimeLimits
    {
        get { lock (_timeLimitsLock) { return _timeLimits.ToList().AsReadOnly(); } }
    }

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
                // Wait ~10s BEFORE each measurement so the first tick captures a full interval
                var now = sw.Elapsed;
                var toWait = TimeSpan.FromSeconds(10) - (now - lastTickElapsed);
                if (toWait > TimeSpan.Zero)
                    try { await Task.Delay(toWait, _cts.Token); }
                    catch (OperationCanceledException) { break; }
                    catch { }
                now = sw.Elapsed;
                var elapsedSinceLastTick = now - lastTickElapsed;
                lastTickElapsed = now;
                try { await TrackUsageAsync(elapsedSinceLastTick); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);

        // Enforcement loop: every 1 second, catch re-launch attempts and re-check limits
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try { EnforceLockedProcesses(); await Task.Delay(1000, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    public void Stop() { _isRunning = false; _cts?.Cancel(); _cts?.Dispose(); _cts = null; }

    private void EnforceLockedProcesses()
    {
        // Take snapshot of limits
        List<TimeLimitItem> active;
        lock (_timeLimitsLock) { active = _timeLimits.ToList(); }

        // First pass: fire re-launch events for dismissed locked processes that are running
        // (do this before terminating so IsProcessRunning returns true)
        foreach (var l in active)
        {
            if (string.IsNullOrWhiteSpace(l.ProcessName)) continue;
            if (!l.IsLocked || !l.SuppressAutoLaunch) continue;
            if (!_processMonitor.IsProcessRunning(l.ProcessName)) continue;
            LockedProcessReLaunched?.Invoke(this, l);
        }

        // Now enforce (terminates all locked processes)
        _processMonitor.EnforceLockedProcesses();

        // Second pass: check for newly-reached limits
        foreach (var l in active)
        {
            if (string.IsNullOrWhiteSpace(l.ProcessName)) continue;
            if (!_processMonitor.IsProcessRunning(l.ProcessName)) continue;

            // If not yet locked but time is up, lock now
            if (!l.IsLocked && l.IsEnabled && l.RemainingMinutes <= 0)
            {
                Console.WriteLine($"[TimeTrack] ENFORCE locking {l.AppName} ({l.ProcessName}): Used={l.UsedMinutesToday:F2}, Limit={l.DailyLimitMinutes}, Remaining={l.RemainingMinutes:F3}");
                l.IsLocked = true;
                l.LockedAt = DateTime.Now;
                var exePath = _processMonitor.GetProcessPath(l.ProcessName);
                if (!string.IsNullOrEmpty(exePath))
                    l.AppExecutablePath = exePath;
                _processMonitor.LockApp(l.ProcessName);
                // Start cooldown BEFORE firing AppLocked so overlay sees it immediately
                var duration = l.LockDuration;
                if (duration <= TimeSpan.Zero)
                    duration = TimeSpan.FromMinutes(10);
                l.IsInCooldown = true;
                l.CooldownEndAt = DateTime.Now.Add(duration);
                AppLocked?.Invoke(this, l);
                LimitReached?.Invoke(this, l);
                CooldownStarted?.Invoke(this, l);
                _ = MonitorCooldownAsync(l);
            }

            if (!l.IsLocked) continue;

            _processMonitor.TerminateProcess(l.ProcessName);
        }
    }
    public void AddTimeLimit(TimeLimitItem limit) { lock (_timeLimitsLock) { _timeLimits.Add(limit); } }

    public void RemoveTimeLimit(string limitId)
    {
        TimeLimitItem? l;
        lock (_timeLimitsLock) { l = _timeLimits.FirstOrDefault(x => x.Id == limitId); }
        if (l != null) { if (l.IsLocked) _processMonitor.UnlockApp(l.ProcessName); lock (_timeLimitsLock) { _timeLimits.Remove(l); } }
        _warningsSent.Remove($"{limitId}_5min");
        _warningsSent.Remove($"{limitId}_1min");
    }

    public void DisableTimeLimit(string limitId)
    {
        TimeLimitItem? l;
        lock (_timeLimitsLock) { l = _timeLimits.FirstOrDefault(x => x.Id == limitId); }
        if (l == null) return;
        l.IsEnabled = false;

        if (l.IsLocked)
        {
            var duration = l.LockDuration;
            if (duration <= TimeSpan.Zero)
            {
                l.IsInCooldown = false;
                l.CooldownEndAt = null;
                l.IsLocked = false;
                l.UsedMinutesToday = 0;
                l.LastResetDate = DateTime.Today;
                _processMonitor.UnlockApp(l.ProcessName);
                _notificationService.ShowNotification("Time Limit Disabled",
                    $"'{l.AppName}' unlocked immediately.");
                _warningsSent.Remove($"{l.Id}_5min");
                _warningsSent.Remove($"{l.Id}_1min");
            }
            else
            {
                l.IsInCooldown = true;
                l.CooldownEndAt = DateTime.Now.Add(duration);
                CooldownStarted?.Invoke(this, l);
                _notificationService.ShowNotification("Time Limit Disabled",
                    $"'{l.AppName}' remains locked until {l.CooldownEndAt:HH:mm}.");
                _ = MonitorCooldownAsync(l);
            }
        }
    }

    public void EnableTimeLimit(string limitId)
    {
        TimeLimitItem? l;
        lock (_timeLimitsLock) { l = _timeLimits.FirstOrDefault(x => x.Id == limitId); }
        if (l != null) { l.IsEnabled = true; l.IsInCooldown = false; l.CooldownEndAt = null;
            if (!l.IsLocked) _processMonitor.UnlockApp(l.ProcessName); }
        _warningsSent.Remove($"{limitId}_5min");
        _warningsSent.Remove($"{limitId}_1min");
    }

    public static string NormalizeProcessName(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        return name;
    }

    public void LoadLimits(IEnumerable<TimeLimitItem> limits)
    {
        lock (_timeLimitsLock)
        {
            _timeLimits.Clear();
            foreach (var l in limits)
            {
                l.ProcessName = NormalizeProcessName(l.ProcessName);
                if (l.LastResetDate < DateTime.Today) { l.UsedMinutesToday = 0; l.IsLocked = false; l.LastResetDate = DateTime.Today; }
                _timeLimits.Add(l);
            }
            Console.WriteLine($"[TimeTrack] Limits loaded: {string.Join(", ", _timeLimits.Select(x => $"{x.AppName}({x.ProcessName})"))}");
        }
    }

    private async Task TrackUsageAsync(TimeSpan elapsedSinceLastTick)
    {
        var foregroundProcess = _processMonitor.GetForegroundProcessName();
        Console.WriteLine($"[TimeTrack] FG={foregroundProcess ?? "(null)"}, elapsed={elapsedSinceLastTick.TotalSeconds:F1}s");

        // Fallback: if foreground detection returns null, check all visible-window processes
        List<string>? visibleProcesses = null;
        if (foregroundProcess == null)
        {
            visibleProcesses = _processMonitor.GetVisibleProcessNames();
            if (visibleProcesses.Count > 0)
                Console.WriteLine($"[TimeTrack] Fallback: {visibleProcesses.Count} visible processes detected");
        }

        List<TimeLimitItem> snapshot;
        lock (_timeLimitsLock) { snapshot = _timeLimits.ToList(); }

        foreach (var l in snapshot)
        {
            if (l.LastResetDate < DateTime.Today)
            {
                l.UsedMinutesToday = 0; l.IsLocked = false;
                l.IsInCooldown = false; l.CooldownEndAt = null;
                l.LastResetDate = DateTime.Today;
                if (l.IsEnabled) _processMonitor.UnlockApp(l.ProcessName);
                _warningsSent.Remove($"{l.Id}_5min");
                _warningsSent.Remove($"{l.Id}_1min");
            }
        }

        double incrementMinutes = elapsedSinceLastTick.TotalMinutes;

        bool dirty = false;
        foreach (var l in snapshot.Where(l => l.IsEnabled && !l.IsLocked))
        {
            bool isMatch = string.Equals(foregroundProcess, l.ProcessName, StringComparison.OrdinalIgnoreCase);

            // Fallback 1: if foreground detection failed, match against any visible-window process
            if (!isMatch && foregroundProcess == null && visibleProcesses != null)
                isMatch = visibleProcesses.Any(vp => string.Equals(vp, l.ProcessName, StringComparison.OrdinalIgnoreCase));

            // Fallback 2: if process is running AND user is actively using the computer, count it
            if (!isMatch)
            {
                double idleSeconds = ProcessMonitorService.GetUserIdleSeconds();
                if (idleSeconds >= 0 && idleSeconds < 60 && _processMonitor.IsProcessRunning(l.ProcessName))
                {
                    Console.WriteLine($"[TimeTrack] Active-user fallback: {l.AppName} is running, user idle={idleSeconds:F0}s");
                    isMatch = true;
                }
            }

            if (isMatch)
            {
                double prevRemaining = l.RemainingMinutes;
                double newTotal = l.UsedMinutesToday + incrementMinutes;
                Console.WriteLine($"[TimeTrack] MATCH {l.AppName}: +{incrementMinutes:F4}min (now={newTotal:F2}, limit={l.DailyLimitMinutes})");
                l.UsedMinutesToday = newTotal;
                // Store executable path early (before lock) so we always have it for re-launch later
                if (string.IsNullOrEmpty(l.AppExecutablePath))
                {
                    var foundPath = _processMonitor.GetProcessPath(l.ProcessName);
                    if (!string.IsNullOrEmpty(foundPath))
                        l.AppExecutablePath = foundPath;
                }
                dirty = true;
                UsageUpdated?.Invoke(this, l);
                double remaining = l.RemainingMinutes;
                Console.WriteLine($"[TimeTrack] {l.AppName}: remaining={remaining:F4} min");
                if (remaining <= 0)
                {
                    if (!l.IsLocked)
                    {
                        Console.WriteLine($"[TimeTrack] LOCK {l.AppName}: Used={l.UsedMinutesToday:F2}, Limit={l.DailyLimitMinutes}, Remaining={remaining:F3}");
                        l.IsLocked = true; l.LockedAt = DateTime.Now;
                        var exePath = _processMonitor.GetProcessPath(l.ProcessName);
                        if (!string.IsNullOrEmpty(exePath))
                            l.AppExecutablePath = exePath;
                        _processMonitor.LockApp(l.ProcessName);
                        _processMonitor.TerminateProcess(l.ProcessName);
                        var duration = l.LockDuration;
                        if (duration <= TimeSpan.Zero)
                            duration = TimeSpan.FromMinutes(10);
                        l.IsInCooldown = true;
                        l.CooldownEndAt = DateTime.Now.Add(duration);
                        AppLocked?.Invoke(this, l); LimitReached?.Invoke(this, l);
                        CooldownStarted?.Invoke(this, l);
                        _ = MonitorCooldownAsync(l);
                        _warningsSent.Remove($"{l.Id}_5min");
                        _warningsSent.Remove($"{l.Id}_1min");
                    }
                    else
                    {
                        Console.WriteLine($"[TimeTrack] {l.AppName}: already locked, skipping re-lock");
                    }
                }
                else if (remaining <= 5 && prevRemaining > 4.8 && _warningsSent.Add($"{l.Id}_5min"))
                {
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in ~5 min.");
                }
                else if (remaining <= 1 && prevRemaining > 0.8 && _warningsSent.Add($"{l.Id}_1min"))
                {
                    _notificationService.ShowNotification("Time Limit Warning", $"'{l.AppName}' will lock in <1 min!");
                }
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
        try
        {
            List<TimeLimitItem> snapshot;
            lock (_timeLimitsLock) { snapshot = _timeLimits.ToList(); }
            await _dataService.SaveTimeLimitsAsync(snapshot);
        }
        catch { }
    }

    private async Task MonitorCooldownAsync(TimeLimitItem l)
    {
        while (l.IsInCooldown && l.CooldownEndAt.HasValue)
        {
            await Task.Delay(5000);
            if (DateTime.Now >= l.CooldownEndAt.Value)
            {
                l.IsInCooldown = false; l.CooldownEndAt = null; l.IsLocked = false;
                l.UsedMinutesToday = 0; l.LastResetDate = DateTime.Today;
                _processMonitor.UnlockApp(l.ProcessName);
                CooldownEnded?.Invoke(this, l);
                _warningsSent.Remove($"{l.Id}_5min");
                _warningsSent.Remove($"{l.Id}_1min");
                // Re-launch the app (always, regardless of SuppressAutoLaunch)
                var launchPath = l.AppExecutablePath;
                if (string.IsNullOrEmpty(launchPath))
                    launchPath = _processMonitor.GetProcessPath(l.ProcessName);
                if (!string.IsNullOrEmpty(launchPath))
                    _processMonitor.LaunchApp(launchPath);
                l.SuppressAutoLaunch = false;
                break;
            }
        }
    }

    public double GetRemainingTime(string processName)
    {
        lock (_timeLimitsLock)
        {
            return _timeLimits.FirstOrDefault(l => l.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))?.RemainingMinutes ?? double.MaxValue;
        }
    }

    private static string FormatLimit(double minutes)
    {
        var totalMinutes = (int)Math.Ceiling(minutes);
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        if (h > 0 && m > 0) return $"{h} hour(s) {m} minute(s)";
        if (h > 0) return $"{h} hour(s)";
        return $"{m} minute(s)";
    }

    public void Dispose()
    {
        Stop();
        _ = SaveLimitsAsync();
        GC.SuppressFinalize(this);
    }
}
