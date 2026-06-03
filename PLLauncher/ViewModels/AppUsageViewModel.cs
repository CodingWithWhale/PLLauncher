using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class AppUsageViewModel : ObservableObject
{
    private readonly AppUsageTrackingService _usageService;

    [ObservableProperty] private ObservableCollection<AppUsageSummary> _topApps = new();
    [ObservableProperty] private UsageTimeFilter _selectedFilter = UsageTimeFilter.Today;
    [ObservableProperty] private string _filterLabel = "Today";
    [ObservableProperty] private double _totalMinutes;
    [ObservableProperty] private int _totalApps;
    [ObservableProperty] private int _totalSessions;
    [ObservableProperty] private bool _isCustomPeriod;
    [ObservableProperty] private DateTime _customFromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _customToDate = DateTime.Today;
    [ObservableProperty] private string _periodDescription = string.Empty;

    public AppUsageViewModel(AppUsageTrackingService usageService)
    {
        _usageService = usageService;
    }

    [RelayCommand]
    private Task LoadUsageAsync()
    {
        RefreshData();
        return Task.CompletedTask;
    }

    public void SetFilter(UsageTimeFilter filter)
    {
        SelectedFilter = filter;
        IsCustomPeriod = filter == UsageTimeFilter.Custom;
        FilterLabel = filter switch
        {
            UsageTimeFilter.Today => "Today",
            UsageTimeFilter.Yesterday => "Yesterday",
            UsageTimeFilter.ThisWeek => "This Week",
            UsageTimeFilter.ThisMonth => "This Month",
            UsageTimeFilter.ThisYear => "This Year",
            UsageTimeFilter.Custom => "Custom",
            _ => "Today"
        };
        RefreshData();
    }

    public void SetCustomPeriod(DateTime from, DateTime to)
    {
        CustomFromDate = from;
        CustomToDate = to;
        if (SelectedFilter == UsageTimeFilter.Custom)
            RefreshData();
    }

    public void RefreshData()
    {
        try
        {
            var (from, to) = GetDateRange();
            var summaries = _usageService.GetUsageSummaries(from, to);

            TopApps = new(summaries);
            TotalMinutes = summaries.Sum(s => s.TotalMinutes);
            TotalApps = summaries.Count;
            TotalSessions = summaries.Sum(s => s.TotalSessions);

            PeriodDescription = SelectedFilter switch
            {
                UsageTimeFilter.Today => $"Usage for today ({from:MMM d})",
                UsageTimeFilter.Yesterday => $"Usage for yesterday ({from:MMM d})",
                UsageTimeFilter.ThisWeek => $"This week (Mon {from:MMM d} - Sun {to:MMM d})",
                UsageTimeFilter.ThisMonth => $"This month ({from:MMM yyyy})",
                UsageTimeFilter.ThisYear => $"This year ({from:yyyy})",
                UsageTimeFilter.Custom => $"{from:MMM d, yyyy} - {to:MMM d, yyyy}",
                _ => ""
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppUsageVM] Refresh error: {ex.Message}");
        }
    }

    private (DateTime from, DateTime to) GetDateRange()
    {
        var today = DateTime.Today;
        var installDate = _usageService.InstallDate;

        return SelectedFilter switch
        {
            UsageTimeFilter.Today => (today, today),
            UsageTimeFilter.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            UsageTimeFilter.ThisWeek => (StartOfWeek(today), EndOfWeek(today)),
            UsageTimeFilter.ThisMonth => (new(today.Year, today.Month, 1), today),
            UsageTimeFilter.ThisYear => (new(today.Year, 1, 1), today),
            UsageTimeFilter.Custom => (CustomFromDate.Date, CustomToDate.Date),
            _ => (today, today)
        };
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.AddDays(-diff).Date;
    }

    private static DateTime EndOfWeek(DateTime dt)
    {
        return StartOfWeek(dt).AddDays(6);
    }
}
