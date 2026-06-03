using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PLLauncher.Helpers;
using PLLauncher.Models;

namespace PLLauncher.Services;

/// <summary>
/// Tracks foreground app usage only (active window), polled every 30 seconds.
/// </summary>
public class AppUsageTrackingService : IDisposable
{
    private readonly DataService _dataService;
    private readonly InstalledAppsService _installedAppsService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private Dictionary<string, AppUsageRecord> _records = new();
    private DateTime? _installDate;
    private string? _lastForegroundProcess;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

    private static readonly HashSet<string> NoiseProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "csrss", "wininit", "services", "lsass", "svchost", "smss", "winlogon",
        "dwm", "fontdrvhost", "sihost", "taskhostw", "Registry", "MemCompression", "conhost",
        "dllhost", "ctfmon", "SearchIndexer", "RuntimeBroker", "SecurityHealthService",
        "SearchHost", "ShellExperienceHost", "StartMenuExperienceHost", "WidgetService",
        "ApplicationFrameHost", "TextInputHost", "LockApp", "SystemSettings", "backgroundTaskHost",
        "BackgroundTaskHost", "msedgewebview2", "MicrosoftEdgeUpdate", "smartscreen",
        "SecurityHealthSystray", "PhoneExperienceHost", "GameBarPresenceWriter",
        "SearchProtocolHost", "SearchFilterHost", "audiodg", "spoolsv", "WmiPrvSE",
        "AggregatorHost", "CompPkgSrv", "dasHost", "MoUsoCoreWorker", "MusNotificationUx",
    };

    public AppUsageTrackingService(DataService ds, InstalledAppsService installedApps)
    {
        _dataService = ds;
        _installedAppsService = installedApps;
    }

    public DateTime InstallDate
    {
        get
        {
            if (_installDate == null)
            {
                var markerPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PLLauncher", ".install_date");
                if (System.IO.File.Exists(markerPath))
                {
                    var text = System.IO.File.ReadAllText(markerPath);
                    DateTime.TryParse(text, out var parsed);
                    _installDate = parsed != default ? parsed : DateTime.Today;
                }
                else
                {
                    _installDate = DateTime.Today;
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(markerPath)!);
                        System.IO.File.WriteAllText(markerPath, _installDate.Value.ToString("O"));
                    }
                    catch { }
                }
            }
            return _installDate.Value;
        }
    }

    public async void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new();
        await LoadRecordsAsync();

        _ = Task.Run(async () =>
        {
            while (!_cts!.IsCancellationRequested)
            {
                try
                {
                    await PollForegroundAppAsync();
                    await Task.Delay(30000, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task LoadRecordsAsync()
    {
        try
        {
            var loaded = await _dataService.LoadAppUsageAsync();
            var today = DateTime.Today;
            _records = new(StringComparer.OrdinalIgnoreCase);
            foreach (var r in loaded)
            {
                // Normalize Date to remove any time component that may have shifted
                // during JSON roundtrip with timezone offset
                r.Date = r.Date.Date;
                // Discard records older than 60 days
                if (r.Date < today.AddDays(-60)) continue;
                var key = $"{r.ProcessName}|{r.Date:yyyy-MM-dd}";
                if (!_records.ContainsKey(key))
                    _records[key] = r;
            }
        }
        catch { _records = new(); }
    }

    private async Task SaveRecordsAsync()
    {
        try
        {
            await _dataService.SaveAppUsageAsync(_records.Values.ToList());
        }
        catch { }
    }

    private async Task PollForegroundAppAsync()
    {
        var processName = GetForegroundProcessName();
        if (string.IsNullOrEmpty(processName) || IsNoiseProcess(processName))
        {
            _lastForegroundProcess = null;
            return;
        }

        var now = DateTime.Now;
        var today = now.Date;
        var key = $"{processName}|{today:yyyy-MM-dd}";

        if (!_records.TryGetValue(key, out var record))
        {
            var displayName = _installedAppsService.GetDisplayNameForProcess(processName)
                ?? CleanDisplayName(processName);
            record = new AppUsageRecord
            {
                ProcessName = processName,
                DisplayName = displayName,
                Date = today,
                FirstSeenAt = now,
                Sessions = 1
            };
            _records[key] = record;
        }
        else if (!string.Equals(_lastForegroundProcess, key, StringComparison.OrdinalIgnoreCase))
        {
            record.Sessions++;
        }

        record.TotalMinutes += 0.5;
        record.LastSeenAt = now;
        _lastForegroundProcess = key;

        if (DateTime.Now - _lastSaveTime >= SaveInterval)
        {
            _lastSaveTime = DateTime.Now;
            await SaveRecordsAsync();
        }
    }

    private static string? GetForegroundProcessName()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;

            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNoiseProcess(string name)
    {
        if (NoiseProcesses.Contains(name)) return true;
        if (name.StartsWith("Nt", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("webview", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("background", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string CleanDisplayName(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return processName;
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < processName.Length; i++)
        {
            if (i > 0 && char.IsUpper(processName[i]) && char.IsLower(processName[i - 1]))
                result.Append(' ');
            result.Append(processName[i]);
        }
        var str = result.ToString();
        return char.ToUpper(str[0]) + str[1..];
    }

    public List<AppUsageSummary> GetUsageSummaries(DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date;
        var records = _records.Values
            .Where(r => r.Date >= fromDate && r.Date < toDate.AddDays(1))
            .Where(r => !IsNoiseProcess(r.ProcessName))
            .GroupBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AppUsageSummary
            {
                ProcessName = g.Key,
                DisplayName = g.First().DisplayName,
                TotalMinutes = g.Sum(r => r.TotalMinutes),
                TotalSessions = g.Sum(r => r.Sessions),
                FirstSeenAt = g.Min(r => r.FirstSeenAt),
                LastSeenAt = g.Max(r => r.LastSeenAt)
            })
            .Where(s => s.TotalMinutes >= 1)
            .OrderByDescending(s => s.TotalMinutes)
            .ToList();

        var maxMinutes = records.FirstOrDefault()?.TotalMinutes ?? 0;
        foreach (var s in records)
            s.UsageBarPercent = maxMinutes > 0 ? (s.TotalMinutes / maxMinutes) * 100 : 0;

        return records;
    }

    public void Dispose()
    {
        Stop();
        _ = SaveRecordsAsync();
        GC.SuppressFinalize(this);
    }
}
