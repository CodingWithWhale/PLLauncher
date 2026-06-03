using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Models;
using PLLauncher.ViewModels;
using System;

namespace PLLauncher.Views;

public partial class AppUsagePage : UserControl
{
    public AppUsagePage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
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

    private static string FormatTotalTime(double minutes)
    {
        if (minutes < 1) return "<1 min";
        if (minutes < 60) return $"{Math.Floor(minutes)} min";
        var hours = Math.Floor(minutes / 60);
        var mins = Math.Floor(minutes % 60);
        return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
    }
}
