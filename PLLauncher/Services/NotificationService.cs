using System;
using System.Collections.Generic;

namespace PLLauncher.Services;

public class NotificationService
{
    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    public void ShowNotification(string title, string message)
        => NotificationRequested?.Invoke(this, new() { Title = title, Message = message, Type = NotificationType.Info });

    public void ShowWarning(string title, string message)
        => NotificationRequested?.Invoke(this, new() { Title = title, Message = message, Type = NotificationType.Warning });

    public void ShowError(string title, string message)
        => NotificationRequested?.Invoke(this, new() { Title = title, Message = message, Type = NotificationType.Error });

    public void ShowActionNotification(string title, string message, Dictionary<string, Action> actions)
        => NotificationRequested?.Invoke(this, new() { Title = title, Message = message, Type = NotificationType.Action, Actions = actions });
}

public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; }
    public Dictionary<string, Action>? Actions { get; set; }
    public int SecondsRemaining { get; set; }
}

public enum NotificationType { Info, Warning, Error, Action, Countdown }
