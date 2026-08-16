using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PLLauncher.Helpers;
using PLLauncher.Services;
using System;
using System.Collections.Generic;
using System.Linq;

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


            SelectLanguageCombo(vm.Language);
            LocalizationService.Instance.LoadFromSettings(vm.Language);
            ApplyLocalizedText();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
                VersionDesc.Text = $"PLLauncher v{version.Major}.{version.Minor}.{version.Build}";

            App.SetAccentColor(vm.AccentColor);
            HighlightSelectedAccent(vm.AccentColor);
            SearchHotkeyBox.Text = string.IsNullOrEmpty(vm.SearchHotkey) ? "" : vm.SearchHotkey;
            SearchHotkeyEnabled.IsChecked = !string.IsNullOrEmpty(vm.SearchHotkey);
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

    private void HighlightSelectedAccent(string name)
    {
        foreach (var child in AccentColorsPanel.Children)
        {
            if (child is Border b)
                b.BorderBrush = b.Tag?.ToString() == name
                    ? Brushes.White : Avalonia.Media.Brushes.Transparent;
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

    private string GetSelectedAccentColor()
    {
        foreach (var child in AccentColorsPanel.Children)
        {
            if (child is Border b && b.BorderBrush == Brushes.White && b.Tag is string name)
                return name;
        }
        return "Blue";
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        SettingsTitle.Text = loc.Get("settings.title");
        SettingsSubtitle.Text = loc.Get("settings.subtitle");
        LanguageSectionTitle.Text = loc.Get("settings.language");
        LanguageLabel.Text = loc.Get("settings.language");
        LanguageDesc.Text = loc.Get("settings.language_desc");
        GeneralSectionTitle.Text = loc.Get("settings.general");
        StartupLabel.Text = loc.Get("settings.startup_label");
        StartupDesc.Text = loc.Get("settings.startup_desc");
        TrayLabel.Text = loc.Get("settings.tray_label");
        TrayDesc.Text = loc.Get("settings.tray_desc");
        NotificationsLabel.Text = loc.Get("settings.notifications_label");
        NotificationsDesc.Text = loc.Get("settings.notifications_desc");
        AppearanceSectionTitle.Text = loc.Get("settings.appearance");
        DarkModeLabel.Text = loc.Get("settings.darkmode_label");
        DarkModeDesc.Text = loc.Get("settings.darkmode_desc");
        AnimationsLabel.Text = loc.Get("settings.animations_label");
        AnimationsDesc.Text = loc.Get("settings.animations_desc");
        PerformanceSectionTitle.Text = loc.Get("settings.performance");
        PerformanceLabel.Text = loc.Get("settings.performance_label");
        PerformanceDesc.Text = loc.Get("settings.performance_desc");
        WarningLabel.Text = loc.Get("settings.warning_label");
        WarningDesc.Text = loc.Get("settings.warning_desc");

        DataSectionTitle.Text = loc.Get("settings.data");
        ExportLabel.Text = loc.Get("settings.export_label");
        ExportDesc.Text = loc.Get("settings.export_desc");
        ImportLabel.Text = loc.Get("settings.import_label");
        ImportDesc.Text = loc.Get("settings.import_desc");
        ResetLabel.Text = loc.Get("settings.reset_label");
        ResetDesc.Text = loc.Get("settings.reset_desc");
        ExportButtonText.Text = loc.Get("settings.export_button");
        ImportButtonText.Text = loc.Get("settings.import_button");
        ResetButtonText.Text = loc.Get("settings.reset_button");
        AboutSectionTitle.Text = loc.Get("settings.about");
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = string.Format(loc.Get("settings.version"), version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "?");
        VersionDesc.Text = loc.Get("settings.about");
        CheckUpdatesBtnText.Text = loc.Get("settings.check_updates");
        TermsButtonText.Text = loc.Get("settings.terms");
        PrivacyButtonText.Text = loc.Get("settings.privacy");
        AccentLabel.Text = loc.Get("settings.accent_label");
        SearchHotkeyLabel.Text = loc.Get("settings.search_hotkey");
        SearchHotkeyDesc.Text = loc.Get("settings.search_hotkey_desc");
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
        BottomBar.IsVisible = unsaved;
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
    private void LanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => OnSettingChanged();

    private void SearchHotkeyEnabled_Changed(object? sender, RoutedEventArgs e) => OnSettingChanged();

    private void SearchHotkeyBox_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        SearchHotkeyBox.Text = "";
        SearchHotkeyBox.Focus();
    }

    private void SearchHotkeyBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Back || e.Key == Avalonia.Input.Key.Delete)
            return;
        var parts = new List<string>();
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Meta) != 0) parts.Add("Win");
        var key = e.Key switch
        {
            Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl
                or Avalonia.Input.Key.LeftAlt or Avalonia.Input.Key.RightAlt
                or Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift
                or Avalonia.Input.Key.LWin or Avalonia.Input.Key.RWin => null,
            _ => e.Key.ToString()
        };
        if (key != null)
        {
            parts.Add(key);
            SearchHotkeyBox.Text = string.Join("+", parts);
            OnSettingChanged();
        }
        e.Handled = true;
    }

    private async void CustomAccentColor_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var dialog = new Helpers.ColorPickerDialog();
        await dialog.ShowDialog(owner);
        if (!dialog.IsConfirmed) return;
        var color = dialog.SelectedColor;
        var secondary = Color.FromRgb(
            (byte)Math.Max(0, color.R - 60),
            (byte)Math.Max(0, color.G - 60),
            (byte)Math.Max(0, color.B - 60));
        App.SetCustomAccentColor(color, secondary);
        HighlightSelectedAccent("Custom");
        OnSettingChanged();
    }

    private void AccentColor_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is string name)
        {
            HighlightSelectedAccent(name);
            App.SetAccentColor(name);
            OnSettingChanged();
        }
    }

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
        SelectLanguageCombo(vm.Language);
            LocalizationService.Instance.LoadFromSettings(vm.Language);
            SearchHotkeyBox.Text = string.IsNullOrEmpty(vm.SearchHotkey) ? "" : vm.SearchHotkey;
            SearchHotkeyEnabled.IsChecked = !string.IsNullOrEmpty(vm.SearchHotkey);
            MarkUnsaved(false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void SaveSettings_Click(object? s, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (!await DialogHelper.ShowConfirmAsync(owner, "Apply settings?", "Save changes to settings?"))
            return;

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
            vm.Language = GetSelectedLanguageCode();
            vm.AccentColor = GetSelectedAccentColor();
            var newHotkey = SearchHotkeyEnabled.IsChecked == true ? SearchHotkeyBox.Text?.Trim() ?? "Ctrl+K" : "";

            if (string.IsNullOrEmpty(newHotkey))
            {
                if (!await DialogHelper.ShowConfirmAsync(TopLevel.GetTopLevel(this) as Window, "Disable search hotkey?",
                    "You won't be able to open search via keyboard until you re-enable it in Settings. Continue?"))
                {
                    SearchHotkeyEnabled.IsChecked = true;
                    MarkUnsaved(true);
                    return;
                }
            }
            vm.SearchHotkey = newHotkey;
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            LocalizationService.Instance.LoadFromSettings(vm.Language);
            ApplyLocalizedText();

            try { App.SetTheme(vm.DarkMode); }
            catch (Exception ex) { Console.WriteLine($"[Settings] Theme switch error: {ex.Message}"); }
            App.SetAccentColor(vm.AccentColor);
            App.AnimationsEnabled = vm.EnableAnimations;

            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ApplyLocalization();
                mainWindow.ReRegisterSearchHotkey(vm.SearchHotkey);
            }

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

    private static string SectionToNavKey(string sectionKey) => sectionKey switch
    {
        "schedules" => "scheduler",
        _ => sectionKey
    };

    private async void ExportConfig_Click(object? s, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var loc = LocalizationService.Instance;
        var sectionKeys = new[] { "keybinds", "tasks", "timelimits", "schedules", "setups", "settings" };
        var items = new Dictionary<string, string>();
        foreach (var k in sectionKeys)
            items[k] = loc.Get($"nav.{SectionToNavKey(k)}");

        var dialog = new ChecklistDialog();
        dialog.Configure(loc.Get("settings.export_label"), items);
        await dialog.ShowDialog(owner);
        if (dialog.SelectedItems.Count == 0) return;

        var saveFile = await owner.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = loc.Get("settings.export_label"),
            DefaultExtension = "json",
            FileTypeChoices = new List<Avalonia.Platform.Storage.FilePickerFileType>
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } }
            },
            SuggestedFileName = $"PLLauncher_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        });
        if (saveFile == null) return;
        var path = saveFile.Path.LocalPath;

        StatusMsg.Text = loc.Get("settings.checking");
        StatusMsg.IsVisible = true;

        await App.SettingsViewModel.ExportConfigCommand.ExecuteAsync((path, dialog.SelectedItems));
        StatusMsg.Text = App.SettingsViewModel.StatusMessage;
        StatusMsg.IsVisible = true;
    }

    private async void ImportConfig_Click(object? s, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var loc = LocalizationService.Instance;

        var openFiles = await owner.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = loc.Get("settings.import_label"),
            AllowMultiple = false,
            FileTypeFilter = new List<Avalonia.Platform.Storage.FilePickerFileType>
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } }
            }
        });
        if (openFiles.Count == 0) return;
        var path = openFiles[0].Path.LocalPath;

        var available = await App.DataService.GetImportableSectionsAsync(path);
        if (available.Count == 0)
        {
            StatusMsg.Text = "No importable data found in file.";
            StatusMsg.IsVisible = true;
            return;
        }

        var items = new Dictionary<string, string>();
        foreach (var key in available)
        {
            items[key] = loc.Get($"nav.{SectionToNavKey(key)}");
        }

        var dialog = new ChecklistDialog();
        dialog.Configure(loc.Get("settings.import_label"), items, available);
        await dialog.ShowDialog(owner);
        if (dialog.SelectedItems.Count == 0) return;

        await App.SettingsViewModel.ImportConfigCommand.ExecuteAsync((path, dialog.SelectedItems));
        _isLoading = true;
        var vm = App.SettingsViewModel;
        StartupToggle.IsChecked = vm.LaunchOnStartup;
        TrayToggle.IsChecked = vm.MinimizeToTray;
        NotificationsToggle.IsChecked = vm.ShowNotifications;
        DarkModeToggle.IsChecked = vm.DarkMode;
        AnimationsToggle.IsChecked = vm.EnableAnimations;
        PerformanceToggle.IsChecked = vm.PerformanceMode;
        WarningTimeBox.Value = (decimal)vm.TaskWarningMinutes;
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

    private async void Terms_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var dialog = new Views.DocumentDialog("terms", "terms.title");
        await dialog.ShowDialog(owner);
    }

    private async void Privacy_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var dialog = new Views.DocumentDialog("privacy", "privacy.title");
        await dialog.ShowDialog(owner);
    }

    private async void CheckUpdates_Click(object? s, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        CheckUpdatesBtn.IsEnabled = false;
        StatusMsg.Text = loc.Get("settings.checking");
        StatusMsg.IsVisible = true;

        try
        {
            var update = await App.UpdateService.CheckForUpdatesAsync();
            if (update != null)
            {
                if (await App.UpdateService.PromptUpdateAsync(TopLevel.GetTopLevel(this) as Window))
                {
                    App._isShuttingDown = true;
                    Environment.Exit(0);
                }
                else
                {
                    StatusMsg.Text = string.Format(loc.Get("settings.update_found"), update.Version);
                }
            }
            else
            {
                StatusMsg.Text = loc.Get("settings.no_update");
            }
        }
        catch (Exception ex)
        {
            StatusMsg.Text = $"Error: {ex.Message}";
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            StatusMsg.IsVisible = true;
        }
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
