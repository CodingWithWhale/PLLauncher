using Avalonia;
using System;
using System.IO;

namespace PLLauncher;

class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PLLauncher", "crash.log");

    [STAThread]
    public static void Main(string[] args)
    {
        // Handle uninstall command (called by installer uninstall step)
        if (args.Length > 0 && args[0] == "--uninstall")
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PLLauncher");
            if (Directory.Exists(appData))
            {
                try { Directory.Delete(appData, true); }
                catch { }
            }
            return;
        }

        // Catch unhandled exceptions on all threads
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogCrash("AppDomain unhandled", ex);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogCrash("Task unobserved", e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash("Main fatal", ex);
            Console.WriteLine($"\n=== PLLauncher Crash ===\n{ex.Message}\n\n{ex.StackTrace}");
            Console.WriteLine($"\nCrash log saved to: {LogPath}");
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex?.Message}\n{ex?.StackTrace}\n\n");
        }
        catch { }
    }
}
