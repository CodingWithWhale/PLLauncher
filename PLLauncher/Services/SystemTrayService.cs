using Avalonia.Controls;
using System;
using System.IO;
using Microsoft.Win32;

namespace PLLauncher.Services;

public class SystemTrayService : IDisposable
{
    private TrayIcon? _trayIcon;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon != null) return;

        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        if (!File.Exists(iconPath)) return;

        var icon = new WindowIcon(iconPath);

        var showItem = new NativeMenuItem("Show PLLauncher");
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "PLLauncher",
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("PLLauncher", $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue("PLLauncher") != null)
                    key.DeleteValue("PLLauncher");
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _disposed = true;
    }
}
