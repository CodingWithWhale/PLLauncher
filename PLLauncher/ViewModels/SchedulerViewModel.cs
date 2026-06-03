using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class SchedulerViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly ScheduleService _scheduleService;

    [ObservableProperty] private ObservableCollection<ScheduleItem> _schedules = new();
    [ObservableProperty] private ScheduleItem? _selectedSchedule;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private string _newScheduleName = string.Empty;
    [ObservableProperty] private string _newScheduleDescription = string.Empty;
    [ObservableProperty] private ScheduleActionType _newActionType = ScheduleActionType.OpenApp;
    [ObservableProperty] private string _newActionTarget = string.Empty;
    [ObservableProperty] private TimeSpan _newTime = TimeSpan.FromHours(16);
    [ObservableProperty] private ScheduleRecurrence _newRecurrence = ScheduleRecurrence.Weekday;

    // Reminder-specific fields
    [ObservableProperty] private bool _isNewItemReminder;
    [ObservableProperty] private string _newReminderMessage = string.Empty;
    [ObservableProperty] private ReminderUrgency _newUrgency = ReminderUrgency.Low;
    [ObservableProperty] private DateTime? _newScheduledDate;

    // Custom recurring days — set by SchedulerPage when "Custom" is selected
    public DayOfWeek[] NewRecurringDays { get; set; } = Array.Empty<DayOfWeek>();

    public SchedulerViewModel(DataService ds, ScheduleService ss) { _dataService = ds; _scheduleService = ss; }

    [RelayCommand]
    private async Task LoadSchedulesAsync()
    { var s = await _dataService.LoadSchedulesAsync(); _scheduleService.LoadSchedules(s); Schedules = new(s); }

    [RelayCommand]
    private async Task AddScheduleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewScheduleName)) return;

        var schedule = new ScheduleItem
        {
            Name = NewScheduleName,
            Description = NewScheduleDescription,
            ActionType = IsNewItemReminder ? ScheduleActionType.Reminder : NewActionType,
            ActionTarget = NewActionTarget,
            Time = NewTime,
            ScheduledDate = NewScheduledDate,
            Recurrence = NewRecurrence,
            IsRecurring = NewRecurrence != ScheduleRecurrence.Once,
            IsEnabled = true,
            RecurringDays = GetRecurringDays(NewRecurrence),
            CreatedAt = DateTime.Now,

            // Reminder fields
            IsReminder = IsNewItemReminder,
            ReminderMessage = NewReminderMessage,
            Urgency = NewUrgency
        };

        _scheduleService.AddSchedule(schedule); Schedules.Add(schedule);
        await _dataService.SaveSchedulesAsync(Schedules.ToList());
        NewScheduleName = ""; NewScheduleDescription = ""; NewActionTarget = "";
        NewReminderMessage = ""; IsNewItemReminder = false; NewScheduledDate = null;
        IsAddingNew = false;
    }

    [RelayCommand]
    private async Task DeleteScheduleAsync(ScheduleItem? s)
    { if (s == null) return; _scheduleService.RemoveSchedule(s.Id);
      Schedules.Remove(s); await _dataService.SaveSchedulesAsync(Schedules.ToList()); }

    [RelayCommand]
    private async Task ToggleScheduleAsync(ScheduleItem s)
    { s.IsEnabled = !s.IsEnabled; await _dataService.SaveSchedulesAsync(Schedules.ToList()); }

    /// <summary>
    /// Gets schedule items for a specific date (for calendar view).
    /// </summary>
    public List<ScheduleItem> GetSchedulesForDate(DateTime date)
        => _scheduleService.GetSchedulesForDate(date);

    private DayOfWeek[] GetRecurringDays(ScheduleRecurrence r) => r switch
    {
        ScheduleRecurrence.Custom => NewRecurringDays.Length > 0
            ? NewRecurringDays
            : new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
        ScheduleRecurrence.Weekday => new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
        ScheduleRecurrence.Daily => Enum.GetValues<DayOfWeek>(),
        ScheduleRecurrence.Weekly => new[] { DateTime.Now.DayOfWeek },
        _ => new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
    };
}
