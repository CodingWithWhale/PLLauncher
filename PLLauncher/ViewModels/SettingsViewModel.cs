using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private AppSettings _settings = new();

    [ObservableProperty] private bool _launchOnStartup = true;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _darkMode = true;
    [ObservableProperty] private bool _performanceMode;
    [ObservableProperty] private double _taskWarningMinutes = 1;
    [ObservableProperty] private double _timeLimitCooldownHours = 12;
    [ObservableProperty] private bool _enableSoundEffects = true;
    [ObservableProperty] private bool _enableAnimations = true;
    [ObservableProperty] private string _language = "en-US";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(DataService ds) => _dataService = ds;

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        _settings = await _dataService.LoadSettingsAsync();
        LaunchOnStartup = _settings.LaunchOnStartup; MinimizeToTray = _settings.MinimizeToTray;
        ShowNotifications = _settings.ShowNotifications; DarkMode = _settings.DarkMode;
        PerformanceMode = _settings.PerformanceMode; TaskWarningMinutes = _settings.TaskWarningMinutes;
        TimeLimitCooldownHours = _settings.TimeLimitCooldownHours; EnableSoundEffects = _settings.EnableSoundEffects;
        EnableAnimations = _settings.EnableAnimations; Language = _settings.Language;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        _settings.LaunchOnStartup = LaunchOnStartup; _settings.MinimizeToTray = MinimizeToTray;
        _settings.ShowNotifications = ShowNotifications; _settings.DarkMode = DarkMode;
        _settings.PerformanceMode = PerformanceMode; _settings.TaskWarningMinutes = TaskWarningMinutes;
        _settings.TimeLimitCooldownHours = TimeLimitCooldownHours; _settings.EnableSoundEffects = EnableSoundEffects;
        _settings.EnableAnimations = EnableAnimations; _settings.Language = Language;
        await _dataService.SaveSettingsAsync(_settings);
        StatusMessage = "Settings saved successfully!";
    }

    [RelayCommand]
    private async Task ExportConfigAsync()
    { try { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        $"PLLauncher_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await _dataService.ExportAllAsync(path); StatusMessage = $"Exported to {path}"; }
      catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; } }

    [RelayCommand]
    private async Task ImportConfigAsync()
    { try { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "PLLauncher_Backup.json");
        if (System.IO.File.Exists(path)) { await _dataService.ImportAllAsync(path); await LoadSettingsAsync();
            StatusMessage = "Imported successfully!"; }
        else StatusMessage = "No backup found on desktop."; }
      catch (Exception ex) { StatusMessage = $"Import failed: {ex.Message}"; } }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    { await _dataService.ResetAllDataAsync(); await LoadSettingsAsync(); StatusMessage = "All data reset."; }
}
