using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class ScheduleItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private ScheduleActionType _actionType = ScheduleActionType.OpenApp;

    [ObservableProperty]
    private string _actionTarget = string.Empty;

    [ObservableProperty]
    private TimeSpan _time = TimeSpan.FromHours(16);

    /// <summary>
    /// The specific date for this schedule item. Used for calendar-based scheduling
    /// and for "Once" items to know which day they fire on.
    /// </summary>
    [ObservableProperty]
    private DateTime? _scheduledDate;

    [ObservableProperty]
    private bool _isRecurring = false;

    [ObservableProperty]
    private DayOfWeek[] _recurringDays = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _lastExecutedAt;

    [ObservableProperty]
    private ScheduleRecurrence _recurrence = ScheduleRecurrence.Weekday;

    // === Reminder-specific fields ===

    [ObservableProperty]
    private bool _isReminder = false;

    [ObservableProperty]
    private string _reminderMessage = string.Empty;

    [ObservableProperty]
    private ReminderUrgency _urgency = ReminderUrgency.Low;
}

public enum ScheduleActionType
{
    OpenApp,
    CloseApp,
    RunCommand,
    Shutdown,
    Restart,
    Sleep,
    LockPC,
    RunWorkflow,
    Reminder
}

public enum ScheduleRecurrence
{
    Once,
    Daily,
    Weekday,
    Weekly,
    Custom
}

public enum ReminderUrgency
{
    Low,
    Medium,
    High
}
