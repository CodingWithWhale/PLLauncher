using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using PLLauncher.Helpers;
using PLLauncher.Services;
using PLLauncher.ViewModels;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
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

    // Main window reference for dynamic icon updates
    public static MainWindow? MainWindow { get; set; }

    // Temp path for toast notification icon
    private static readonly string GeneratedIconDir = Path.Combine(Path.GetTempPath(), "PLLauncher");
    internal static string GeneratedIconPath => Path.Combine(GeneratedIconDir, "appicon.ico");

    // Accent colour presets
    public static string CurrentAccentColor { get; set; } = "Blue";

    public static readonly Dictionary<string, (Color Primary, Color Secondary)> AccentColors = new()
    {
        ["Blue"]   = (Color.FromRgb(0x60, 0xCD, 0xFF), Color.FromRgb(0x00, 0x78, 0xD4)),
        ["Purple"] = (Color.FromRgb(0xBB, 0x86, 0xFC), Color.FromRgb(0x7C, 0x4D, 0xFF)),
        ["Green"]  = (Color.FromRgb(0x81, 0xC7, 0x84), Color.FromRgb(0x4C, 0xAF, 0x50)),
        ["Orange"] = (Color.FromRgb(0xFF, 0xB7, 0x4D), Color.FromRgb(0xFF, 0x98, 0x00)),
        ["Red"]    = (Color.FromRgb(0xE5, 0x73, 0x73), Color.FromRgb(0xF4, 0x43, 0x36)),
        ["Cyan"]   = (Color.FromRgb(0x4D, 0xD0, 0xE1), Color.FromRgb(0x00, 0xBC, 0xD4)),
        ["Custom"] = (Color.FromRgb(0x60, 0xCD, 0xFF), Color.FromRgb(0x00, 0x78, 0xD4)),
    };

    public static void SetAccentColor(string name)
    {
        if (!AccentColors.TryGetValue(name, out var colors))
            name = "Blue";
        CurrentAccentColor = name;
        var r = Current?.Resources;
        if (r == null) return;
        r["AccentColor"] = colors.Primary;
        r["AccentColorSecondary"] = colors.Secondary;
        r["AccentBrush"] = new SolidColorBrush(colors.Primary);
        r["AccentSecondaryBrush"] = new SolidColorBrush(colors.Secondary);

        UpdateAppIcons(colors.Secondary);
    }

    public static void SetCustomAccentColor(Color primary, Color secondary)
    {
        AccentColors["Custom"] = (primary, secondary);
        SetAccentColor("Custom");
    }

    private static byte[] RenderPlPng(Color backgroundColor, int size, float textSize)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bg = new SKColor(backgroundColor.R, backgroundColor.G, backgroundColor.B, backgroundColor.A);
        using var bgPaint = new SKPaint { Color = bg, IsAntialias = true };
        float margin = size <= 16 ? 1 : 2;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(margin, margin, size - margin, size - margin), margin * 3), bgPaint);
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White, IsAntialias = true, TextAlign = SKTextAlign.Center,
            Typeface = typeface, TextSize = textSize
        };
        canvas.DrawText("PL", size / 2f, size / 2f + textSize / 3f, textPaint);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] BuildIco(byte[][] pngs, int[] sizes)
    {
        int count = pngs.Length, headerSize = 6 + count * 16;
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((short)0); bw.Write((short)1); bw.Write((short)count);
        int offset = headerSize;
        for (int i = 0; i < count; i++)
        {
            bw.Write((byte)sizes[i]); bw.Write((byte)sizes[i]);
            bw.Write((byte)0); bw.Write((byte)0);
            bw.Write((short)1); bw.Write((short)32);
            bw.Write(pngs[i].Length); bw.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var p in pngs) bw.Write(p);
        return ms.ToArray();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
    private const uint WM_SETICON = 0x0080;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    internal static void ApplyWindowIcon(Window? window)
    {
        if (window == null || !File.Exists(GeneratedIconPath)) return;
        try
        {
            var hwnd = window.TryGetPlatformHandle()?.Handle;
            if (hwnd == null || hwnd.Value == IntPtr.Zero) return;

            var hIcon16 = LoadImage(IntPtr.Zero, GeneratedIconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            var hIcon32 = LoadImage(IntPtr.Zero, GeneratedIconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

            SendMessage(hwnd.Value, WM_SETICON, new IntPtr(0), hIcon16);
            SendMessage(hwnd.Value, WM_SETICON, new IntPtr(1), hIcon32);
            SetClassLongPtr(hwnd.Value, GCLP_HICONSM, hIcon16);
            SetClassLongPtr(hwnd.Value, GCLP_HICON, hIcon32);

            window.Icon = new WindowIcon(GeneratedIconPath);
        }
        catch { }
    }

    public static void UpdateAppIcons(Color secondaryColor)
    {
        try
        {
            Directory.CreateDirectory(GeneratedIconDir);
            var png32 = RenderPlPng(secondaryColor, 32, 18);
            var png16 = RenderPlPng(secondaryColor, 16, 9);
            File.WriteAllBytes(GeneratedIconPath, BuildIco(new[] { png32, png16 }, new[] { 32, 16 }));
            ToastHelper.GeneratedIconPath = GeneratedIconPath;

            ApplyWindowIcon(MainWindow);

            var ms = new MemoryStream(png32);
            using var bmp = new Bitmap(ms);
            SystemTrayService?.UpdateIcon(new WindowIcon(bmp));
        }
        catch { }
    }

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
            var mutex = new Mutex(false, MutexName, out createdNew);
            try
            {
                if (mutex.WaitOne(0))
                {
                    // We own the mutex (created new, or recovered from abandoned/closed)
                }
                else
                {
                    // Another instance holds the mutex — signal it to show window, then exit.
                    // Do NOT call desktop.Shutdown() here: we are inside OnFrameworkInitializationCompleted
                    // while the dispatcher is still starting up, and shutting it down mid-init crashes.
                    try { File.WriteAllText(SignalFilePath, DateTime.Now.ToString("O")); } catch { }
                    Environment.Exit(0);
                    return;
                }
            }
            catch (AbandonedMutexException amex)
            {
                // Previous owner crashed — but WaitOne still grants ownership.
                mutex = amex.Mutex ?? mutex;
            }
            _singleInstanceMutex = mutex;
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

            // Wire up notifications to show actual Windows toast balloons
            NotificationService.NotificationRequested += (_, e) =>
                ToastHelper.Show(e.Title, e.Message, e.Type);

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
            // Generate initial app icon .ico file (multi-size for taskbar)
            try
            {
                Directory.CreateDirectory(GeneratedIconDir);
                var initSecondary = AccentColors.GetValueOrDefault("Blue").Secondary;
                var p32 = RenderPlPng(initSecondary, 32, 18);
                var p16 = RenderPlPng(initSecondary, 16, 9);
                File.WriteAllBytes(GeneratedIconPath, BuildIco(new[] { p32, p16 }, new[] { 32, 16 }));
                ToastHelper.GeneratedIconPath = GeneratedIconPath;
            }
            catch { }

            desktop.MainWindow = new MainWindow();
            MainWindow = (MainWindow)desktop.MainWindow;

            // Must attach Opened handler BEFORE Show() — Opened fires synchronously during Show()
            desktop.MainWindow.Opened += async (_, _) =>
            {
                await Task.Delay(500);
                if (await UpdateService.PromptUpdateAsync(desktop.MainWindow))
                {
                    _isShuttingDown = true;
                    try { File.Delete(SignalFilePath); } catch { }
                    try { _singleInstanceMutex?.ReleaseMutex(); _singleInstanceMutex?.Dispose(); } catch { }
                    _singleInstanceMutex = null;
                    desktop.MainWindow?.Close();
                    Environment.Exit(0);
                }
            };

            desktop.MainWindow.Show();
            ToastHelper.MainWindowHandle = desktop.MainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

            // Load saved data on startup
            _ = LoadSavedDataAsync();

            // Load time limits and start services in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    var savedLimits = await DataService.LoadTimeLimitsAsync() ?? new();
                    Console.WriteLine($"[App] Loaded {savedLimits.Count} time limits from disk");

                    // Switch back to UI thread to update ViewModel
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        TimeTrackingService.LoadLimits(savedLimits);
                        TimeLimitsViewModel.TimeLimits = new(savedLimits);

                        // Start background services
                        TaskSchedulerService?.Start();
                        TimeTrackingService?.Start();
                        ScheduleService?.Start();
                        AppUsageTrackingService?.Start();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Service start error: {ex.Message}");
                }
            });

            // Wire up tray icon events
            SystemTrayService.ShowWindowRequested += (_, _) => EnsureWindowVisible(desktop);
            SystemTrayService.ExitRequested += (_, _) =>
            {
                _isShuttingDown = true;
                try { File.Delete(SignalFilePath); } catch { }
                try { _singleInstanceMutex?.ReleaseMutex(); _singleInstanceMutex?.Dispose(); } catch { }
                _singleInstanceMutex = null;
                desktop.MainWindow?.Close();
                // Force exit if desktop.Shutdown() doesn't complete
                _ = Task.Delay(2000).ContinueWith(_ => Environment.Exit(0));
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
            if (desktop.MainWindow is not PLLauncher.MainWindow)
            {
                desktop.MainWindow?.Close();
                desktop.MainWindow = new MainWindow();
            }
            var w = (MainWindow)desktop.MainWindow;
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
            MainWindow = w;
        }
        catch
        {
            desktop.MainWindow?.Close();
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Show();
            MainWindow = (MainWindow)desktop.MainWindow;
        }
    }

    private static async Task ListenForShowWindowSignalAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        while (!_isShuttingDown)
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

                SetAccentColor(CurrentAccentColor);
            }
            else
            {
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

                SetAccentColor(CurrentAccentColor);
            }
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
            CurrentAccentColor = SettingsViewModel.AccentColor;
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
        // Release the mutex FIRST so the next instance can always start,
        // even if one of the service Dispose calls below throws or hangs.
        try { if (File.Exists(SignalFilePath)) File.Delete(SignalFilePath); } catch { }
        try { _singleInstanceMutex?.ReleaseMutex(); _singleInstanceMutex?.Dispose(); } catch { }
        _singleInstanceMutex = null;

        // Now dispose services (best-effort, exceptions logged but never block next launch)
        try { HotkeyService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown HotkeyService: {ex.Message}"); }
        try { TaskSchedulerService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown TaskSchedulerService: {ex.Message}"); }
        try { TimeTrackingService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown TimeTrackingService: {ex.Message}"); }
        try { ScheduleService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown ScheduleService: {ex.Message}"); }
        try { ProcessMonitorService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown ProcessMonitorService: {ex.Message}"); }
        try { SystemTrayService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown SystemTrayService: {ex.Message}"); }
        try { AppUsageTrackingService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown AppUsageTrackingService: {ex.Message}"); }
        try { PomodoroService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown PomodoroService: {ex.Message}"); }
        try { HealthReminderService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown HealthReminderService: {ex.Message}"); }
        try { UpdateService.Dispose(); } catch (Exception ex) { Console.WriteLine($"[App] Shutdown UpdateService: {ex.Message}"); }
    }
}
