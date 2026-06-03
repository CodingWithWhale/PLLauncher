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
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { Console.WriteLine($"Dashboard load error: {ex.Message}"); }
    }

    private Window? GetOwnerWindow()
        => TopLevel.GetTopLevel(this) as Window;

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var vm = App.DashboardViewModel;
        await vm.RefreshCommand.ExecuteAsync(null);
        GreetingText.Text = vm.Greeting;
        HotkeyCount.Text = vm.ActiveHotkeyCount.ToString();
        TaskCount.Text = vm.ActiveTaskCount.ToString();
        LimitCount.Text = vm.ActiveTimeLimitsCount.ToString();
        ScheduleCount.Text = vm.ActiveScheduleCount.ToString();
        SystemStatusText.Text = vm.SystemStatus;
        AntiSleepText.Text = vm.IsAntiSleepActive ? "Stop Anti-Sleep" : "Anti-Sleep";
    }

    private async void ToggleAntiSleep_Click(object? sender, RoutedEventArgs e)
    {
        var vm = App.DashboardViewModel;
        if (vm.IsAntiSleepActive)
        {
            vm.ToggleAntiSleepCommand.Execute(null);
            AntiSleepText.Text = "Anti-Sleep";
            await RefreshAsync();
            return;
        }

        var loc = LocalizationService.Instance;
        var confirmed = await DialogHelper.ShowConfirmAsync(
            GetOwnerWindow(),
            loc.Get("confirm.antisleep"),
            loc.Get("confirm.title"));

        if (!confirmed) return;

        vm.ToggleAntiSleepCommand.Execute(null);
        AntiSleepText.Text = vm.IsAntiSleepActive ? "Stop Anti-Sleep" : "Anti-Sleep";
        await RefreshAsync();
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
