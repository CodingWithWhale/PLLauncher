using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Services;
using System;

namespace PLLauncher.Views;

public partial class SettingsPage : UserControl
{
    private bool _isLoading = true;

    public SettingsPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoading = true;
        try
        {
            await App.SettingsViewModel.LoadSettingsCommand.ExecuteAsync(null);
            var vm = App.SettingsViewModel;

            StartupToggle.IsChecked = vm.LaunchOnStartup;
            TrayToggle.IsChecked = vm.MinimizeToTray;
            NotificationsToggle.IsChecked = vm.ShowNotifications;
            DarkModeToggle.IsChecked = vm.DarkMode;
            AnimationsToggle.IsChecked = vm.EnableAnimations;
            PerformanceToggle.IsChecked = vm.PerformanceMode;
            WarningTimeBox.Value = (decimal)vm.TaskWarningMinutes;
            CooldownBox.Value = (decimal)vm.TimeLimitCooldownHours;

            SelectLanguageCombo(vm.Language);
            LocalizationService.Instance.LoadFromSettings(vm.Language);
            ApplyLocalizedText();

            App.SetTheme(vm.DarkMode);
            App.AnimationsEnabled = vm.EnableAnimations;
            MarkUnsaved(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Load error: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SelectLanguageCombo(string languageCode)
    {
        var normalized = LocalizationService.NormalizeLanguage(languageCode);
        for (int i = 0; i < LanguageCombo.Items.Count; i++)
        {
            if (LanguageCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == normalized)
            {
                LanguageCombo.SelectedIndex = i;
                return;
            }
        }
        LanguageCombo.SelectedIndex = 0;
    }

    private string GetSelectedLanguageCode()
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string code)
            return code;
        return "en-US";
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        SettingsTitle.Text = loc.Get("settings.title");
        SettingsSubtitle.Text = loc.Get("settings.subtitle");
        LanguageSectionTitle.Text = loc.Get("settings.language");
        LanguageLabel.Text = loc.Get("settings.language");
        GeneralSectionTitle.Text = loc.Get("settings.general");
        AppearanceSectionTitle.Text = loc.Get("settings.appearance");
        PerformanceSectionTitle.Text = loc.Get("settings.performance");
        DataSectionTitle.Text = loc.Get("settings.data");
        SaveButtonText.Text = loc.Get("settings.save");
        DiscardButtonText.Text = loc.Get("settings.discard");
        if (UnsavedHint.IsVisible)
            UnsavedHint.Text = loc.Get("settings.unsaved");
    }

    private void MarkUnsaved(bool unsaved)
    {
        UnsavedHint.Text = LocalizationService.Instance.Get("settings.unsaved");
        UnsavedHint.IsVisible = unsaved;
        DiscardButton.IsVisible = unsaved;
    }

    private void OnSettingChanged()
    {
        if (_isLoading) return;
        MarkUnsaved(true);
    }

    private void DarkModeToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void AnimationsToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void StartupToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void TrayToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void NotificationsToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void PerformanceToggle_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();
    private void WarningTimeBox_Changed(object? sender, NumericUpDownValueChangedEventArgs e) => OnSettingChanged();
    private void CooldownBox_Changed(object? sender, NumericUpDownValueChangedEventArgs e) => OnSettingChanged();
    private void LanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => OnSettingChanged();

    private async void DiscardChanges_Click(object? s, RoutedEventArgs e)
    {
        _isLoading = true;
        try
        {
            var vm = App.SettingsViewModel;
            await vm.LoadSettingsCommand.ExecuteAsync(null);
            StartupToggle.IsChecked = vm.LaunchOnStartup;
            TrayToggle.IsChecked = vm.MinimizeToTray;
            NotificationsToggle.IsChecked = vm.ShowNotifications;
            DarkModeToggle.IsChecked = vm.DarkMode;
            AnimationsToggle.IsChecked = vm.EnableAnimations;
            PerformanceToggle.IsChecked = vm.PerformanceMode;
            WarningTimeBox.Value = (decimal)vm.TaskWarningMinutes;
            CooldownBox.Value = (decimal)vm.TimeLimitCooldownHours;
            SelectLanguageCombo(vm.Language);
            LocalizationService.Instance.LoadFromSettings(vm.Language);
            MarkUnsaved(false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void SaveSettings_Click(object? s, RoutedEventArgs e)
    {
        try
        {
            var vm = App.SettingsViewModel;
            vm.LaunchOnStartup = StartupToggle.IsChecked ?? true;
            vm.MinimizeToTray = TrayToggle.IsChecked ?? true;
            vm.ShowNotifications = NotificationsToggle.IsChecked ?? true;
            vm.DarkMode = DarkModeToggle.IsChecked ?? true;
            vm.EnableAnimations = AnimationsToggle.IsChecked ?? true;
            vm.PerformanceMode = PerformanceToggle.IsChecked ?? false;
            vm.TaskWarningMinutes = (double)(WarningTimeBox.Value ?? 0);
            vm.TimeLimitCooldownHours = (double)(CooldownBox.Value ?? 0);
            vm.Language = GetSelectedLanguageCode();

            await vm.SaveSettingsCommand.ExecuteAsync(null);

            LocalizationService.Instance.LoadFromSettings(vm.Language);
            ApplyLocalizedText();

            try { App.SetTheme(vm.DarkMode); }
            catch (Exception ex) { Console.WriteLine($"[Settings] Theme switch error: {ex.Message}"); }
            App.AnimationsEnabled = vm.EnableAnimations;

            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow)
                mainWindow.ApplyLocalization();

            StatusMsg.Text = LocalizationService.Instance.Get("settings.saved");
            StatusMsg.IsVisible = true;
            MarkUnsaved(false);
        }
        catch (Exception ex)
        {
            StatusMsg.Text = $"Save failed: {ex.Message}";
            StatusMsg.IsVisible = true;
        }
    }

    private async void ExportConfig_Click(object? s, RoutedEventArgs e)
    {
        await App.SettingsViewModel.ExportConfigCommand.ExecuteAsync(null);
        StatusMsg.Text = App.SettingsViewModel.StatusMessage;
        StatusMsg.IsVisible = true;
    }

    private async void ImportConfig_Click(object? s, RoutedEventArgs e)
    {
        await App.SettingsViewModel.ImportConfigCommand.ExecuteAsync(null);
        _isLoading = true;
        var vm = App.SettingsViewModel;
        StartupToggle.IsChecked = vm.LaunchOnStartup;
        TrayToggle.IsChecked = vm.MinimizeToTray;
        NotificationsToggle.IsChecked = vm.ShowNotifications;
        DarkModeToggle.IsChecked = vm.DarkMode;
        AnimationsToggle.IsChecked = vm.EnableAnimations;
        PerformanceToggle.IsChecked = vm.PerformanceMode;
        WarningTimeBox.Value = (decimal)vm.TaskWarningMinutes;
        CooldownBox.Value = (decimal)vm.TimeLimitCooldownHours;
        SelectLanguageCombo(vm.Language);
        LocalizationService.Instance.LoadFromSettings(vm.Language);
        _isLoading = false;
        App.SetTheme(vm.DarkMode);
        App.AnimationsEnabled = vm.EnableAnimations;
        ApplyLocalizedText();
        StatusMsg.Text = App.SettingsViewModel.StatusMessage;
        StatusMsg.IsVisible = true;
        MarkUnsaved(false);
    }

    private async void ResetSettings_Click(object? s, RoutedEventArgs e)
    {
        await App.SettingsViewModel.ResetSettingsCommand.ExecuteAsync(null);
        _isLoading = true;
        var vm = App.SettingsViewModel;
        StartupToggle.IsChecked = vm.LaunchOnStartup;
        TrayToggle.IsChecked = vm.MinimizeToTray;
        NotificationsToggle.IsChecked = vm.ShowNotifications;
        DarkModeToggle.IsChecked = vm.DarkMode;
        AnimationsToggle.IsChecked = vm.EnableAnimations;
        PerformanceToggle.IsChecked = vm.PerformanceMode;
        WarningTimeBox.Value = (decimal)vm.TaskWarningMinutes;
        CooldownBox.Value = (decimal)vm.TimeLimitCooldownHours;
        SelectLanguageCombo(vm.Language);
        LocalizationService.Instance.LoadFromSettings(vm.Language);
        _isLoading = false;
        App.SetTheme(vm.DarkMode);
        App.AnimationsEnabled = vm.EnableAnimations;
        ApplyLocalizedText();
        StatusMsg.Text = "All data reset.";
        StatusMsg.IsVisible = true;
        MarkUnsaved(false);
    }
}
