using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Helpers;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;

namespace PLLauncher.Views;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
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
}
