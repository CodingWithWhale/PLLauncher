using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly HotkeyService _hotkeyService;
    private readonly TaskSchedulerService _taskSchedulerService;
    private readonly TimeTrackingService _timeTrackingService;
    private readonly ScheduleService _scheduleService;

    [ObservableProperty] private int _activeHotkeyCount;
    [ObservableProperty] private int _activeTaskCount;
    [ObservableProperty] private int _activeScheduleCount;
    [ObservableProperty] private int _activeTimeLimitsCount;
    [ObservableProperty] private bool _isAntiSleepActive;
    [ObservableProperty] private ObservableCollection<KeybindItem> _recentKeybinds = new();
    [ObservableProperty] private ObservableCollection<TaskItem> _runningTasks = new();
    [ObservableProperty] private ObservableCollection<TimeLimitItem> _activeTimeLimits = new();
    [ObservableProperty] private ObservableCollection<ScheduleItem> _upcomingSchedules = new();
    [ObservableProperty] private string _systemStatus = "All systems operational";
    [ObservableProperty] private string _greeting = string.Empty;

    public DashboardViewModel(DataService ds, HotkeyService hs, TaskSchedulerService ts, TimeTrackingService tt, ScheduleService ss)
    { _dataService = ds; _hotkeyService = hs; _taskSchedulerService = ts; _timeTrackingService = tt; _scheduleService = ss; UpdateGreeting(); }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var keybinds = await _dataService.LoadKeybindsAsync();
        var tasks = await _dataService.LoadTasksAsync();
        var limits = await _dataService.LoadTimeLimitsAsync();
        var schedules = await _dataService.LoadSchedulesAsync();
        ActiveHotkeyCount = keybinds.Count(k => k.IsEnabled);
        ActiveTaskCount = _taskSchedulerService.ActiveTasks.Count;
        ActiveScheduleCount = schedules.Count(s => s.IsEnabled);
        ActiveTimeLimitsCount = limits.Count(l => l.IsEnabled);
        IsAntiSleepActive = _taskSchedulerService.IsAntiSleepActive;
        RecentKeybinds = new(keybinds.Take(5));
        RunningTasks = new(_taskSchedulerService.ActiveTasks);
        ActiveTimeLimits = new(limits.Where(l => l.IsEnabled).Take(5));
        UpcomingSchedules = new(schedules.Where(s => s.IsEnabled).Take(5));
        UpdateGreeting();
        SystemStatus = IsAntiSleepActive ? "Anti-sleep mode active" : "All systems operational";
    }

    [RelayCommand]
    private void ToggleAntiSleep()
    {
        if (IsAntiSleepActive) _taskSchedulerService.StopAntiSleep();
        else _taskSchedulerService.CreateAntiSleepTask();
        IsAntiSleepActive = _taskSchedulerService.IsAntiSleepActive;
    }

    private void UpdateGreeting()
    {
        Greeting = DateTime.Now.Hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 17 => "Good afternoon",
            >= 17 and < 21 => "Good evening",
            _ => "Good night"
        };
    }
}
