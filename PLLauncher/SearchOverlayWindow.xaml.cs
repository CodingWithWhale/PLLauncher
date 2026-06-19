using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using PLLauncher.Helpers;
using PLLauncher.Models;
using PLLauncher.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher;

public partial class SearchOverlayWindow : Window
{
    private enum SearchMode { Actions, AppSearch }
    private SearchMode _currentSearchMode = SearchMode.Actions;
    private readonly List<SearchAction> _searchActions = new();
    private List<AppInfo>? _installedApps;

    public SearchOverlayWindow()
    {
        InitializeComponent();
    }

    public void ShowSearch()
    {
        _currentSearchMode = SearchMode.Actions;
        BuildSearchActions();
        SearchTextBox.Text = "";
        SearchTextBox.Watermark = "Search actions...";
        SearchResultsList.ItemsSource = null;
        _ = SearchTextBox.Focus();
    }

    private void BuildSearchActions()
    {
        _searchActions.Clear();
        _searchActions.Add(new("Open App...", "\uE8F1", "open start launch run program app", SwitchToAppSearchMode, ClosesSearch: false));
        _searchActions.Add(new("Lock PC", "\uE72E", "lock", () => { Close(); NativeMethods.LockWorkStation(); }));
        _searchActions.Add(new("Shutdown 1h", "\uE7E8", "shutdown", () =>
        {
            Close();
            _ = App.TaskSchedulerService.CreateDelayedTask("Quick Shutdown", Models.TaskType.Shutdown, 60);
        }));
        _searchActions.Add(new("Dashboard", "\uE80F", "home dashboard", () => { Close(); NavigateToPage("Dashboard"); }));
        _searchActions.Add(new("Keybinds", "\uE92E", "keybinds keys hotkeys", () => { Close(); NavigateToPage("Keybinds"); }));
        _searchActions.Add(new("Tasks", "\uE916", "tasks", () => { Close(); NavigateToPage("Tasks"); }));
        _searchActions.Add(new("Time Limits", "\uE917", "time limits", () => { Close(); NavigateToPage("TimeLimits"); }));
        _searchActions.Add(new("Scheduler", "\uE787", "scheduler schedule", () => { Close(); NavigateToPage("Scheduler"); }));
        _searchActions.Add(new("Setups", "\uE8F1", "setups groups", () => { Close(); NavigateToPage("Setups"); }));
        _searchActions.Add(new("Pomodoro", "\uE917", "pomodoro timer focus", () => { Close(); NavigateToPage("Pomodoro"); }));
        _searchActions.Add(new("App Usage", "\uE9D9", "usage app", () => { Close(); NavigateToPage("AppUsage"); }));
        _searchActions.Add(new("Settings", "\uE713", "settings config", () => { Close(); NavigateToPage("Settings"); }));
    }

    private void SwitchToAppSearchMode()
    {
        _currentSearchMode = SearchMode.AppSearch;
        _installedApps ??= App.InstalledAppsService.GetInstalledApps();
        SearchTextBox.Text = "";
        SearchTextBox.Watermark = "Search apps...";
        FilterSearchResults();
        _ = SearchTextBox.Focus();
    }

    private void SwitchToActionMode()
    {
        _currentSearchMode = SearchMode.Actions;
        SearchTextBox.Watermark = "Search actions...";
        SearchTextBox.Text = "";
        SearchResultsList.SelectedIndex = -1;
        SearchResultsList.ItemsSource = null;
        BuildSearchActions();
        _ = SearchTextBox.Focus();
    }

    private void LaunchApp(string exePath)
    {
        try
        {
            Close();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath, UseShellExecute = true
            });
        }
        catch (Exception ex) { Console.WriteLine($"[OverlaySearch] Launch failed: {ex.Message}"); }
    }

    private void NavigateToPage(string tag)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is MainWindow main)
            main.NavigateToPage(tag);
    }

    private void FilterSearchResults()
    {
        var query = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";

        if (_currentSearchMode == SearchMode.AppSearch)
        {
            var results = new List<SearchAction>
            {
                new("← Back", "\uE72B", "", SwitchToActionMode)
            };
            var apps = _installedApps ??= App.InstalledAppsService.GetInstalledApps();
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(query) ||
                    app.DisplayName.ToLowerInvariant().Contains(query))
                {
                    var capturedPath = app.ExecutablePath;
                    results.Add(new(app.DisplayName, "\uE8F1", "",
                        () => LaunchApp(capturedPath)));
                }
            }
            SearchResultsList.ItemsSource = results;
            return;
        }

        if (string.IsNullOrEmpty(query))
        {
            SearchResultsList.ItemsSource = null;
            return;
        }
        var actionResults = _searchActions
            .Where(a => a.Keywords.Contains(query) || a.Title.ToLowerInvariant().Contains(query))
            .ToList();
        SearchResultsList.ItemsSource = actionResults;
    }

    private void ExecuteSearchAction(SearchAction? action)
    {
        if (action == null) return;
        if (!action.ClosesSearch)
        {
            action.Action();
            return;
        }
        Close();
        action.Action();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e) => FilterSearchResults();

    private void SearchTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            if (_currentSearchMode == SearchMode.AppSearch)
                SwitchToActionMode();
            else
                Close();
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Enter && SearchResultsList.SelectedItem is SearchAction sa)
        {
            if (_currentSearchMode == SearchMode.AppSearch && SearchResultsList.SelectedIndex == 0)
                SwitchToActionMode();
            else
                ExecuteSearchAction(sa);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Down)
        {
            if (SearchResultsList.ItemCount > 0)
            {
                SearchResultsList.SelectedIndex = SearchResultsList.SelectedIndex < 0
                    ? 0 : Math.Min(SearchResultsList.SelectedIndex + 1, SearchResultsList.ItemCount - 1);
                SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Up)
        {
            if (SearchResultsList.SelectedIndex > 0)
            {
                SearchResultsList.SelectedIndex--;
                SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
            }
            e.Handled = true;
        }
    }

    private void SearchResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is SearchAction sa)
        {
            if (_currentSearchMode == SearchMode.AppSearch && SearchResultsList.SelectedIndex == 0)
            {
                SwitchToActionMode();
                return;
            }
            ExecuteSearchAction(sa);
        }
    }

    private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        Close();
    }

    private void SearchCard_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Prevent click on the search card from closing the window
    }
}
