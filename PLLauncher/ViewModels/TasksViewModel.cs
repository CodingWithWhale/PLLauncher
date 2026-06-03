using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class TasksViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly TaskSchedulerService _taskSchedulerService;

    [ObservableProperty] private ObservableCollection<TaskItem> _tasks = new();
    [ObservableProperty] private TaskItem? _selectedTask;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private string _newTaskName = string.Empty;
    [ObservableProperty] private TaskType _newTaskType = TaskType.Shutdown;
    [ObservableProperty] private TaskScheduleType _newScheduleType = TaskScheduleType.Delayed;
    [ObservableProperty] private double _newDelayMinutes = 60;
    [ObservableProperty] private DateTime _newScheduledTime = DateTime.Now.AddHours(1);
    [ObservableProperty] private string _newTargetApp = string.Empty;
    [ObservableProperty] private bool _isAntiSleepActive;

    public TasksViewModel(DataService ds, TaskSchedulerService ts)
    { _dataService = ds; _taskSchedulerService = ts;
      _taskSchedulerService.TaskExecuted += OnTaskExecuted; _taskSchedulerService.TaskCancelled += OnTaskCancelled; }

    [RelayCommand]
    private async Task LoadTasksAsync()
    { var saved = await _dataService.LoadTasksAsync();
      Tasks = new(_taskSchedulerService.ActiveTasks.Concat(saved).DistinctBy(t => t.Id));
      IsAntiSleepActive = _taskSchedulerService.IsAntiSleepActive; }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;
        TaskItem task;
        if (NewTaskType == TaskType.AntiSleep) task = _taskSchedulerService.CreateAntiSleepTask();
        else if (NewScheduleType == TaskScheduleType.Delayed)
            task = _taskSchedulerService.CreateDelayedTask(NewTaskName, NewTaskType, NewDelayMinutes, NewTargetApp);
        else task = _taskSchedulerService.CreateTimedTask(NewTaskName, NewTaskType, NewScheduledTime, NewTargetApp);
        Tasks.Add(task);
        await SaveTasksAsync();
        NewTaskName = ""; NewTargetApp = ""; IsAddingNew = false;
        IsAntiSleepActive = _taskSchedulerService.IsAntiSleepActive;
    }

    [RelayCommand]
    private async Task CancelTaskAsync(string taskId)
    { _taskSchedulerService.CancelTask(taskId); var t = Tasks.FirstOrDefault(t => t.Id == taskId);
      if (t != null) Tasks.Remove(t); await SaveTasksAsync(); }

    [RelayCommand]
    private async Task DelayTaskAsync(string taskId)
    { _taskSchedulerService.DelayTask(taskId, 10); await SaveTasksAsync(); }

    [RelayCommand]
    private void ToggleAntiSleep()
    { if (IsAntiSleepActive) _taskSchedulerService.StopAntiSleep(); else _taskSchedulerService.CreateAntiSleepTask();
      IsAntiSleepActive = _taskSchedulerService.IsAntiSleepActive; }

    private async void OnTaskExecuted(object? s, TaskItem t) { Tasks.Remove(t); await SaveTasksAsync(); }
    private async void OnTaskCancelled(object? s, TaskItem t) { Tasks.Remove(t); await SaveTasksAsync(); }
    private async Task SaveTasksAsync() => await _dataService.SaveTasksAsync(Tasks.ToList());
}
