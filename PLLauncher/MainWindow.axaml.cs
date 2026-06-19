using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PLLauncher.Helpers;
using PLLauncher.Models;
using PLLauncher.Services;
using PLLauncher.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PLLauncher;

public partial class MainWindow : Window
{
    private bool _isInitialized;
    private bool _isNavigating;

    public MainWindow()
    {
        InitializeComponent();
        _isInitialized = true;
        ApplyLocalization();
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalization();
        NavigateToPage("Dashboard");

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
            VersionText.Text = $"PLLauncher v{version.Major}.{version.Minor}.{version.Build}";

        // Use dynamically generated icon matching current accent color
        var accentSecondary = (Color)(App.Current?.Resources["AccentColorSecondary"] ?? Color.FromRgb(0x00, 0x78, 0xD4));
        Icon = App.GenerateAppIcon(accentSecondary);
    }

    private void ApplySearchLocalization()
    {
        var loc = LocalizationService.Instance;
        var hotkey = App.SettingsViewModel.SearchHotkey;
        SearchHintText.Text = string.IsNullOrEmpty(hotkey)
            ? loc.Get("search.hint")
            : loc.Get("search.hint").Replace("Ctrl+K", hotkey);
        SearchTextBox.Watermark = _currentSearchMode == SearchMode.AppSearch
            ? loc.Get("search.placeholder_apps")
            : loc.Get("search.placeholder");
    }

    public void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        var tags = new[] { "Dashboard", "Keybinds", "Tasks", "TimeLimits", "Scheduler", "Setups", "Pomodoro", "AppUsage", "Settings" };
        var keys = new[] { "nav.dashboard", "nav.keybinds", "nav.tasks", "nav.timelimits", "nav.scheduler", "nav.setups", "nav.pomodoro", "nav.appusage", "nav.settings" };

        for (int i = 0; i < NavList.Items.Count && i < tags.Length; i++)
        {
            if (NavList.Items[i] is ListBoxItem item && item.Tag?.ToString() == tags[i])
            {
                var label = item.GetVisualDescendants().OfType<TextBlock>()
                    .LastOrDefault(tb => tb.Parent is StackPanel);
                if (label != null)
                    label.Text = loc.Get(keys[i]);
            }
        }
        ApplySearchLocalization();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!App._isShuttingDown && e.CloseReason == WindowCloseReason.WindowClosing && App.SettingsViewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        // For ApplicationExit/real shutdown, proceed with closing
        base.OnClosing(e);
    }

    // Search bar
    private enum SearchMode { Actions, AppSearch }
    private SearchMode _currentSearchMode = SearchMode.Actions;
    private readonly List<SearchAction> _searchActions = new();
    private List<Services.AppInfo>? _installedApps;
    private int _searchHotkeyId = -1;
    private SearchOverlayWindow? _searchOverlay;

    private void BuildSearchActions()
    {
        _searchActions.Clear();
        _searchActions.Add(new("Open App...", "\uE8F1", "open start launch run program app", SwitchToAppSearchMode, ClosesSearch: false));
        _searchActions.Add(new("Lock PC", "\uE72E", "lock", () => NativeMethods.LockWorkStation()));
        _searchActions.Add(new("Shutdown 1h", "\uE7E8", "shutdown", () =>
        {
            _ = App.TaskSchedulerService.CreateDelayedTask("Quick Shutdown", Models.TaskType.Shutdown, 60);
        }));
        _searchActions.Add(new("Dashboard", "\uE80F", "home dashboard", () => NavigateToPage("Dashboard")));
        _searchActions.Add(new("Keybinds", "\uE92E", "keybinds keys hotkeys", () => NavigateToPage("Keybinds")));
        _searchActions.Add(new("Tasks", "\uE916", "tasks", () => NavigateToPage("Tasks")));
        _searchActions.Add(new("Time Limits", "\uE917", "time limits", () => NavigateToPage("TimeLimits")));
        _searchActions.Add(new("Scheduler", "\uE787", "scheduler schedule", () => NavigateToPage("Scheduler")));
        _searchActions.Add(new("Setups", "\uE8F1", "setups groups", () => NavigateToPage("Setups")));
        _searchActions.Add(new("Pomodoro", "\uE917", "pomodoro timer focus", () => NavigateToPage("Pomodoro")));
        _searchActions.Add(new("App Usage", "\uE9D9", "usage app", () => NavigateToPage("AppUsage")));
        _searchActions.Add(new("Settings", "\uE713", "settings config", () => NavigateToPage("Settings")));
    }

    private void SwitchToAppSearchMode()
    {
        _currentSearchMode = SearchMode.AppSearch;
        _installedApps ??= App.InstalledAppsService.GetInstalledApps();
        SearchTextBox.Text = "";
        SearchTextBox.Watermark = LocalizationService.Instance.Get("search.placeholder_apps");
        FilterSearchResults();
        _ = SearchTextBox.Focus();
    }

    private void SwitchToActionMode()
    {
        _currentSearchMode = SearchMode.Actions;
        SearchTextBox.Watermark = LocalizationService.Instance.Get("search.placeholder");
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath, UseShellExecute = true
            });
        }
        catch (Exception ex) { Console.WriteLine($"[Search] Launch failed: {ex.Message}"); }
    }

    private void ShowSearch()
    {
        _currentSearchMode = SearchMode.Actions;
        BuildSearchActions();
        SearchOverlay.IsVisible = true;
        SearchTextBox.Text = "";
        SearchTextBox.Watermark = LocalizationService.Instance.Get("search.placeholder");
        SearchResultsList.ItemsSource = null;
        _ = SearchTextBox.Focus();
    }

    private void HideSearch()
    {
        _currentSearchMode = SearchMode.Actions;
        SearchOverlay.IsVisible = false;
        SearchTextBox.Text = "";
        SearchResultsList.ItemsSource = null;
    }

    private void FilterSearchResults()
    {
        var query = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";

        if (_currentSearchMode == SearchMode.AppSearch)
        {
            var results = new List<SearchAction>
            {
                new("← Back", "\uE72B", "",
                    SwitchToActionMode)
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
        HideSearch();
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
                HideSearch();
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
                // "Back to actions" — don't hide search, just switch mode
                SwitchToActionMode();
                return;
            }
            ExecuteSearchAction(sa);
        }
    }

    private void SearchOverlaySelf_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Clicking the backdrop dismisses
        if (e.Source is Border && e.Source == SearchOverlay)
            HideSearch();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle != null)
            {
                var hwnd = platformHandle.Handle;
                Console.WriteLine($"[MainWindow] HWND={hwnd}");

                App.HotkeyService.Initialize(hwnd);
                App.HotkeyService.SubclassWindow(hwnd);

                _ = RegisterAllHotkeysAsync();

                // Register search hotkey (saved in settings)
                _ = LoadAndRegisterSearchHotkeyAsync();
            }
            else
            {
                Console.WriteLine("[MainWindow] WARNING: Could not get platform handle. Hotkeys will not work.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] OnOpened error: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task LoadAndRegisterSearchHotkeyAsync()
    {
        try
        {
            var settings = await App.DataService.LoadSettingsAsync();
            var savedHotkey = settings.SearchHotkey ?? "Ctrl+K";
            _searchHotkeyId = App.HotkeyService.RegisterAppHotkey(savedHotkey, () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(ShowSearchGlobal);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Failed to load search hotkey: {ex.Message}");
        }
    }

    private void ShowSearchGlobal()
    {
        if (this.IsVisible && this.WindowState != WindowState.Minimized)
        {
            // Main window is visible — use in-window overlay
            if (SearchOverlay.IsVisible) HideSearch();
            else ShowSearch();
            if (WindowState == WindowState.Normal) Activate();
        }
        else
        {
            // Main window is hidden — show system overlay
            if (_searchOverlay == null || !_searchOverlay.IsVisible)
            {
                _searchOverlay = new SearchOverlayWindow();
                _searchOverlay.Show();
                _searchOverlay.ShowSearch();
            }
            else
            {
                _searchOverlay.Close();
                _searchOverlay = null;
            }
        }
    }

    public void ReRegisterSearchHotkey(string keyCombo)
    {
        App.HotkeyService.UnregisterAppHotkey(_searchHotkeyId);
        if (!string.IsNullOrEmpty(keyCombo))
        {
            _searchHotkeyId = App.HotkeyService.RegisterAppHotkey(keyCombo, () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(ShowSearchGlobal);
            });
        }
        else
        {
            _searchHotkeyId = -1;
        }
    }

    private async System.Threading.Tasks.Task RegisterAllHotkeysAsync()
    {
        try
        {
            var keybinds = await App.DataService.LoadKeybindsAsync();
            App.HotkeyService.RegisterAll(keybinds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Hotkey registration error: {ex.Message}");
        }
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isNavigating) return;
        try
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ListBoxItem item && item.Tag is string tag)
                NavigateToPage(tag);
        }
        catch (Exception ex) { Console.WriteLine($"Navigation error: {ex.Message}"); }
    }

    public void NavigateToPage(string pageTag)
    {
        _isNavigating = true;
        try
        {
            UserControl page = pageTag switch
            {
                "Dashboard" => new DashboardPage(),
                "Keybinds" => new KeybindsPage(),
                "Tasks" => new TasksPage(),
                "TimeLimits" => new TimeLimitsPage(),
                "Scheduler" => new SchedulerPage(),
                "Setups" => new SetupsPage(),
                "Pomodoro" => new PomodoroPage(),
                "AppUsage" => new AppUsagePage(),
                "Settings" => new SettingsPage(),
                _ => new DashboardPage()
            };

            if (App.AnimationsEnabled)
            {
                var animation = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(250),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0),
                            Setters = { new Setter(OpacityProperty, 0.0) }
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1),
                            Setters = { new Setter(OpacityProperty, 1.0) }
                        }
                    }
                };
                ContentArea.Content = page;
                animation.RunAsync(page);
            }
            else
            {
                ContentArea.Content = page;
            }

            for (int i = 0; i < NavList.Items.Count; i++)
            {
                if (NavList.Items[i] is ListBoxItem item && item.Tag?.ToString() == pageTag)
                { NavList.SelectedIndex = i; break; }
            }
        }
        finally { _isNavigating = false; }
    }
}
