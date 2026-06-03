using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PLLauncher.Models;
using PLLauncher.Helpers;

namespace PLLauncher.Services;

public class ScheduleService : IDisposable
{
    private readonly List<ScheduleItem> _schedules = new();
    private readonly NotificationService _notificationService;
    private readonly ProcessMonitorService _processMonitor;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public event EventHandler<ScheduleItem>? ScheduleExecuted;
    public IReadOnlyList<ScheduleItem> Schedules => _schedules.AsReadOnly();

    public ScheduleService(NotificationService ns, ProcessMonitorService pm)
    { _notificationService = ns; _processMonitor = pm; }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true; _cts = new();
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try { await ProcessSchedulesAsync(); await Task.Delay(30000, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, _cts.Token);
    }

    public void Stop() { _isRunning = false; _cts?.Cancel(); _cts?.Dispose(); _cts = null; }
    public void AddSchedule(ScheduleItem schedule) => _schedules.Add(schedule);
    public void RemoveSchedule(string scheduleId) { _schedules.RemoveAll(s => s.Id == scheduleId); }
    public void LoadSchedules(IEnumerable<ScheduleItem> schedules) { _schedules.Clear(); _schedules.AddRange(schedules); }

    /// <summary>
    /// Gets all schedule items that fall on a specific date, for calendar display.
    /// </summary>
    public List<ScheduleItem> GetSchedulesForDate(DateTime date)
    {
        return _schedules.Where(s =>
        {
            // If the item has a specific scheduled date, check if it matches
            if (s.ScheduledDate.HasValue)
                return s.ScheduledDate.Value.Date == date.Date;

            // For recurring items, check if this day of week is in the recurring days
            if (s.IsRecurring && s.RecurringDays.Contains(date.DayOfWeek))
                return true;

            // For weekday recurrence
            if (s.Recurrence == ScheduleRecurrence.Weekday &&
                date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                return true;

            // For daily recurrence
            if (s.Recurrence == ScheduleRecurrence.Daily)
                return true;

            return false;
        }).OrderBy(s => s.Time).ToList();
    }

    private async Task ProcessSchedulesAsync()
    {
        var now = DateTime.Now;
        var today = now.Date;
        foreach (var s in _schedules.Where(s => s.IsEnabled).ToList())
        {
            var currentTime = TimeSpan.FromHours(now.Hour) + TimeSpan.FromMinutes(now.Minute);
            var diff = Math.Abs((currentTime - s.Time).TotalMinutes);
            if (diff > 1) continue;
            if (s.LastExecutedAt.HasValue && (now - s.LastExecutedAt.Value).TotalMinutes < 2) continue;

            // Check date: if ScheduledDate is set, only fire on that exact date
            if (s.ScheduledDate.HasValue && s.ScheduledDate.Value.Date != today)
                continue;

            if (!ShouldExecute(s, now)) continue;
            await ExecuteScheduleAsync(s);
        }
    }

    private bool ShouldExecute(ScheduleItem s, DateTime now) => s.Recurrence switch
    {
        ScheduleRecurrence.Once => !s.LastExecutedAt.HasValue,
        ScheduleRecurrence.Daily => true,
        ScheduleRecurrence.Weekday => now.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
        ScheduleRecurrence.Weekly => s.RecurringDays.Contains(now.DayOfWeek),
        ScheduleRecurrence.Custom => s.RecurringDays.Contains(now.DayOfWeek),
        _ => false
    };

    private async Task ExecuteScheduleAsync(ScheduleItem s)
    {
        try
        {
            switch (s.ActionType)
            {
                case ScheduleActionType.Reminder:
                    var urgencyPrefix = s.Urgency switch
                    {
                        ReminderUrgency.High => "🔴 High Priority",
                        ReminderUrgency.Medium => "🟡 Medium Priority",
                        _ => "🟢 Low Priority"
                    };
                    _notificationService.ShowNotification(
                        $"{urgencyPrefix}: {s.Name}",
                        string.IsNullOrEmpty(s.ReminderMessage) ? "Reminder!" : s.ReminderMessage);

                    // Show a reminder popup on the UI thread
                    ShowReminderPopup(s);
                    break;

                case ScheduleActionType.OpenApp:
                    if (!string.IsNullOrEmpty(s.ActionTarget)) _processMonitor.LaunchApp(s.ActionTarget); break;
                case ScheduleActionType.CloseApp:
                    if (!string.IsNullOrEmpty(s.ActionTarget)) _processMonitor.TerminateProcess(s.ActionTarget); break;
                case ScheduleActionType.RunCommand:
                    if (!string.IsNullOrEmpty(s.ActionTarget))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
                            { Arguments = $"/c {s.ActionTarget}", UseShellExecute = false, CreateNoWindow = true }); break;
                case ScheduleActionType.Shutdown:
                    NativeMethods.EnableShutdownPrivilege();
                    NativeMethods.ExitWindowsEx(NativeMethods.EWX_SHUTDOWN | NativeMethods.EWX_POWEROFF, 0); break;
                case ScheduleActionType.Restart:
                    NativeMethods.EnableShutdownPrivilege();
                    NativeMethods.ExitWindowsEx(NativeMethods.EWX_REBOOT, 0); break;
                case ScheduleActionType.Sleep: NativeMethods.SetSuspendState(false, false, false); break;
                case ScheduleActionType.LockPC: NativeMethods.LockWorkStation(); break;
            }
            s.LastExecutedAt = DateTime.Now;
            ScheduleExecuted?.Invoke(this, s);
            if (s.ActionType != ScheduleActionType.Reminder)
                _notificationService.ShowNotification("Schedule Executed", $"'{s.Name}' executed.");
            if (s.Recurrence == ScheduleRecurrence.Once) _schedules.Remove(s);
        }
        catch { _notificationService.ShowError("Schedule Failed", $"Failed: '{s.Name}'"); }
    }

    private void ShowReminderPopup(ScheduleItem s)
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
                        var popup = new Views.ReminderPopup(s);
                        popup.Show(mainWindow);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduleService] ShowReminderPopup error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScheduleService] Dispatcher error: {ex.Message}");
        }
    }

    public void Dispose() { Stop(); GC.SuppressFinalize(this); }
}
