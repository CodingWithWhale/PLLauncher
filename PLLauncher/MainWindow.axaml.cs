using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PLLauncher.Helpers;
using PLLauncher.Services;
using PLLauncher.Views;
using System;
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

        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new WindowIcon(iconPath);
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
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            // Get the Win32 window handle
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle != null)
            {
                var hwnd = platformHandle.Handle;
                Console.WriteLine($"[MainWindow] HWND={hwnd}");

                // Initialize the hotkey service with the real window handle
                App.HotkeyService.Initialize(hwnd);

                // Subclass the window to intercept WM_HOTKEY messages
                App.HotkeyService.SubclassWindow(hwnd);

                // Register all saved hotkeys now that we have a valid window handle
                _ = RegisterAllHotkeysAsync();
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
                    Duration = TimeSpan.FromMilliseconds(150),
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
