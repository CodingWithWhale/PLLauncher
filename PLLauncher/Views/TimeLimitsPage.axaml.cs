using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;
using System.Collections.Generic;

namespace PLLauncher.Views;

public partial class TimeLimitsPage : UserControl
{
    private bool _isLoaded;
    private List<AppInfo>? _installedApps;

    public TimeLimitsPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            await App.TimeLimitsViewModel.LoadTimeLimitsCommand.ExecuteAsync(null);
            RefreshList();
        }
        catch (Exception ex) { Console.WriteLine($"TimeLimits load error: {ex.Message}"); }
        ApplyLocalizedText();
    }

    private void RefreshList()
    {
        LimitsList.ItemsSource = null;
        LimitsList.ItemsSource = App.TimeLimitsViewModel.TimeLimits;
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        TitleText.Text = loc.Get("timelimits.title");
        SubtitleText.Text = loc.Get("timelimits.subtitle");
        AddLimitBtnText.Text = loc.Get("timelimits.add_limit");
        NewLimitTitle.Text = loc.Get("timelimits.new_limit");
        AppNameBox.Watermark = loc.Get("timelimits.app_name");
        ProcessNameBox.Watermark = loc.Get("timelimits.process_name");
        DailyLimitLabel.Text = loc.Get("timelimits.daily_limit");
        CooldownWarningText.Text = loc.Get("timelimits.cooldown_warning");
        CancelBtn.Content = loc.Get("confirm.cancel");
        SaveLimitBtn.Content = loc.Get("timelimits.save_limit");
    }

    // === App Picker ===

    private void LoadInstalledApps()
    {
        try
        {
            App.InstalledAppsService.RefreshCache();
            _installedApps = App.InstalledAppsService.GetInstalledApps();
            AppPickerCombo.ItemsSource = _installedApps;
        }
        catch (Exception ex) { Console.WriteLine($"Failed to load apps: {ex.Message}"); }
    }

    private void AppPickerCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            if (AppPickerCombo.SelectedItem is AppInfo app)
            {
                if (!string.IsNullOrEmpty(app.DisplayName))
                    AppNameBox.Text = app.DisplayName;
                if (!string.IsNullOrEmpty(app.ProcessName))
                    ProcessNameBox.Text = app.ProcessName;
            }
        }
        catch (Exception ex) { Console.WriteLine($"AppPicker error: {ex.Message}"); }
    }

    // === Panel Controls ===

    private void AddLimit_Click(object? s, RoutedEventArgs e)
    {
        AddLimitPanel.IsVisible = true;
        _installedApps = null;
        LoadInstalledApps();
        AppNameBox.Focus();
    }

    private void CancelLimit_Click(object? s, RoutedEventArgs e) => AddLimitPanel.IsVisible = false;

    private async void SaveLimit_Click(object? s, RoutedEventArgs e)
    {
        var vm = App.TimeLimitsViewModel;
        vm.NewAppName = AppNameBox.Text ?? ""; vm.NewProcessName = ProcessNameBox.Text ?? "";
        vm.NewDailyLimitMinutes = (double)(DailyLimitBox.Value ?? 120);
        await vm.AddTimeLimitCommand.ExecuteAsync(null);
        AddLimitPanel.IsVisible = false;
        RefreshList();
    }

    private async void DisableLimit_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            var limit = App.TimeLimitsViewModel.TimeLimits.FirstOrDefault(l => l.Id == id);
            if (limit != null)
            {
                await App.TimeLimitsViewModel.DisableTimeLimitCommand.ExecuteAsync(limit);
                RefreshList();
            }
        }
    }

    private async void DeleteLimit_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            var limit = App.TimeLimitsViewModel.TimeLimits.FirstOrDefault(l => l.Id == id);
            if (limit != null)
            {
                await App.TimeLimitsViewModel.DeleteTimeLimitCommand.ExecuteAsync(limit);
                RefreshList();
            }
        }
    }
}
