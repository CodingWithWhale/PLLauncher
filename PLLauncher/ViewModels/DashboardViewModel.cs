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
    [ObservableProperty] private ObservableCollection<KeybindItem> _recentKeybinds = new();
    [ObservableProperty] private ObservableCollection<TaskItem> _runningTasks = new();
    [ObservableProperty] private ObservableCollection<TimeLimitItem> _activeTimeLimits = new();
    [ObservableProperty] private ObservableCollection<ScheduleItem> _upcomingSchedules = new();
    [ObservableProperty] private string _nextTaskInfo = "No upcoming tasks";
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
        RecentKeybinds = new(keybinds.Take(5));
        RunningTasks = new(_taskSchedulerService.ActiveTasks);
        ActiveTimeLimits = new(limits.Where(l => l.IsEnabled).Take(5));
        UpcomingSchedules = new(schedules.Where(s => s.IsEnabled).Take(5));
        UpdateGreeting();
        UpdateNextTask();
    }

    private void UpdateNextTask()
    {
        var next = _taskSchedulerService.ActiveTasks
            .Where(t => t.Status == Models.TaskStatus.Pending)
            .OrderBy(t => t.ScheduledTime)
            .FirstOrDefault();

        if (next == null)
        {
            NextTaskInfo = "No upcoming tasks";
            return;
        }

        var remaining = next.ScheduledTime - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            NextTaskInfo = $"{next.TaskType} - now";
            return;
        }

        var timeStr = remaining.TotalHours >= 1
            ? $"in {remaining.Hours}h {remaining.Minutes}m"
            : $"in {Math.Max(1, (int)remaining.TotalMinutes)}m";
        NextTaskInfo = $"{next.TaskType} {timeStr}";
    }

    private void UpdateGreeting()
    {
        var loc = LocalizationService.Instance;
        Greeting = DateTime.Now.Hour switch
        {
            >= 5 and < 12 => loc.Get("dashboard.greeting_morning"),
            >= 12 and < 17 => loc.Get("dashboard.greeting_afternoon"),
            >= 17 and < 21 => loc.Get("dashboard.greeting_evening"),
            _ => loc.Get("dashboard.greeting_night")
        };
    }
}
