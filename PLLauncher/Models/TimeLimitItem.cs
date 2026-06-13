using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class TimeLimitItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private string _processName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsagePercentage))]
    [NotifyPropertyChangedFor(nameof(RemainingMinutes))]
    private double _dailyLimitMinutes = 120;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsagePercentage))]
    [NotifyPropertyChangedFor(nameof(RemainingMinutes))]
    private double _usedMinutesToday = 0;

    [ObservableProperty]
    private bool _isLocked = false;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private DateTime _lastResetDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _lockedAt;

    [ObservableProperty]
    private DateTime? _cooldownEndAt;

    [ObservableProperty]
    private bool _isInCooldown = false;

    [ObservableProperty]
    private double _cooldownHours = 12;

    [ObservableProperty]
    private string _appIconPath = string.Empty;

    public double RemainingMinutes => DailyLimitMinutes - UsedMinutesToday;
    
    public double UsagePercentage => DailyLimitMinutes > 0 
        ? Math.Min(100, (UsedMinutesToday / DailyLimitMinutes) * 100) 
        : 0;
}
