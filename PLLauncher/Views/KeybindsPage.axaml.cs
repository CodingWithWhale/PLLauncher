using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PLLauncher.Models;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher.Views;

public partial class KeybindsPage : UserControl
{
    private bool _isLoaded;
    private bool _isRecordingKeys;
    private string _recordedKeyCombo = "";
    private List<AppInfo>? _installedApps;

    public KeybindsPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    /// <summary>
    /// Get a brush from Application resources (theme-aware).
    /// </summary>
    private Avalonia.Media.IBrush GetResourceBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources[key] is Avalonia.Media.IBrush brush)
            return brush;
        return Avalonia.Media.Brushes.Transparent;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            await App.KeybindsViewModel.LoadKeybindsCommand.ExecuteAsync(null);
            RefreshList();
        }
        catch (Exception ex) { Console.WriteLine($"Keybinds load error: {ex.Message}"); }
    }

    private void RefreshList()
    {
        KeybindsList.ItemsSource = null;
        KeybindsList.ItemsSource = App.KeybindsViewModel.Keybinds;
    }

    // === Key Combo Recording ===

    private void KeyComboBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isRecordingKeys = true;
        KeyComboText.Text = "Press your key combo...";
        KeyComboText.Foreground = GetResourceBrush("AccentBrush");
        KeyComboBorder.BorderBrush = GetResourceBrush("AccentBrush");
        KeyComboBorder.Focus();
        e.Handled = true;
    }

    private void KeyComboBorder_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecordingKeys) return;
        e.Handled = true;

        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (e.Key == Key.Escape)
        {
            _isRecordingKeys = false;
            UpdateKeyComboDisplay();
            return;
        }

        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");

        var keyName = FormatKeyName(e.Key);
        if (!string.IsNullOrEmpty(keyName))
        {
            parts.Add(keyName);
            _recordedKeyCombo = string.Join("+", parts);
            _isRecordingKeys = false;
            UpdateKeyComboDisplay();
        }
    }

    private string FormatKeyName(Key key)
    {
        var name = key.ToString();
        if (name.StartsWith("D") && name.Length == 2 && char.IsDigit(name[1]))
            return name[1].ToString();

        return key switch
        {
            Key.Return => "Enter", Key.Escape => "Esc", Key.Back => "Backspace",
            Key.Delete => "Delete", Key.Insert => "Insert", Key.Home => "Home",
            Key.End => "End", Key.PageUp => "PageUp", Key.PageDown => "PageDown",
            Key.Up => "Up", Key.Down => "Down", Key.Left => "Left", Key.Right => "Right",
            Key.Space => "Space", Key.Tab => "Tab", Key.CapsLock => "CapsLock",
            Key.NumLock => "NumLock", Key.Scroll => "ScrollLock",
            Key.PrintScreen => "PrintScreen", Key.Pause => "Pause",
            Key.OemTilde => "`", Key.OemMinus => "-", Key.OemPlus => "=",
            Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\", Key.OemSemicolon => ";", Key.OemQuotes => "'",
            Key.OemComma => ",", Key.OemPeriod => ".", Key.OemQuestion => "/",
            _ => name
        };
    }

    private void UpdateKeyComboDisplay()
    {
        if (!string.IsNullOrEmpty(_recordedKeyCombo))
        {
            KeyComboText.Text = _recordedKeyCombo;
            KeyComboText.Foreground = GetResourceBrush("TextPrimaryBrush");
        }
        else
        {
            KeyComboText.Text = "Click to record keys...";
            KeyComboText.Foreground = GetResourceBrush("TextTertiaryBrush");
        }
        KeyComboBorder.BorderBrush = GetResourceBrush("BorderBrush");
    }

    // === App Picker ===

    private void ActionTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            if (ActionTypeCombo.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                bool showAppPicker = tag is "OpenApp" or "OpenFolder";
                AppPickerCombo.IsVisible = showAppPicker;

                TargetBox.Watermark = tag switch
                {
                    "OpenApp" => "App path (or select from list above)",
                    "RunCommand" => "Command to run",
                    "SystemAction" => "Action: mute, lockpc, sleep, hibernate",
                    "OpenUrl" => "URL to open",
                    "OpenFolder" => "Folder path (or select from list above)",
                    _ => "Target"
                };

                if (showAppPicker) LoadInstalledApps();
            }
        }
        catch (Exception ex) { Console.WriteLine($"ActionType error: {ex.Message}"); }
    }

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
                var actionTag = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                // For OpenFolder, use ExecutablePath's directory; for OpenApp, use ExecutablePath
                if (!string.IsNullOrEmpty(app.ExecutablePath))
                    TargetBox.Text = app.ExecutablePath;
            }
        }
        catch (Exception ex) { Console.WriteLine($"AppPicker error: {ex.Message}"); }
    }

    // === Panel Controls ===

    private void AddKeybind_Click(object? s, RoutedEventArgs e)
    {
        AddPanel.IsVisible = true;
        _recordedKeyCombo = "";
        UpdateKeyComboDisplay();
        _installedApps = null;
        NameBox.Focus();
        // Set default action type to OpenApp
        ActionTypeCombo.SelectedIndex = 0;
        LoadInstalledApps();
        AppPickerCombo.IsVisible = true;
    }

    private void CancelAdd_Click(object? s, RoutedEventArgs e)
    {
        AddPanel.IsVisible = false;
        _recordedKeyCombo = "";
        _isRecordingKeys = false;
        UpdateKeyComboDisplay();
    }

    private async void SaveKeybind_Click(object? s, RoutedEventArgs e)
    {
        var vm = App.KeybindsViewModel;
        vm.NewKeyName = NameBox.Text ?? "";
        vm.NewKeyCombo = _recordedKeyCombo;
        vm.NewActionTarget = TargetBox.Text ?? "";
        vm.NewActionType = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        { "OpenApp" => KeybindActionType.OpenApp, "RunCommand" => KeybindActionType.RunCommand,
          "SystemAction" => KeybindActionType.SystemAction, "OpenUrl" => KeybindActionType.OpenUrl,
          "OpenFolder" => KeybindActionType.OpenFolder, _ => KeybindActionType.OpenApp };

        if (string.IsNullOrWhiteSpace(vm.NewKeyName) || string.IsNullOrWhiteSpace(vm.NewKeyCombo)) return;

        await vm.AddKeybindCommand.ExecuteAsync(null);
        if (vm.HasConflict) { ConflictMsg.Text = vm.ConflictMessage; ConflictMsg.IsVisible = true; }
        else
        {
            AddPanel.IsVisible = false;
            NameBox.Text = ""; _recordedKeyCombo = ""; TargetBox.Text = "";
            UpdateKeyComboDisplay();
            ConflictMsg.IsVisible = false;
            RefreshList();
        }
    }

    private async void DeleteKeybind_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            var kb = App.KeybindsViewModel.Keybinds.FirstOrDefault(k => k.Id == id);
            if (kb != null)
            {
                await App.KeybindsViewModel.DeleteKeybindCommand.ExecuteAsync(kb);
                RefreshList();
            }
        }
    }

    private void SearchBox_TextChanged(object? s, TextChangedEventArgs e)
    {
        App.KeybindsViewModel.SearchText = SearchBox.Text ?? "";
        KeybindsList.ItemsSource = null;
        KeybindsList.ItemsSource = App.KeybindsViewModel.FilteredKeybinds;
    }
}
