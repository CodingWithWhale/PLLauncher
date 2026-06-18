using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PLLauncher.Helpers;
using PLLauncher.Models;
using AppTaskStatus = PLLauncher.Models.TaskStatus;

namespace PLLauncher.Services;

public class TaskSchedulerService : IDisposable
{
    private readonly List<TaskItem> _activeTasks = new();
    private readonly NotificationService _notificationService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // Track which tasks have already triggered a warning popup (so we don't spam)
    private readonly HashSet<string> _warnedTasks = new();

    public event EventHandler<TaskItem>? TaskExecuted;
    public event EventHandler<TaskItem>? TaskCancelled;
    public event EventHandler<TaskItem>? TaskWarning;

    public IReadOnlyList<TaskItem> ActiveTasks => _activeTasks.AsReadOnly();

    public TaskSchedulerService(NotificationService notificationService)
        => _notificationService = notificationService;

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new();
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try { await ProcessTasksAsync(); await Task.Delay(1000, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    public void Stop() { _isRunning = false; _cts?.Cancel(); _cts?.Dispose(); _cts = null; }

    public void AddTask(TaskItem task) { task.Status = AppTaskStatus.Pending; _activeTasks.Add(task); }

    public void RemoveTask(string taskId)
    {
        var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null) { task.Status = AppTaskStatus.Cancelled; _activeTasks.Remove(task); TaskCancelled?.Invoke(this, task); }
    }

    public void CancelTask(string taskId)
    {
        var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.Status = AppTaskStatus.Cancelled; _activeTasks.Remove(task); TaskCancelled?.Invoke(this, task);
            _warnedTasks.Remove(taskId);
            _notificationService.ShowNotification("Task Cancelled", $"Task '{task.Name}' has been cancelled.");
        }
    }

    public void DelayTask(string taskId, double delayMinutes)
    {
        var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.ScheduledTime = task.ScheduledTime.AddMinutes(delayMinutes);
            task.DelayMinutes += delayMinutes;
            _notificationService.ShowNotification("Task Delayed",
                $"Task '{task.Name}' delayed by {delayMinutes} min. New time: {task.ScheduledTime:HH:mm}");
        }
    }

    public TaskItem CreateDelayedTask(string name, TaskType type, double delayMinutes, string? targetApp = null)
    {
        var task = new TaskItem { Name = name, TaskType = type, ScheduleType = TaskScheduleType.Delayed,
            ScheduledTime = DateTime.Now.AddMinutes(delayMinutes), DelayMinutes = delayMinutes,
            TargetApp = targetApp ?? "", Status = AppTaskStatus.Pending };
        AddTask(task); return task;
    }

    public TaskItem CreateTimedTask(string name, TaskType type, DateTime scheduledTime, string? targetApp = null)
    {
        var task = new TaskItem { Name = name, TaskType = type, ScheduleType = TaskScheduleType.SpecificTime,
            ScheduledTime = scheduledTime, TargetApp = targetApp ?? "", Status = AppTaskStatus.Pending };
        AddTask(task); return task;
    }

    private async Task ProcessTasksAsync()
    {
        var now = DateTime.Now;
        // Read the global warning time setting (in minutes). 0 = no warning.
        double warningMinutes = App.SettingsViewModel.TaskWarningMinutes;

        foreach (var task in _activeTasks.ToList())
        {
            if (task.Status != AppTaskStatus.Pending && task.Status != AppTaskStatus.Running) continue;

            var remaining = task.ScheduledTime - now;

            // Task has reached its scheduled time — execute it
            if (remaining.TotalSeconds <= 0)
            {
                await ExecuteTaskAsync(task);
                continue;
            }

            // Warning check: fire once when the task enters the warning window
            if (warningMinutes > 0 && !_warnedTasks.Contains(task.Id))
            {
                if (remaining.TotalMinutes <= warningMinutes)
                {
                    _warnedTasks.Add(task.Id);
                    TaskWarning?.Invoke(this, task);
                    _notificationService.ShowNotification("Task Warning",
                        $"{task.TaskType} in {Math.Ceiling(remaining.TotalMinutes)} min(s).");

                    // Show the interactive popup on the UI thread
                    ShowWarningPopup(task, remaining.TotalMinutes);
                }
            }
        }
    }

    /// <summary>
    /// Shows a TaskWarningPopup on the UI thread. Must be thread-safe.
    /// </summary>
    private void ShowWarningPopup(TaskItem task, double remainingMinutes)
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Avalonia.Application.Current;
                    if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        && desktop.MainWindow is MainWindow mainWindow)
                    {
                        var popup = new Views.TaskWarningPopup(task, this, remainingMinutes);
                        popup.Snoozed += (s, e) =>
                        {
                            _warnedTasks.Remove(task.Id);
                        };
                        popup.Discarded += (s, e) =>
                        {
                            _warnedTasks.Remove(task.Id);
                        };
                        popup.Show(mainWindow);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TaskScheduler] ShowWarningPopup error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TaskScheduler] Dispatcher error: {ex.Message}");
        }
    }

    private async Task ExecuteTaskAsync(TaskItem task)
    {
        try
        {
            task.Status = AppTaskStatus.Running;
            switch (task.TaskType)
            {
                case TaskType.Shutdown:
                    NativeMethods.EnableShutdownPrivilege();
                    NativeMethods.ExitWindowsEx(NativeMethods.EWX_SHUTDOWN | NativeMethods.EWX_POWEROFF | NativeMethods.EWX_FORCE, 0);
                    break;
                case TaskType.Restart:
                    NativeMethods.EnableShutdownPrivilege();
                    NativeMethods.ExitWindowsEx(NativeMethods.EWX_REBOOT | NativeMethods.EWX_FORCE, 0);
                    break;
                case TaskType.Sleep: NativeMethods.SetSuspendState(false, false, false); break;
                case TaskType.LockPC: NativeMethods.LockWorkStation(); break;
                case TaskType.OpenApp:
                    if (!string.IsNullOrEmpty(task.TargetApp))
                        Process.Start(new ProcessStartInfo(task.TargetApp) { UseShellExecute = true });
                    break;
                case TaskType.CloseApp:
                    if (!string.IsNullOrEmpty(task.TargetApp))
                        foreach (var p in System.Diagnostics.Process.GetProcessesByName(task.TargetApp))
                        { p.CloseMainWindow(); if (!p.WaitForExit(3000)) p.Kill(); }
                    break;
            }
            task.Status = AppTaskStatus.Completed;
            task.LastExecutedAt = DateTime.Now;
            TaskExecuted?.Invoke(this, task);
        }
        catch { task.Status = AppTaskStatus.Failed; }
        _warnedTasks.Remove(task.Id);
        _activeTasks.Remove(task);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
