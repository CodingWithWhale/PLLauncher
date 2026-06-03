using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TaskType _taskType = TaskType.Shutdown;

    [ObservableProperty]
    private TaskScheduleType _scheduleType = TaskScheduleType.Once;

    [ObservableProperty]
    private DateTime _scheduledTime = DateTime.Now;

    [ObservableProperty]
    private double _delayMinutes = 0;

    [ObservableProperty]
    private string _targetApp = string.Empty;

    [ObservableProperty]
    private TaskStatus _status = TaskStatus.Pending;

    [ObservableProperty]
    private bool _isRecurring = false;

    [ObservableProperty]
    private DayOfWeek[] _recurringDays = Array.Empty<DayOfWeek>();

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _lastExecutedAt;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isAntiSleep = false;

    [ObservableProperty]
    private int _antiSleepIntervalSeconds = 60;
}

public enum TaskType
{
    Shutdown,
    Restart,
    Sleep,
    LockPC,
    OpenApp,
    CloseApp,
    RunCommand,
    AntiSleep,
    Hibernate
}

public enum TaskScheduleType
{
    Once,
    Delayed,
    SpecificTime,
    Daily,
    Weekly
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Cancelled,
    Failed
}
