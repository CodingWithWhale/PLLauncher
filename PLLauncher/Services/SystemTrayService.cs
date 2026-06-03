using System;

namespace PLLauncher.Services;

public class SystemTrayService : IDisposable
{
    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public void ShowWindow() => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    public void HideWindow() { /* Minimize to tray */ }
    public void ExitApplication() { ExitRequested?.Invoke(this, EventArgs.Empty); }
    public void ShowTrayNotification(string title, string message) { /* Tray balloon */ }
    public void Dispose() => GC.SuppressFinalize(this);
}
