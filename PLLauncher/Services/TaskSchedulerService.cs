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

    // Anti-sleep state
    private bool _antiSleepSystemActive;
    private NativeMethods.POINT _antiSleepLastCursorPos;
    private DateTime _ignoreMovementUntil = DateTime.MinValue;

    // Track which tasks have already triggered a warning popup (so we don't spam)
    private readonly HashSet<string> _warnedTasks = new();

    public event EventHandler<TaskItem>? TaskExecuted;
    public event EventHandler<TaskItem>? TaskCancelled;
    public event EventHandler<TaskItem>? TaskWarning;

    public IReadOnlyList<TaskItem> ActiveTasks => _activeTasks.AsReadOnly();
    public bool IsAntiSleepActive => _activeTasks.Any(t => t.IsAntiSleep && t.Status == AppTaskStatus.Running);

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

    public TaskItem CreateAntiSleepTask()
    {
        var task = new TaskItem { Name = "Anti-Sleep Mode", TaskType = TaskType.AntiSleep,
            ScheduleType = TaskScheduleType.Delayed, ScheduledTime = DateTime.Now.AddHours(8),
            IsAntiSleep = true, AntiSleepIntervalSeconds = 60, Status = AppTaskStatus.Running };
        AddTask(task);

        // Use SetThreadExecutionState to actually prevent Windows from sleeping
        // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        // This tells Windows: keep system awake, keep display on, persist until we clear it
        NativeMethods.SetThreadExecutionState(
            NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);
        _antiSleepSystemActive = true;

        NativeMethods.GetCursorPos(out _antiSleepLastCursorPos);
        _ignoreMovementUntil = DateTime.Now.AddMilliseconds(500);
        task.AntiSleepIntervalSeconds = 15;

        _notificationService.ShowNotification("Anti-Sleep Mode",
            "Anti-sleep activated. Move your mouse to stop it.");
        return task;
    }

    public void StopAntiSleep()
    {
        var t = _activeTasks.FirstOrDefault(t => t.IsAntiSleep);
        if (t != null)
        {
            t.Status = AppTaskStatus.Cancelled;
            _activeTasks.Remove(t);

            // Clear the execution state to allow Windows to sleep again
            // ES_CONTINUOUS alone resets the state
            if (_antiSleepSystemActive)
            {
                NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
                _antiSleepSystemActive = false;
            }

            _notificationService.ShowNotification("Anti-Sleep Mode", "Anti-sleep mode deactivated.");
        }
    }

    private async Task ProcessTasksAsync()
    {
        var now = DateTime.Now;
        // Read the global warning time setting (in minutes). 0 = no warning.
        double warningMinutes = App.SettingsViewModel.TaskWarningMinutes;

        foreach (var task in _activeTasks.ToList())
        {
            if (task.Status != AppTaskStatus.Pending && task.Status != AppTaskStatus.Running) continue;

            if (task.IsAntiSleep)
            {
                if (task.Status == AppTaskStatus.Running)
                {
                    if (CheckUserMovedMouse())
                    {
                        StopAntiSleep();
                        continue;
                    }
                    ProcessAntiSleep(task);
                }
                continue;
            }

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
                case TaskType.Hibernate: NativeMethods.SetSuspendState(true, false, false); break;
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
                case TaskType.RunCommand:
                    if (!string.IsNullOrEmpty(task.TargetApp))
                        Process.Start(new ProcessStartInfo("cmd.exe")
                            { Arguments = $"/c {task.TargetApp}", UseShellExecute = false, CreateNoWindow = true });
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

    private bool CheckUserMovedMouse()
    {
        if (DateTime.Now < _ignoreMovementUntil) return false;

        try
        {
            NativeMethods.GetCursorPos(out var pos);
            var dx = Math.Abs(pos.X - _antiSleepLastCursorPos.X);
            var dy = Math.Abs(pos.Y - _antiSleepLastCursorPos.Y);
            return dx > 4 || dy > 4;
        }
        catch { return false; }
    }

    private void ProcessAntiSleep(TaskItem task)
    {
        if ((DateTime.Now - (task.LastExecutedAt ?? DateTime.MinValue)).TotalSeconds < task.AntiSleepIntervalSeconds)
            return;

        NativeMethods.SetThreadExecutionState(
            NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);

        try
        {
            NativeMethods.GetCursorPos(out var pos);
            NativeMethods.SetCursorPos(pos.X + 2, pos.Y);
            Thread.Sleep(40);
            NativeMethods.SetCursorPos(pos.X, pos.Y + 2);
            Thread.Sleep(40);
            NativeMethods.SetCursorPos(pos.X, pos.Y);
            _antiSleepLastCursorPos = pos;
            _ignoreMovementUntil = DateTime.Now.AddMilliseconds(500);
        }
        catch { }

        task.LastExecutedAt = DateTime.Now;
    }

    public void Dispose()
    {
        // Make sure to clear anti-sleep state on exit
        if (_antiSleepSystemActive)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _antiSleepSystemActive = false;
        }
        Stop();
        GC.SuppressFinalize(this);
    }
}
