using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    private bool _launchOnStartup = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private bool _darkMode = true;

    [ObservableProperty]
    private bool _performanceMode = false;

    [ObservableProperty]
    private double _taskWarningMinutes = 1;

    [ObservableProperty]
    private double _timeLimitCooldownHours = 12;

    [ObservableProperty]
    private bool _enableSoundEffects = true;

    [ObservableProperty]
    private bool _enableAnimations = true;

    [ObservableProperty]
    private string _language = "en-US";

    [ObservableProperty]
    private double _antiSleepIntervalSeconds = 60;

    [ObservableProperty]
    private string _lastExportPath = string.Empty;

    [ObservableProperty]
    private DateTime? _lastBackupDate;
}
