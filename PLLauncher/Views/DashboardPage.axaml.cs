using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PLLauncher.Helpers;
using PLLauncher.Models;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher.Views;

public partial class DashboardPage : UserControl
{
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _lockCountdownTimer;
    private readonly List<TimeLimitItem> _lockedApps = new();

    public DashboardPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
        StartClock();
        SubscribeToLockEvents();
    }

    private void SubscribeToLockEvents()
    {
        App.TimeTrackingService.AppLocked += OnAppLocked;
        App.TimeTrackingService.CooldownStarted += OnCooldownStarted;
        App.TimeTrackingService.CooldownEnded += OnCooldownEnded;
    }

    private void OnAppLocked(object? sender, TimeLimitItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_lockedApps.Any(l => l.Id == item.Id))
                _lockedApps.Add(item);
            UpdateLockedBanner();
        });
    }

    private void OnCooldownStarted(object? sender, TimeLimitItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_lockedApps.Any(l => l.Id == item.Id))
                _lockedApps.Add(item);
            UpdateLockedBanner();
        });
    }

    private void OnCooldownEnded(object? sender, TimeLimitItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lockedApps.RemoveAll(l => l.Id == item.Id);
            UpdateLockedBanner();
        });
    }

    private void UpdateLockedBanner()
    {
        // Remove expired lock entries
        _lockedApps.RemoveAll(l => !l.IsInCooldown || !l.CooldownEndAt.HasValue || DateTime.Now >= l.CooldownEndAt.Value);

        if (_lockedApps.Count == 0)
        {
            LockedAppBanner.IsVisible = false;
            _lockCountdownTimer?.Stop();
            _lockCountdownTimer = null;
            return;
        }

        LockedAppBanner.IsVisible = true;
        var loc = LocalizationService.Instance;
        var first = _lockedApps[0];
        if (_lockedApps.Count == 1)
        {
            LockedTitle.Text = $"Time Limit for \"{first.AppName}\" Reached";
            LockedDescription.Text = $"The app will be unlocked automatically when the lock period ends.";
        }
        else
        {
            LockedTitle.Text = $"{_lockedApps.Count} Apps Time Limit Reached";
            LockedDescription.Text = string.Join(", ", _lockedApps.Select(l => $"\"{l.AppName}\""));
        }

        UpdateLockCountdown();

        if (_lockCountdownTimer == null)
        {
            _lockCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lockCountdownTimer.Tick += (_, _) => UpdateLockCountdown();
            _lockCountdownTimer.Start();
        }
    }

    private void UpdateLockCountdown()
    {
        _lockedApps.RemoveAll(l => !l.IsInCooldown || !l.CooldownEndAt.HasValue || DateTime.Now >= l.CooldownEndAt.Value);

        if (_lockedApps.Count == 0)
        {
            LockedAppBanner.IsVisible = false;
            _lockCountdownTimer?.Stop();
            _lockCountdownTimer = null;
            return;
        }

        var remaining = _lockedApps[0].CooldownEndAt!.Value - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            LockedCountdown.Text = "Unlocking...";
            return;
        }
        LockedCountdown.Text = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private void StartClock()
    {
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockTime.Text = now.ToString("HH:mm");
        ClockDate.Text = now.ToString("dddd, d MMMM");
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _clockTimer?.Stop();
        _clockTimer = null;
        _lockCountdownTimer?.Stop();
        _lockCountdownTimer = null;
        App.TimeTrackingService.AppLocked -= OnAppLocked;
        App.TimeTrackingService.CooldownStarted -= OnCooldownStarted;
        App.TimeTrackingService.CooldownEnded -= OnCooldownEnded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { Console.WriteLine($"Dashboard load error: {ex.Message}"); }
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        SubtitleText.Text = loc.Get("dashboard.subtitle");
        HotkeysLabel.Text = loc.Get("dashboard.active_hotkeys");
        TasksLabel.Text = loc.Get("dashboard.active_tasks");
        LimitsLabel.Text = loc.Get("dashboard.time_limits");
        SchedulesLabel.Text = loc.Get("dashboard.schedules");
        QuickActionsLabel.Text = loc.Get("dashboard.quick_actions");
        ShutdownText.Text = loc.Get("dashboard.shutdown_1h");
        LockText.Text = loc.Get("dashboard.lock_pc");
        NextTaskTitle.Text = loc.Get("dashboard.next_task");
        var vm = App.DashboardViewModel;
        if (string.IsNullOrEmpty(vm.NextTaskInfo) || vm.NextTaskInfo == "No upcoming tasks")
            NextTaskInfo.Text = loc.Get("dashboard.no_tasks");
        else
            NextTaskInfo.Text = vm.NextTaskInfo;
    }

    private Window? GetOwnerWindow()
        => TopLevel.GetTopLevel(this) as Window;

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var vm = App.DashboardViewModel;
        await vm.RefreshCommand.ExecuteAsync(null);
        ApplyLocalizedText();
        GreetingText.Text = vm.Greeting;
        HotkeyCount.Text = vm.ActiveHotkeyCount.ToString();
        TaskCount.Text = vm.ActiveTaskCount.ToString();
        LimitCount.Text = vm.ActiveTimeLimitsCount.ToString();
        ScheduleCount.Text = vm.ActiveScheduleCount.ToString();
        NextTaskInfo.Text = vm.NextTaskInfo;
    }

    private async void QuickShutdown_Click(object? sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        var confirmed = await DialogHelper.ShowConfirmAsync(
            GetOwnerWindow(),
            loc.Get("confirm.shutdown"),
            loc.Get("confirm.title"));

        if (confirmed)
            App.TaskSchedulerService.CreateDelayedTask("Quick Shutdown", Models.TaskType.Shutdown, 60);
    }

    private async void QuickLock_Click(object? sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        var confirmed = await DialogHelper.ShowConfirmAsync(
            GetOwnerWindow(),
            loc.Get("confirm.lock"),
            loc.Get("confirm.title"));

        if (confirmed)
            NativeMethods.LockWorkStation();
    }

    private void NavigateTo(string pageTag)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow main)
            main.NavigateToPage(pageTag);
    }

    private void HotkeysCard_Click(object? sender, RoutedEventArgs e) => NavigateTo("Keybinds");
    private void TasksCard_Click(object? sender, RoutedEventArgs e) => NavigateTo("Tasks");
    private void TimeLimitsCard_Click(object? sender, RoutedEventArgs e) => NavigateTo("TimeLimits");
    private void SchedulesCard_Click(object? sender, RoutedEventArgs e) => NavigateTo("Scheduler");
}
