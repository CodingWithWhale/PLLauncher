using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PLLauncher;

public partial class App : Application
{
    // Single-instance enforcement
    private static Mutex? _singleInstanceMutex;
    internal static bool _isShuttingDown;
    private const string AppGuid = "{B8A3C8E0-4C1A-4F3A-9E2D-7A1B2C3D4E5F}";
    private static readonly string MutexName = $"PLLauncher-{AppGuid}";
    private static readonly string SignalFileName = $"PLLauncher-signal-{AppGuid}.tmp";
    private static string SignalFilePath => Path.Combine(Path.GetTempPath(), SignalFileName);

    // Services (singleton instances)
    public static DataService DataService { get; } = new();
    public static NotificationService NotificationService { get; } = new();
    public static ProcessMonitorService ProcessMonitorService { get; } = new();
    public static HotkeyService HotkeyService { get; } = new();
    public static InstalledAppsService InstalledAppsService { get; } = new();
    public static TaskSchedulerService TaskSchedulerService { get; private set; } = null!;
    public static TimeTrackingService TimeTrackingService { get; private set; } = null!;
    public static ScheduleService ScheduleService { get; private set; } = null!;
    public static SystemTrayService SystemTrayService { get; private set; } = null!;
    public static AppUsageTrackingService AppUsageTrackingService { get; private set; } = null!;
    public static PomodoroService PomodoroService { get; private set; } = null!;
    public static HealthReminderService HealthReminderService { get; private set; } = null!;
    public static UpdateService UpdateService { get; private set; } = null!;

    // GitHub repo info — CHANGE THESE to match your repo
    private const string GitHubOwner = "CodingWithWhale";
    private const string GitHubRepo = "PLLauncher";

    // ViewModels
    public static DashboardViewModel DashboardViewModel { get; private set; } = null!;
    public static KeybindsViewModel KeybindsViewModel { get; private set; } = null!;
    public static TasksViewModel TasksViewModel { get; private set; } = null!;
    public static TimeLimitsViewModel TimeLimitsViewModel { get; private set; } = null!;
    public static SchedulerViewModel SchedulerViewModel { get; private set; } = null!;
    public static SetupsViewModel SetupsViewModel { get; private set; } = null!;
    public static AppUsageViewModel AppUsageViewModel { get; private set; } = null!;
    public static SettingsViewModel SettingsViewModel { get; private set; } = null!;

    // Settings cache
    public static bool AnimationsEnabled { get; set; } = true;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Create all brushes programmatically from the Color resources defined in XAML.
        // This ensures they are mutable SolidColorBrush (not ImmutableSolidColorBrush),
        // so SetTheme() can safely replace them later without casting crashes.
        BuildBrushesFromColors();
    }

    /// <summary>
    /// Creates SolidColorBrush resources from the Color resources in XAML.
    /// Called once during Initialize() before any UI is rendered.
    /// </summary>
    private void BuildBrushesFromColors()
    {
        var r = Resources;
        r["AccentBrush"] = new SolidColorBrush((Color)(r["AccentColor"] ?? Colors.Transparent));
        r["AccentSecondaryBrush"] = new SolidColorBrush((Color)(r["AccentColorSecondary"] ?? Colors.Transparent));
        r["BackgroundBrush"] = new SolidColorBrush((Color)(r["BackgroundColor"] ?? Colors.Transparent));
        r["SurfaceBrush"] = new SolidColorBrush((Color)(r["SurfaceColor"] ?? Colors.Transparent));
        r["Surface2Brush"] = new SolidColorBrush((Color)(r["SurfaceColor2"] ?? Colors.Transparent));
        r["CardBackgroundBrush"] = new SolidColorBrush((Color)(r["CardBackgroundColor"] ?? Colors.Transparent));
        r["SidebarBackgroundBrush"] = new SolidColorBrush((Color)(r["SidebarBackgroundColor"] ?? Colors.Transparent));
        r["TextPrimaryBrush"] = new SolidColorBrush((Color)(r["TextPrimaryColor"] ?? Colors.Transparent));
        r["TextSecondaryBrush"] = new SolidColorBrush((Color)(r["TextSecondaryColor"] ?? Colors.Transparent));
        r["TextTertiaryBrush"] = new SolidColorBrush((Color)(r["TextTertiaryColor"] ?? Colors.Transparent));
        r["BorderBrush"] = new SolidColorBrush((Color)(r["BorderColor"] ?? Colors.Transparent));
        r["SuccessBrush"] = new SolidColorBrush((Color)(r["SuccessColor"] ?? Colors.Transparent));
        r["WarningBrush"] = new SolidColorBrush((Color)(r["WarningColor"] ?? Colors.Transparent));
        r["ErrorBrush"] = new SolidColorBrush((Color)(r["ErrorColor"] ?? Colors.Transparent));
        r["InfoBrush"] = new SolidColorBrush((Color)(r["InfoColor"] ?? Colors.Transparent));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Single-instance check: try to own the mutex
        try
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                try
                {
                    // Try to acquire the mutex (non-blocking)
                    if (_singleInstanceMutex.WaitOne(0))
                    {
                        // Previous instance exited — we own it now.
                    }
                    else
                    {
                        // Mutex held by another running instance — signal it, then exit
                        try { File.WriteAllText(SignalFilePath, DateTime.Now.ToString("O")); } catch { }

                        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                            lifetime.Shutdown(0);
                        return;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Crash-abandoned mutex — we now own it, proceed as first instance
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] Mutex error: {ex.Message}");
        }

        try
        {
            // Initialize services with dependencies
            TaskSchedulerService = new TaskSchedulerService(NotificationService);
            TimeTrackingService = new TimeTrackingService(NotificationService, ProcessMonitorService, DataService);
            ScheduleService = new ScheduleService(NotificationService, ProcessMonitorService);
            SystemTrayService = new SystemTrayService();
            SystemTrayService.Initialize();
            AppUsageTrackingService = new AppUsageTrackingService(DataService, InstalledAppsService);
            PomodoroService = new PomodoroService();
            HealthReminderService = new HealthReminderService(NotificationService);
            UpdateService = new UpdateService(NotificationService, GitHubOwner, GitHubRepo);

            // Initialize ViewModels
            DashboardViewModel = new DashboardViewModel(
                DataService, HotkeyService, TaskSchedulerService, TimeTrackingService, ScheduleService);
            KeybindsViewModel = new KeybindsViewModel(DataService, HotkeyService);
            TasksViewModel = new TasksViewModel(DataService, TaskSchedulerService);
            TimeLimitsViewModel = new TimeLimitsViewModel(
                DataService, TimeTrackingService, ProcessMonitorService);
            SchedulerViewModel = new SchedulerViewModel(DataService, ScheduleService);
            SetupsViewModel = new SetupsViewModel(DataService, ProcessMonitorService);
            AppUsageViewModel = new AppUsageViewModel(AppUsageTrackingService);
            SettingsViewModel = new SettingsViewModel(DataService, SystemTrayService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service init error: {ex.Message}");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            // Load saved data on startup
            _ = LoadSavedDataAsync();

            try
            {
                // Load time limits synchronously before tracking starts
                var savedLimits = Task.Run(() => DataService.LoadTimeLimitsAsync()).GetAwaiter().GetResult() ?? new();
                Console.WriteLine($"[App] Loaded {savedLimits.Count} time limits from disk");
                TimeTrackingService.LoadLimits(savedLimits);
                TimeLimitsViewModel.TimeLimits = new(savedLimits);

                // Start background services
                TaskSchedulerService?.Start();
                TimeTrackingService?.Start();
                ScheduleService?.Start();
                AppUsageTrackingService?.Start();
                // Pomodoro and Health are started manually by the user from the Pomodoro page
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service start error: {ex.Message}");
            }

            // Wire up update check BEFORE the window opens (Opened fires only once)
            desktop.MainWindow.Opened += async (_, _) =>
            {
                await Task.Delay(2000);
                if (await UpdateService.PromptUpdateAsync(desktop.MainWindow))
                    desktop.Shutdown();
            };

            // Wire up tray icon events
            SystemTrayService.ShowWindowRequested += (_, _) => EnsureWindowVisible(desktop);
            SystemTrayService.ExitRequested += (_, _) =>
            {
                _isShuttingDown = true;
                desktop.Shutdown();
            };

            // Listen for signals from other instances to show the window
            _ = ListenForShowWindowSignalAsync(desktop);

            // Handle application exit
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void EnsureWindowVisible(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            if (desktop.MainWindow is not MainWindow)
            {
                desktop.MainWindow?.Close();
                desktop.MainWindow = new MainWindow();
            }
            var w = (MainWindow)desktop.MainWindow;
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
        }
        catch
        {
            desktop.MainWindow?.Close();
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Show();
        }
    }

    private static async Task ListenForShowWindowSignalAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        while (true)
        {
            try
            {
                if (!File.Exists(SignalFilePath))
                {
                    await Task.Delay(1000);
                    continue;
                }
            }
            catch
            {
                await Task.Delay(1000);
                continue;
            }

            try { File.Delete(SignalFilePath); } catch { }
            EnsureWindowVisible(desktop);
        }
    }

    /// <summary>
    /// Switches between dark and light theme.
    /// Replaces Color + SolidColorBrush resources entirely (never casts/modifies existing brushes).
    /// This avoids the ImmutableSolidColorBrush crash that occurs when Avalonia freezes XAML-defined brushes.
    /// </summary>
    public static void SetTheme(bool darkMode)
    {
        var app = Current;
        if (app == null) return;

        try
        {
            // Switch FluentTheme built-in variant first (handles system controls like buttons, inputs)
            app.RequestedThemeVariant = darkMode ? ThemeVariant.Dark : ThemeVariant.Light;

            // Replace ALL custom resources with new values
            var r = app.Resources;

            if (darkMode)
            {
                // Dark theme colors
                r["AccentColor"] = Color.FromRgb(0x60, 0xCD, 0xFF);
                r["AccentColorSecondary"] = Color.FromRgb(0x00, 0x78, 0xD4);
                r["BackgroundColor"] = Color.FromRgb(0x0A, 0x0A, 0x0A);
                r["SurfaceColor"] = Color.FromRgb(0x1C, 0x1C, 0x1C);
                r["SurfaceColor2"] = Color.FromRgb(0x2D, 0x2D, 0x2D);
                r["CardBackgroundColor"] = Color.FromRgb(0x1E, 0x1E, 0x1E);
                r["SidebarBackgroundColor"] = Color.FromRgb(0x0F, 0x0F, 0x0F);
                r["TextPrimaryColor"] = Color.FromRgb(0xFF, 0xFF, 0xFF);
                r["TextSecondaryColor"] = Color.FromRgb(0x9E, 0x9E, 0x9E);
                r["TextTertiaryColor"] = Color.FromRgb(0x6E, 0x6E, 0x6E);
                r["BorderColor"] = Color.FromRgb(0x3D, 0x3D, 0x3D);
                r["SuccessColor"] = Color.FromRgb(0x4C, 0xAF, 0x50);
                r["WarningColor"] = Color.FromRgb(0xFF, 0x98, 0x00);
                r["ErrorColor"] = Color.FromRgb(0xF4, 0x43, 0x36);
                r["InfoColor"] = Color.FromRgb(0x21, 0x96, 0xF3);

                // Dark theme brushes — always create NEW instances
                r["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
                r["AccentSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                r["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
                r["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C));
                r["Surface2Brush"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                r["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                r["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F));
                r["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
                r["TextTertiaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));
                r["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
                r["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                r["WarningBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                r["ErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                r["InfoBrush"] = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
            }
            else
            {
                // Light theme colors — Windows 11 inspired
                r["AccentColor"] = Color.FromRgb(0x00, 0x78, 0xD4);
                r["AccentColorSecondary"] = Color.FromRgb(0x00, 0x5A, 0x9E);
                r["BackgroundColor"] = Color.FromRgb(0xF3, 0xF3, 0xF3);
                r["SurfaceColor"] = Color.FromRgb(0xFF, 0xFF, 0xFF);
                r["SurfaceColor2"] = Color.FromRgb(0xE8, 0xE8, 0xE8);
                r["CardBackgroundColor"] = Color.FromRgb(0xFF, 0xFF, 0xFF);
                r["SidebarBackgroundColor"] = Color.FromRgb(0xEB, 0xEB, 0xEB);
                r["TextPrimaryColor"] = Color.FromRgb(0x1A, 0x1A, 0x1A);
                r["TextSecondaryColor"] = Color.FromRgb(0x61, 0x61, 0x61);
                r["TextTertiaryColor"] = Color.FromRgb(0x9E, 0x9E, 0x9E);
                r["BorderColor"] = Color.FromRgb(0xD1, 0xD1, 0xD1);
                r["SuccessColor"] = Color.FromRgb(0x2E, 0x7D, 0x32);
                r["WarningColor"] = Color.FromRgb(0xE6, 0x51, 0x00);
                r["ErrorColor"] = Color.FromRgb(0xC6, 0x28, 0x28);
                r["InfoColor"] = Color.FromRgb(0x15, 0x65, 0xC0);

                // Light theme brushes — always create NEW instances
                r["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                r["AccentSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x5A, 0x9E));
                r["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
                r["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["Surface2Brush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                r["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xEB));
                r["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                r["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x61, 0x61, 0x61));
                r["TextTertiaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
                r["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD1));
                r["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                r["WarningBrush"] = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
                r["ErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                r["InfoBrush"] = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            }

            Console.WriteLine($"[App] Theme switched to {(darkMode ? "Dark" : "Light")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] SetTheme error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async System.Threading.Tasks.Task LoadSavedDataAsync()
    {
        try
        {
            await DashboardViewModel.RefreshCommand.ExecuteAsync(null);
            await KeybindsViewModel.LoadKeybindsCommand.ExecuteAsync(null);
            await TasksViewModel.LoadTasksCommand.ExecuteAsync(null);
            // Time limits already loaded synchronously before tracking starts
            await SchedulerViewModel.LoadSchedulesCommand.ExecuteAsync(null);
            await SettingsViewModel.LoadSettingsCommand.ExecuteAsync(null);

            // Apply saved settings on startup
            AnimationsEnabled = SettingsViewModel.EnableAnimations;
            SetTheme(SettingsViewModel.DarkMode);
            LocalizationService.Instance.LoadFromSettings(SettingsViewModel.Language);
            SystemTrayService.SetAutoStart(SettingsViewModel.LaunchOnStartup);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] LoadSavedData error: {ex.Message}");
        }
    }

    private void OnShutdownRequested(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            HotkeyService.Dispose();
            TaskSchedulerService.Dispose();
            TimeTrackingService.Dispose();
            ScheduleService.Dispose();
            ProcessMonitorService.Dispose();
            SystemTrayService.Dispose();
            AppUsageTrackingService.Dispose();
            PomodoroService.Dispose();
            HealthReminderService.Dispose();
            UpdateService.Dispose();

            try { if (File.Exists(SignalFilePath)) File.Delete(SignalFilePath); } catch { }
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }
    }
}
