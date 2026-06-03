using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class AppUsageRecord : ObservableObject
{
    [ObservableProperty]
    private string _processName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private double _totalMinutes = 0;

    [ObservableProperty]
    private int _sessions = 0;

    [ObservableProperty]
    private DateTime _firstSeenAt = DateTime.Now;

    [ObservableProperty]
    private DateTime _lastSeenAt = DateTime.Now;
}

/// <summary>
/// Represents an aggregated usage summary for a single app across a time period.
/// Used for display in the App Usage page.
/// </summary>
public class AppUsageSummary
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double TotalMinutes { get; set; }
    public int TotalSessions { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public string FormattedTime
    {
        get
        {
            if (TotalMinutes < 1) return "<1 min";
            if (TotalMinutes < 60) return $"{Math.Floor(TotalMinutes)} min";
            var hours = Math.Floor(TotalMinutes / 60);
            var mins = Math.Floor(TotalMinutes % 60);
            return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
        }
    }

    public double UsageBarPercent { get; set; }
}

/// <summary>
/// The time filter for the App Usage page.
/// </summary>
public enum UsageTimeFilter
{
    Today,
    Yesterday,
    ThisWeek,
    ThisMonth,
    ThisYear,
    Custom
}
