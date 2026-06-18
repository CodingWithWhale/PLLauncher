using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Models;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;

namespace PLLauncher.Views;

public partial class AppUsagePage : UserControl
{
    public AppUsagePage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyLocalizedText();
        RefreshView();
    }

    private void Filter_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            var filter = Enum.Parse<UsageTimeFilter>(tag);
            var vm = App.AppUsageViewModel;

            vm.SetFilter(filter);
            CustomDatePanel.IsVisible = filter == UsageTimeFilter.Custom;
            RefreshView();
        }
    }

    private void ApplyCustomFilter_Click(object? sender, RoutedEventArgs e)
    {
        var vm = App.AppUsageViewModel;
        var from = CustomFromDate.SelectedDate ?? DateTime.Today.AddDays(-7);
        var to = CustomToDate.SelectedDate ?? DateTime.Today;
        vm.SetCustomPeriod(from, to);
        RefreshView();
    }

    private void RefreshBtn_Click(object? sender, RoutedEventArgs e)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        try
        {
            var vm = App.AppUsageViewModel;
            vm.RefreshData();

            TopAppsList.ItemsSource = vm.TopApps;
            TotalTimeText.Text = FormatTotalTime(vm.TotalMinutes);
            TotalAppsText.Text = vm.TotalApps.ToString();
            TotalSessionsText.Text = vm.TotalSessions.ToString();
            PeriodDesc.Text = vm.PeriodDescription;

            EmptyState.IsVisible = vm.TopApps.Count == 0;
            TopAppsList.IsVisible = vm.TopApps.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppUsagePage] Refresh error: {ex.Message}");
        }
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        AppUsageTitle.Text = loc.Get("appusage.title");
        AppUsageSubtitle.Text = loc.Get("appusage.subtitle");
        TimePeriodLabel.Text = loc.Get("appusage.time_period");
        FilterToday.Content = loc.Get("appusage.filter_today");
        FilterYesterday.Content = loc.Get("appusage.filter_yesterday");
        FilterThisWeek.Content = loc.Get("appusage.filter_thisweek");
        FilterThisMonth.Content = loc.Get("appusage.filter_thismonth");
        FilterThisYear.Content = loc.Get("appusage.filter_thisyear");
        FilterCustom.Content = loc.Get("appusage.filter_custom");
        FromLabel.Text = loc.Get("appusage.from");
        ToLabel.Text = loc.Get("appusage.to");
        ApplyFilterBtn.Content = loc.Get("appusage.apply");
        RefreshBtnText.Text = loc.Get("appusage.refresh");
        TotalTimeLabel.Text = loc.Get("appusage.total_time");
        TotalAppsLabel.Text = loc.Get("appusage.apps_used");
        TotalSessionsLabel.Text = loc.Get("appusage.sessions_label");
        EmptyTitle.Text = loc.Get("appusage.no_data");
        EmptyDesc.Text = loc.Get("appusage.no_data_desc");
    }

    private string FormatTotalTime(double minutes)
    {
        var loc = LocalizationService.Instance;
        if (minutes < 1) return loc.Get("appusage.less_1min");
        if (minutes < 60) return $"{Math.Floor(minutes)} {loc.Get("appusage.min")}";
        var hours = Math.Floor(minutes / 60);
        var mins = Math.Floor(minutes % 60);
        if (mins > 0)
            return $"{hours}h {mins}{loc.Get("appusage.min").Substring(0, 1)}";
        return $"{hours}h";
    }
}
