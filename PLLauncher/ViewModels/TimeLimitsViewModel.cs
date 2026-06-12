using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class TimeLimitsViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly TimeTrackingService _timeTrackingService;
    private readonly ProcessMonitorService _processMonitor;

    [ObservableProperty] private ObservableCollection<TimeLimitItem> _timeLimits = new();
    [ObservableProperty] private TimeLimitItem? _selectedLimit;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private string _newAppName = string.Empty;
    [ObservableProperty] private string _newProcessName = string.Empty;
    [ObservableProperty] private double _newDailyLimitMinutes = 120;
    [ObservableProperty] private ObservableCollection<ProcessInfo> _runningProcesses = new();

    public TimeLimitsViewModel(DataService ds, TimeTrackingService tt, ProcessMonitorService pm)
    { _dataService = ds; _timeTrackingService = tt; _processMonitor = pm; }

    [RelayCommand]
    private async Task LoadTimeLimitsAsync()
    { var limits = await _dataService.LoadTimeLimitsAsync(); _timeTrackingService.LoadLimits(limits);
      TimeLimits = new(limits); }

    private static string NormalizeProcessName(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        return name;
    }

    [RelayCommand]
    private async Task AddTimeLimitAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAppName) || string.IsNullOrWhiteSpace(NewProcessName)) return;
        var processName = NormalizeProcessName(NewProcessName.Trim());
        var limit = new TimeLimitItem { AppName = NewAppName, ProcessName = processName,
            DailyLimitMinutes = NewDailyLimitMinutes, IsEnabled = true, LastResetDate = DateTime.Today };
        _timeTrackingService.AddTimeLimit(limit);
        TimeLimits.Add(limit);
        await _dataService.SaveTimeLimitsAsync(TimeLimits.ToList());
        NewAppName = ""; NewProcessName = ""; NewDailyLimitMinutes = 120; IsAddingNew = false;
    }

    [RelayCommand]
    private async Task DeleteTimeLimitAsync(TimeLimitItem? l)
    { if (l == null) return; _timeTrackingService.RemoveTimeLimit(l.Id);
      TimeLimits.Remove(l); await _dataService.SaveTimeLimitsAsync(TimeLimits.ToList()); }

    [RelayCommand]
    private async Task DisableTimeLimitAsync(TimeLimitItem l)
    { _timeTrackingService.DisableTimeLimit(l.Id); await _dataService.SaveTimeLimitsAsync(TimeLimits.ToList()); }

    [RelayCommand]
    private async Task EnableTimeLimitAsync(TimeLimitItem l)
    { _timeTrackingService.EnableTimeLimit(l.Id); await _dataService.SaveTimeLimitsAsync(TimeLimits.ToList()); }

    [RelayCommand]
    private void RefreshRunningProcesses()
    { RunningProcesses = new(_processMonitor.GetRunningProcesses()
        .Where(p => !string.IsNullOrEmpty(p.ProcessName)).GroupBy(p => p.ProcessName)
        .Select(g => g.First()).OrderBy(p => p.ProcessName).ToList()); }
}
