using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Models;
using PLLauncher.Services;
using System;

namespace PLLauncher.Views;

public partial class TaskWarningPopup : Window
{
    private readonly TaskItem _task;
    private readonly TaskSchedulerService _taskSchedulerService;
    private readonly double _remainingMinutes;

    public event EventHandler? Snoozed;
    public event EventHandler? Discarded;

    public TaskWarningPopup() : this(new TaskItem(), null!, 1) { }

    public TaskWarningPopup(TaskItem task, TaskSchedulerService taskSchedulerService, double remainingMinutes)
    {
        InitializeComponent();

        _task = task;
        _taskSchedulerService = taskSchedulerService;
        _remainingMinutes = remainingMinutes;

        // Fill in task details
        TaskNameText.Text = task.Name;
        TaskTypeText.Text = task.TaskType.ToString();
        TaskTimeText.Text = $"at {task.ScheduledTime:HH:mm}";

        if (remainingMinutes < 1)
            TimeRemainingText.Text = "Less than 1 minute remaining!";
        else
            TimeRemainingText.Text = $"{Math.Ceiling(remainingMinutes)} minute(s) remaining";

        // Position in top-right corner of the primary screen
        PositionInTopRight();
    }

    private void PositionInTopRight()
    {
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var wa = screen.WorkingArea;
                var x = wa.X + wa.Width - (int)Width - 20;
                var y = wa.Y + 20;
                Position = new PixelPoint(x, y);
            }
        }
        catch { }
    }

    private void Snooze_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Delay the task by 10 minutes
            _taskSchedulerService.DelayTask(_task.Id, 10);
            Snoozed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TaskWarningPopup] Snooze error: {ex.Message}");
        }
        Close();
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Cancel the task entirely
            _taskSchedulerService.CancelTask(_task.Id);
            Discarded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TaskWarningPopup] Discard error: {ex.Message}");
        }
        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Auto-close after 5 minutes if no action is taken
        AutoCloseAfterDelay();
    }

    private async void AutoCloseAfterDelay()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(5));
            if (this.IsVisible)
                Close();
        }
        catch { }
    }
}
